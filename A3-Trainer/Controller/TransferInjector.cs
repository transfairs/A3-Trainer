using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace A3_Trainer.Controller
{
    /// <summary>
    /// Moves a player to another club at the next daily processing through the game's own transfer
    /// (FUN_0070CA69), so squads, line-ups, money, transfer history and news come out as for a
    /// transfer the game made.
    /// </summary>
    /// <remarks>
    /// A stub is placed in the game process and both calls of the day's transfer market
    /// (TransferCode.MarketCallSites) are redirected to it. With a request pending it fetches both
    /// clubs and the player as FUN_0076D021 does, lets the game build the new club's contract, moves
    /// the player and then jumps on to the original market; without one it only jumps on. It refuses
    /// the move if the player isn't in the old club's squad any more or the new squad is full, and
    /// leaves the outcome for the trainer. The redirects live in the running process only.
    ///
    /// Stub layout: +0x00 signature, +0x08 request flag, +0x0C outcome, +0x10 arguments (each a dword:
    /// player, old country, old number, new country, new number, fee, immediate), +0x30 contract,
    /// +0x50 code.
    /// </remarks>
    internal sealed class TransferInjector
    {
        /// <summary>Outcome of <see cref="Request"/>.</summary>
        internal enum RequestResult
        {
            /// <summary>The move will be made at the next daily processing.</summary>
            Requested,
            /// <summary>The target is the player's current club.</summary>
            SameClub,
            /// <summary>The redirects couldn't be set up (unexpected code at a call site, or no memory).</summary>
            HookFailed
        }

        /// <summary>What the stub did with the last request, as left in the game process.</summary>
        internal enum Outcome
        {
            /// <summary>Nothing to report (no request run yet, or already taken).</summary>
            None = 0,
            /// <summary>The player was moved (or, without "immediate", booked for the season end).</summary>
            Done = 1,
            /// <summary>The player wasn't in the old club's squad any more.</summary>
            NotInClub = 2,
            /// <summary>The new club's squad was full.</summary>
            SquadFull = 3
        }

        /// <summary>Absolute addresses of the call sites and game functions the stub uses.</summary>
        internal sealed class Targets
        {
            /// <summary>The CALL rel32 of the day's transfer market that are redirected.</summary>
            internal uint[] MarketCallSites;
            /// <summary>The game functions the stub calls: the transfer market and the player, club, contract and move helpers.</summary>
            internal uint Market, FetchPlayer, ReleasePlayer, FetchClub, ReleaseClub, BuildContract, Move;
        }

        /// <summary>The game's own code (TransferCode).</summary>
        internal static readonly Targets Game = new Targets
        {
            MarketCallSites = TransferCode.MarketCallSites,
            Market = TransferCode.Market,
            FetchPlayer = TransferCode.FetchPlayer,
            ReleasePlayer = TransferCode.ReleasePlayer,
            FetchClub = TransferCode.FetchClub,
            ReleaseClub = TransferCode.ReleaseClub,
            BuildContract = TransferCode.BuildContract,
            Move = TransferCode.Move,
        };

        /// <summary>Marks the start of an installed stub, so a trainer started later finds it again.</summary>
        internal static readonly byte[] Signature = Encoding.ASCII.GetBytes("A3XFER01");
        internal const int FlagOffset = 0x08;
        internal const int OutcomeOffset = 0x0C;
        internal const int ArgumentsOffset = 0x10;
        internal const int ContractOffset = 0x30;
        internal const int CodeOffset = 0x50;
        internal const int StubSize = 0x200;

        // Argument slots (dwords from ArgumentsOffset).
        internal const int ArgPlayer = 0, ArgOldCountry = 1, ArgOldNumber = 2, ArgNewCountry = 3, ArgNewNumber = 4, ArgFee = 5, ArgImmediate = 6;

        private readonly IGameMemory memory;
        private readonly IGameCode code;
        private readonly Targets targets;

        /// <summary>Creates an injector for the attached game; <paramref name="targets"/> defaults to the game's code.</summary>
        internal TransferInjector(IGameMemory memory, IGameCode code, Targets targets = null)
        {
            this.memory = memory;
            this.code = code;
            this.targets = targets ?? Game;
        }

        /// <summary>
        /// Requests moving <paramref name="player"/> from <paramref name="from"/> to <paramref name="to"/> for
        /// <paramref name="fee"/> DM. <paramref name="immediate"/> moves him right away, otherwise the game books
        /// the transfer for the season end. A new request replaces one that hasn't run yet.
        /// </summary>
        internal RequestResult Request(int player, ClubKey from, ClubKey to, int fee, bool immediate)
        {
            if (from.Equals(to))
                return RequestResult.SameClub;
            uint? stub = this.EnsureInstalled();
            if (!stub.HasValue)
                return RequestResult.HookFailed;

            uint[] arguments = { (uint)player, (uint)from.Country, (uint)from.Number, (uint)to.Country, (uint)to.Number, (uint)fee, immediate ? 1u : 0u };
            // Flag cleared first so the stub can't pick up half-written arguments of a replaced request.
            this.memory.Write(stub.Value + FlagOffset, BitConverter.GetBytes(0u));
            this.memory.Write(stub.Value + OutcomeOffset, BitConverter.GetBytes(0u));
            this.memory.Write(stub.Value + ArgumentsOffset, arguments.SelectMany(BitConverter.GetBytes).ToArray());
            this.memory.Write(stub.Value + FlagOffset, BitConverter.GetBytes(1u));
            Logger.Debug($"TransferInjector: player {player} from {from.Country}/{from.Number} to {to.Country}/{to.Number} requested (fee {fee}, immediate {immediate}, stub 0x{stub.Value:X}).");
            return RequestResult.Requested;
        }

        /// <summary>The player of a requested move that hasn't been made yet, or null.</summary>
        internal int? PendingPlayer()
        {
            uint? stub = this.FindInstalledStub();
            byte[] flag = stub.HasValue ? this.memory.Read(stub.Value + FlagOffset, 1) : null;
            if (flag == null || flag[0] == 0)
                return null;
            byte[] player = this.memory.Read(stub.Value + ArgumentsOffset + ArgPlayer * 4, 4);
            return player == null ? (int?)null : BitConverter.ToInt32(player, 0);
        }

        /// <summary>Returns what the last request came to and clears it, so each outcome is reported once.</summary>
        internal Outcome TakeOutcome()
        {
            uint? stub = this.FindInstalledStub();
            if (!stub.HasValue || this.PendingPlayer().HasValue)
                return Outcome.None;
            byte[] outcome = this.memory.Read(stub.Value + OutcomeOffset, 4);
            if (outcome == null || BitConverter.ToUInt32(outcome, 0) == 0)
                return Outcome.None;
            this.memory.Write(stub.Value + OutcomeOffset, BitConverter.GetBytes(0u));
            return (Outcome)BitConverter.ToUInt32(outcome, 0);
        }

        /// <summary>Returns the installed stub, installing it (or finishing a partial install) first; null if a call site isn't as expected.</summary>
        internal uint? EnsureInstalled()
        {
            uint?[] callTargets = this.targets.MarketCallSites.Select(this.ReadCallTarget).ToArray();
            if (callTargets.Any(t => !t.HasValue))
                return null;

            // Every site must go either to the original function or into one and the same stub of ours.
            uint? stub = null;
            foreach (uint target in callTargets.Select(t => t.Value).Where(t => t != this.targets.Market))
            {
                uint? found = this.StubAt(target - CodeOffset);
                if (!found.HasValue || (stub.HasValue && stub != found))
                    return null;
                stub = found;
            }

            if (!stub.HasValue)
            {
                stub = this.code.AllocateExecutable(StubSize);
                if (!stub.HasValue)
                    return null;
                this.memory.Write(stub.Value, BuildStub(stub.Value, this.targets));
            }

            for (int i = 0; i < callTargets.Length; i++)
            {
                if (callTargets[i].Value == this.targets.Market && !this.Redirect(this.targets.MarketCallSites[i], stub.Value + CodeOffset))
                    return null;
            }
            return stub;
        }

        private bool Redirect(uint callSite, uint target)
        {
            int rel = unchecked((int)(target - (callSite + 5)));
            bool written = this.code.WriteCode(callSite + 1, BitConverter.GetBytes(rel));
            if (written)
                Logger.Debug($"TransferInjector: redirected the call at 0x{callSite:X} to 0x{target:X}.");
            return written;
        }

        private uint? FindInstalledStub()
        {
            uint? target = this.ReadCallTarget(this.targets.MarketCallSites[0]);
            return target.HasValue && target.Value != this.targets.Market ? this.StubAt(target.Value - CodeOffset) : null;
        }

        // The call site must still be a CALL rel32; returns where it currently goes.
        private uint? ReadCallTarget(uint callSite)
        {
            byte[] call = this.memory.Read(callSite, 5);
            if (call == null || call[0] != 0xE8)
                return null;
            return unchecked((uint)(callSite + 5 + BitConverter.ToInt32(call, 1)));
        }

        // Accepts a redirected call only if it lands in our own stub (signature check), never in some other tool's patch.
        private uint? StubAt(uint stub)
        {
            byte[] signature = this.memory.Read(stub, Signature.Length);
            return signature != null && signature.SequenceEqual(Signature) ? stub : (uint?)null;
        }

        /// <summary>Builds the complete stub (data + code) at <paramref name="stubBase"/> - see the class remarks.</summary>
        internal static byte[] BuildStub(uint stubBase, Targets t)
        {
            byte[] stub = new byte[StubSize];
            Array.Copy(Signature, stub, Signature.Length);

            uint flag = stubBase + FlagOffset;
            uint outcome = stubBase + OutcomeOffset;
            uint contract = stubBase + ContractOffset;
            uint Arg(int slot) => stubBase + ArgumentsOffset + (uint)slot * 4;

            X86 c = new X86();
            c.Op(0x80, 0x3D).U32(flag).Op(0x00);                      // cmp byte [flag], 0
            c.Jcc(0x84, "done");                                      // je done
            c.Op(0x60);                                               // pushad
            c.Op(0xC6, 0x05).U32(flag).Op(0x00);                      // mov byte [flag], 0
            c.Op(0xC7, 0x05).U32(outcome).U32((uint)Outcome.NotInClub);

            // esi = old club, edi = new club (FUN_0063DD1C(country, number, 0xFFFF))
            c.Op(0x68).U32(0xFFFF).Op(0xFF, 0x35).U32(Arg(ArgOldNumber)).Op(0xFF, 0x35).U32(Arg(ArgOldCountry));
            c.Call(t.FetchClub, 3).Op(0x89, 0xC6);                    // mov esi, eax
            c.Op(0x68).U32(0xFFFF).Op(0xFF, 0x35).U32(Arg(ArgNewNumber)).Op(0xFF, 0x35).U32(Arg(ArgNewCountry));
            c.Call(t.FetchClub, 3).Op(0x89, 0xC7);                    // mov edi, eax
            c.Op(0x85, 0xF6).Jcc(0x84, "releaseClubs");               // test esi, esi; je releaseClubs
            c.Op(0x85, 0xFF).Jcc(0x84, "releaseClubs");               // test edi, edi; je releaseClubs

            // Is the player in the old club's squad? (ebx = found)
            c.Op(0x0F, 0xBF, 0x8E).U32(TransferCode.SquadCount);      // movsx ecx, word [esi+count]
            c.Op(0x8B, 0x15).U32(Arg(ArgPlayer));                     // mov edx, [player]
            c.Op(0x31, 0xDB);                                         // xor ebx, ebx
            c.Label("search").Op(0x85, 0xC9);                         // test ecx, ecx
            c.Jcc(0x8E, "searched");                                  // jle searched
            c.Op(0x49);                                               // dec ecx
            c.Op(0x8B, 0x84, 0x8E).U32(TransferCode.Squad);           // mov eax, [esi+ecx*4+squad]
            c.Op(0x66, 0x39, 0x10);                                   // cmp [eax], dx
            c.Jcc(0x85, "search");                                    // jne search
            c.Op(0x43);                                               // inc ebx
            c.Label("searched").Op(0x85, 0xDB);                       // test ebx, ebx
            c.Jcc(0x84, "releaseClubs");                              // je releaseClubs
            c.Op(0xC7, 0x05).U32(outcome).U32((uint)Outcome.SquadFull);
            c.Op(0x66, 0x81, 0xBF).U32(TransferCode.SquadCount).U16((ushort)TransferCode.SquadCapacity); // cmp word [edi+count], capacity
            c.Jcc(0x8D, "releaseClubs");                              // jge releaseClubs

            // ebx = player (FUN_00636459(number)); the contract built by the game for the new club
            c.Op(0xFF, 0x35).U32(Arg(ArgPlayer)).Call(t.FetchPlayer, 1).Op(0x89, 0xC3);
            c.Op(0x31, 0xC0);                                         // xor eax, eax
            for (uint i = 0; i < TransferCode.ContractSize; i += 4)
                c.Op(0xA3).U32(contract + i);                         // mov [contract+i], eax
            c.Op(0x57, 0x53, 0x68).U32(contract);                     // push edi; push ebx; push contract
            c.Call(t.BuildContract, 3);
            c.Op(0x80, 0x3D).U32(contract + TransferCode.ContractYears).Op(0x02); // cmp byte [years], 2
            c.Jcc(0x8D, "yearsOk");                                   // jge yearsOk
            c.Op(0xC6, 0x05).U32(contract + TransferCode.ContractYears).Op(0x02);
            c.Label("yearsOk");

            // FUN_0070CA69(player, new, old, fee, &contract, immediate)
            c.Op(0xFF, 0x35).U32(Arg(ArgImmediate)).Op(0x68).U32(contract);
            c.Op(0xFF, 0x35).U32(Arg(ArgFee)).Op(0x56, 0x57, 0x53);
            c.Call(t.Move, 6);
            c.Op(0xC7, 0x05).U32(outcome).U32((uint)Outcome.Done);
            c.Op(0x53).Call(t.ReleasePlayer, 1);                      // FUN_006364CF(player)

            c.Label("releaseClubs");
            c.Op(0x85, 0xFF).Jcc(0x84, "oldClub");                    // test edi, edi; je oldClub
            c.Op(0x57).Call(t.ReleaseClub, 1);                        // FUN_0063DF96(new)
            c.Label("oldClub");
            c.Op(0x85, 0xF6).Jcc(0x84, "restore");                    // test esi, esi; je restore
            c.Op(0x56).Call(t.ReleaseClub, 1);                        // FUN_0063DF96(old)
            c.Label("restore").Op(0x61);                              // popad
            c.Label("done").Op(0xB8).U32(t.Market).Op(0xFF, 0xE0);    // mov eax, market; jmp eax
            byte[] code = c.Build();

            if (CodeOffset + code.Length > StubSize)
                throw new InvalidOperationException("Transfer stub doesn't fit.");
            Array.Copy(code, 0, stub, CodeOffset, code.Length);
            return stub;
        }

        // Minimal x86 emitter: raw bytes, labels and rel32 jumps resolved at Build.
        private sealed class X86
        {
            private readonly List<byte> bytes = new List<byte>();
            private readonly Dictionary<string, int> labels = new Dictionary<string, int>();
            private readonly List<KeyValuePair<int, string>> fixups = new List<KeyValuePair<int, string>>();

            public X86 Op(params byte[] op) { this.bytes.AddRange(op); return this; }
            public X86 U32(uint value) { this.bytes.AddRange(BitConverter.GetBytes(value)); return this; }
            public X86 U16(ushort value) { this.bytes.AddRange(BitConverter.GetBytes(value)); return this; }
            public X86 Label(string name) { this.labels[name] = this.bytes.Count; return this; }

            // Jcc rel32 (0F cc).
            public X86 Jcc(byte condition, string label) => this.Op(0x0F, condition).Rel(label);

            // mov eax, target; call eax; add esp, 4 * arguments (cdecl).
            public X86 Call(uint target, int arguments) => this.Op(0xB8).U32(target).Op(0xFF, 0xD0, 0x83, 0xC4, (byte)(arguments * 4));

            private X86 Rel(string label)
            {
                this.fixups.Add(new KeyValuePair<int, string>(this.bytes.Count, label));
                return this.U32(0);
            }

            public byte[] Build()
            {
                byte[] code = this.bytes.ToArray();
                foreach (KeyValuePair<int, string> fixup in this.fixups)
                {
                    int rel = this.labels[fixup.Value] - (fixup.Key + 4);
                    Array.Copy(BitConverter.GetBytes(rel), 0, code, fixup.Key, 4);
                }
                return code;
            }
        }
    }
}
