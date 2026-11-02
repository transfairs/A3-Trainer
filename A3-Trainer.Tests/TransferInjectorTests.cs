using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>
    /// TransferInjector: redirecting the market calls to the stub, the request, refusing foreign patches - and
    /// actually running the generated x86 stub (this test process is x86 like the game) against stand-ins for
    /// the game's functions and clubs.
    /// </summary>
    public class TransferInjectorTests
    {
        private static readonly ClubKey Bremen = new ClubKey(14, 0);
        private static readonly ClubKey Munich = new ClubKey(14, 1);

        // The game's two market calls in a fake memory, with code placed in mapped regions.
        internal sealed class FakeCode : IGameCode
        {
            private readonly FakeGameMemory memory;
            private uint next = 0x30000000;

            internal FakeCode(FakeGameMemory memory)
            {
                this.memory = memory;
                foreach (uint site in TransferCode.MarketCallSites)
                {
                    memory.Map(site, 5);
                    memory.Put(site, Call(site, TransferCode.Market));
                }
            }

            internal bool Refuse { get; set; }

            public uint? AllocateExecutable(int size)
            {
                if (this.Refuse)
                    return null;
                uint at = this.next;
                this.memory.Map(at, size);
                this.next += 0x1000;
                return at;
            }

            public bool WriteCode(uint address, byte[] data)
            {
                if (this.Refuse)
                    return false;
                this.memory.Put(address, data);
                return true;
            }
        }

        private static byte[] Call(uint site, uint target) =>
            new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(unchecked((int)(target - (site + 5))))).ToArray();

        private static uint CallTarget(IGameMemory m, uint site) => unchecked((uint)(site + 5 + BitConverter.ToInt32(m.Read(site, 5), 1)));

        [Fact]
        public void Request_RedirectsBothCallsAndStoresTheMove()
        {
            var m = new FakeGameMemory();
            var injector = new TransferInjector(m, new FakeCode(m));

            Assert.Null(injector.PendingPlayer());
            Assert.Equal(TransferInjector.RequestResult.Requested, injector.Request(1027, Bremen, Munich, 1000000, immediate: true));

            uint stub = CallTarget(m, TransferCode.MarketCallSites[0]) - TransferInjector.CodeOffset;
            Assert.Equal(TransferInjector.Signature, m.Read(stub, TransferInjector.Signature.Length));
            Assert.Equal(stub + TransferInjector.CodeOffset, CallTarget(m, TransferCode.MarketCallSites[1]));
            Assert.Equal(new uint[] { 1027, 14, 0, 14, 1, 1000000, 1 },
                Enumerable.Range(0, 7).Select(i => m.ReadUInt32(stub + TransferInjector.ArgumentsOffset + (uint)i * 4)).ToArray());
            Assert.Equal(1027, injector.PendingPlayer());
            Assert.Equal(TransferInjector.Outcome.None, injector.TakeOutcome());

            // A second request replaces the first in the same stub.
            Assert.Equal(TransferInjector.RequestResult.Requested, injector.Request(1028, Munich, Bremen, 0, immediate: false));
            Assert.Equal(stub + TransferInjector.CodeOffset, CallTarget(m, TransferCode.MarketCallSites[0]));
            Assert.Equal(0u, m.ReadUInt32(stub + TransferInjector.ArgumentsOffset + TransferInjector.ArgImmediate * 4));

            // The stub ran: the flag is down and the outcome is reported once.
            m.PutUInt32(stub + TransferInjector.FlagOffset, 0);
            m.PutUInt32(stub + TransferInjector.OutcomeOffset, (uint)TransferInjector.Outcome.SquadFull);
            Assert.Null(injector.PendingPlayer());
            Assert.Equal(TransferInjector.Outcome.SquadFull, injector.TakeOutcome());
            Assert.Equal(TransferInjector.Outcome.None, injector.TakeOutcome());
        }

        [Fact]
        public void Request_FinishesAPartialInstall_AndRefusesSameClubAndForeignCode()
        {
            var m = new FakeGameMemory();
            var code = new FakeCode(m);
            var injector = new TransferInjector(m, code);
            Assert.Equal(TransferInjector.RequestResult.SameClub, injector.Request(1027, Munich, Munich, 0, true));
            Assert.Null(m.WrittenAt(TransferCode.MarketCallSites[0] + 1));

            injector.Request(1027, Bremen, Munich, 0, true);
            uint stub = CallTarget(m, TransferCode.MarketCallSites[0]);
            // Only the first call redirected (e.g. the trainer was closed halfway): the second follows into the same stub.
            m.Put(TransferCode.MarketCallSites[1], Call(TransferCode.MarketCallSites[1], TransferCode.Market));
            Assert.Equal(TransferInjector.RequestResult.Requested, injector.Request(1027, Bremen, Munich, 0, true));
            Assert.Equal(stub, CallTarget(m, TransferCode.MarketCallSites[1]));

            // Another tool's patch, or code that isn't a call, is left alone.
            m.Put(TransferCode.MarketCallSites[1], Call(TransferCode.MarketCallSites[1], 0x00401000));
            Assert.Equal(TransferInjector.RequestResult.HookFailed, injector.Request(1027, Bremen, Munich, 0, true));
            m.Put(TransferCode.MarketCallSites[1], 0x90, 0x90, 0x90, 0x90, 0x90);
            Assert.Equal(TransferInjector.RequestResult.HookFailed, injector.Request(1027, Bremen, Munich, 0, true));
            Assert.Equal(1027, injector.PendingPlayer());   // the request before still waits

            var fresh = new FakeGameMemory();
            Assert.Equal(TransferInjector.RequestResult.HookFailed,
                new TransferInjector(fresh, new FakeCode(fresh) { Refuse = true }).Request(1027, Bremen, Munich, 0, true));
            Assert.Equal(TransferInjector.Outcome.None, new TransferInjector(fresh, new FakeCode(fresh)).TakeOutcome());
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void StubEntry();

        // Stand-ins for the game in this process: two clubs (old = country 1, new = country 2), one player and
        // recording versions of every function the stub calls.
        private sealed class Game
        {
            internal readonly uint Stub, Record, OldClub, NewClub, Player;
            internal readonly LiveGameMemory Memory;
            private readonly ProcessMemory process = new ProcessMemory();
            private readonly uint clubTable;

            // Record slots (dwords): call counters, then the move's arguments.
            internal const int FetchClubCalls = 0, ReleaseClubCalls = 1, FetchPlayerCalls = 2, ReleasePlayerCalls = 3,
                MoveCalls = 4, MarketCalls = 5, Arguments = 8;

            internal Game()
            {
                Assert.True(this.process.OpenProcess(Process.GetCurrentProcess().Id));
                this.Memory = new LiveGameMemory(this.process);
                uint block = this.Memory.AllocateExecutable(0x3000).Value;
                this.Stub = block;
                uint code = block + 0x400;
                this.Record = block + 0x1000;
                this.clubTable = block + 0x1100;
                this.OldClub = block + 0x1200;
                this.NewClub = block + 0x1C00;
                this.Player = block + 0x2800;

                this.Write(this.clubTable + 4, BitConverter.GetBytes(this.OldClub));
                this.Write(this.clubTable + 8, BitConverter.GetBytes(this.NewClub));
                this.Write(this.Player, BitConverter.GetBytes((ushort)1027));
                this.SetSquad(this.OldClub, this.Player);

                uint R(int slot) => this.Record + (uint)slot * 4;
                byte[] A(uint a) => BitConverter.GetBytes(a);
                byte[] Inc(int slot) => new byte[] { 0xFF, 0x05 }.Concat(A(R(slot))).ToArray();
                byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
                byte[] StoreArgs(int count) => Concat(Enumerable.Range(0, count).Select(i =>
                    Concat(new byte[] { 0x8B, 0x44, 0x24, (byte)(4 + i * 4), 0xA3 }, A(R(Arguments + i)))).ToArray());

                var t = new TransferInjector.Targets();
                uint next = code;
                uint Place(byte[] bytes) { uint at = next; this.Write(at, bytes); next += 0x60; return at; }

                t.Market = Place(Concat(Inc(MarketCalls), new byte[] { 0xC3 }));
                // FUN_0063DD1C: the club by country from the table (0 for an unknown one).
                t.FetchClub = Place(Concat(new byte[] { 0x8B, 0x44, 0x24, 0x04, 0x8B, 0x04, 0x85 }, A(this.clubTable), Inc(FetchClubCalls), new byte[] { 0xC3 }));
                t.ReleaseClub = Place(Concat(Inc(ReleaseClubCalls), new byte[] { 0xC3 }));
                t.FetchPlayer = Place(Concat(new byte[] { 0xB8 }, A(this.Player), Inc(FetchPlayerCalls), new byte[] { 0xC3 }));
                t.ReleasePlayer = Place(Concat(Inc(ReleasePlayerCalls), new byte[] { 0xC3 }));
                // Salary 77, one year - the stub must raise the years to 2.
                t.BuildContract = Place(new byte[] { 0x8B, 0x44, 0x24, 0x04, 0x66, 0xC7, 0x00, 77, 0, 0xC6, 0x40, 0x0C, 1, 0xC3 });
                t.Move = Place(Concat(StoreArgs(6), Inc(MoveCalls), new byte[] { 0xC3 }));
                t.MarketCallSites = new[] { Place(new byte[5]) };

                this.Write(this.Stub, TransferInjector.BuildStub(this.Stub, t));
            }

            internal void Write(uint address, byte[] bytes) => this.Memory.Write(address, bytes);
            internal uint U32(uint address) => BitConverter.ToUInt32(this.Memory.Read(address, 4), 0);
            internal uint Rec(int slot) => this.U32(this.Record + (uint)slot * 4);
            internal uint[] Args(int count) => Enumerable.Range(0, count).Select(i => this.Rec(Arguments + i)).ToArray();

            internal void SetSquad(uint club, params uint[] players)
            {
                this.Write(club + TransferCode.SquadCount, BitConverter.GetBytes((short)players.Length));
                for (int i = 0; i < players.Length; i++)
                    this.Write(club + TransferCode.Squad + (uint)i * 4, BitConverter.GetBytes(players[i]));
            }

            internal void RequestMove(uint oldCountry = 1, uint newCountry = 2, bool immediate = true)
            {
                uint[] args = { 1027, oldCountry, 9, newCountry, 7, 500000, immediate ? 1u : 0u };
                this.Write(this.Stub + TransferInjector.ArgumentsOffset, args.SelectMany(BitConverter.GetBytes).ToArray());
                this.Write(this.Stub + TransferInjector.FlagOffset, BitConverter.GetBytes(1u));
            }

            internal void Run() => Marshal.GetDelegateForFunctionPointer<StubEntry>(new IntPtr(this.Stub + TransferInjector.CodeOffset))();

            internal uint Outcome => this.U32(this.Stub + TransferInjector.OutcomeOffset);
            internal uint Contract => this.Stub + TransferInjector.ContractOffset;

        }

        [Fact]
        public void Stub_MovesThroughTheGame_ThenOnlyJumpsOn() => RunInGame(game =>
        {
            game.Write(game.Contract, Enumerable.Repeat((byte)0xEE, TransferCode.ContractSize).ToArray());
            game.RequestMove(immediate: false);
            game.Run();

            Assert.Equal((uint)TransferInjector.Outcome.Done, game.Outcome);
            Assert.Equal(0u, game.U32(game.Stub + TransferInjector.FlagOffset));
            Assert.Equal(1u, game.Rec(Game.MoveCalls));
            // FUN_0070CA69(player, new, old, fee, &contract, immediate)
            Assert.Equal(new[] { game.Player, game.NewClub, game.OldClub, 500000u, game.Contract, 0u }, game.Args(6));
            Assert.Equal(77, BitConverter.ToInt16(game.Memory.Read(game.Contract, 2), 0));
            Assert.Equal(2, game.Memory.Read(game.Contract + TransferCode.ContractYears, 1)[0]);
            Assert.Equal(0u, game.U32(game.Contract + 0x10));      // zeroed before the game fills it
            Assert.Equal(new uint[] { 2, 2, 1, 1, 1, 1 }, Enumerable.Range(0, 6).Select(game.Rec).ToArray());

            // No request: straight on to the market.
            game.Run();
            Assert.Equal(1u, game.Rec(Game.MoveCalls));
            Assert.Equal(2u, game.Rec(Game.MarketCalls));
            Assert.Equal(2u, game.Rec(Game.FetchClubCalls));
        });

        [Fact]
        public void Stub_FindsThePlayerAnywhereInTheSquad_AndPassesImmediate() => RunInGame(game =>
        {
            uint other = game.Player + 0x200;
            game.Write(other, BitConverter.GetBytes((ushort)1));
            game.SetSquad(game.OldClub, game.Player, other, other);
            game.RequestMove();
            game.Run();

            Assert.Equal((uint)TransferInjector.Outcome.Done, game.Outcome);
            Assert.Equal(1u, game.Args(6)[5]);
        });

        [Fact]
        public void Stub_WhenThePlayerLeftTheOldClub_RefusesAndReleasesTheClubs() => RunInGame(game =>
        {
            game.Write(game.Player, BitConverter.GetBytes((ushort)4321));
            game.RequestMove();
            game.Run();

            Assert.Equal((uint)TransferInjector.Outcome.NotInClub, game.Outcome);
            Assert.Equal(0u, game.Rec(Game.MoveCalls));
            Assert.Equal(0u, game.Rec(Game.FetchPlayerCalls));
            Assert.Equal(2u, game.Rec(Game.ReleaseClubCalls));
            Assert.Equal(1u, game.Rec(Game.MarketCalls));
        });

        [Fact]
        public void Stub_WhenTheNewSquadIsFull_Refuses() => RunInGame(game =>
        {
            uint other = game.Player + 0x200;
            game.SetSquad(game.NewClub, Enumerable.Repeat(other, TransferCode.SquadCapacity).ToArray());
            game.RequestMove();
            game.Run();

            Assert.Equal((uint)TransferInjector.Outcome.SquadFull, game.Outcome);
            Assert.Equal(0u, game.Rec(Game.MoveCalls));
            Assert.Equal(0u, game.Rec(Game.FetchPlayerCalls));
            Assert.Equal(2u, game.Rec(Game.ReleaseClubCalls));
        });

        [Fact]
        public void Stub_WhenAClubIsUnknown_ReleasesOnlyTheOtherOne() => RunInGame(game =>
        {
            game.RequestMove(newCountry: 3);
            game.Run();

            Assert.Equal((uint)TransferInjector.Outcome.NotInClub, game.Outcome);
            Assert.Equal(0u, game.Rec(Game.MoveCalls));
            Assert.Equal(1u, game.Rec(Game.ReleaseClubCalls));

            game.RequestMove(oldCountry: 3);
            game.Run();
            Assert.Equal(2u, game.Rec(Game.ReleaseClubCalls));
            Assert.Equal(2u, game.Rec(Game.MarketCalls));
        });

        private static void RunInGame(Action<Game> test)
        {
            test(new Game());
        }

        [Fact]
        public void BuildStub_FitsAndStartsWithTheSignature()
        {
            byte[] stub = TransferInjector.BuildStub(0x10000000, TransferInjector.Game);
            Assert.Equal(TransferInjector.StubSize, stub.Length);
            Assert.Equal(TransferInjector.Signature, stub.Take(TransferInjector.Signature.Length));
        }
    }
}
