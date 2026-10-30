using A3_Trainer.Controller;
using A3_Trainer.Model;
using System;
using System.Linq;
using System.Text;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>A3Game with missing, damaged or out-of-range game data.</summary>
    public class A3GameEdgeTests
    {
        private static uint Record(int index) => A3Addresses.PlayerArray + (uint)(index * A3Addresses.PlayerSize);

        [Fact]
        public void Today_WithoutACalendar_IsNull()
        {
            Assert.Null(new A3Game(new FakeGameMemory()).Today());
        }

        [Theory]
        [InlineData(1899, 10)]   // before any game year
        [InlineData(1999, -1)]
        [InlineData(1999, 366)]
        public void Today_WithAnImplausibleCalendarEntry_IsNull(int year, int dayOfYear)
        {
            FakeGameMemory m = A3GameTests.Career();
            m.Put(A3Addresses.Calendar + 184 * 4, BitConverter.GetBytes((short)year).Concat(BitConverter.GetBytes((short)dayOfYear)).ToArray());

            Assert.Null(new A3Game(m).Today());
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(256, 0)]
        [InlineData(0, -1)]
        [InlineData(0, 256)]
        public void ClubAddress_OutsideTheGamesKeyRange_IsNull(int country, int number)
        {
            Assert.Null(new A3Game(A3GameTests.Career()).ClubAddress(new ClubKey(country, number)));
        }

        [Fact]
        public void ClubAddress_BeyondTheClubArray_IsNull()
        {
            FakeGameMemory m = A3GameTests.Career();
            m.PutUInt16(A3Addresses.ClubIndexTable + 3 * 2, A3Addresses.ClubCount - 1);

            Assert.Null(new A3Game(m).ClubAddress(new ClubKey(3, 1)));
        }

        [Fact]
        public void ClubName_OfAnUnknownClub_IsEmpty()
        {
            Assert.Equal("", new A3Game(A3GameTests.Career()).ClubName(new ClubKey(14, 2)));
        }

        [Fact]
        public void ReadPlayerCache_SkipsUnreadableNodesEmptyEntriesAndInvalidPlayerNumbers()
        {
            FakeGameMemory m = A3GameTests.Career();
            uint root = 0x05000000;
            m.PutUInt32(A3Addresses.PlayerCacheRoot, root);
            // root: left = unreadable node, right = node with an unreadable entry; no entry itself.
            m.Map(root, 0x10);
            m.PutUInt32(root + A3Addresses.CacheNodeLeft, 0x05F00000);
            m.PutUInt32(root + A3Addresses.CacheNodeRight, root + 0x10);
            m.Map(root + 0x10, 0x10);
            m.PutUInt32(root + 0x10 + A3Addresses.CacheNodeEntry, 0x07000000);
            m.PutUInt32(root + 0x10 + A3Addresses.CacheNodeRight, root + 0x20);
            // Two more nodes with entries for player numbers the game can't have.
            m.Map(root + 0x20, 0x20);
            m.PutUInt32(root + 0x20 + A3Addresses.CacheNodeEntry, 0x06000000);
            m.PutUInt32(root + 0x20 + A3Addresses.CacheNodeRight, root + 0x30);
            m.PutUInt32(root + 0x30 + A3Addresses.CacheNodeEntry, 0x06000100);
            m.PutUInt16(0x06000000, 0xFFFF);                    // -1
            m.PutUInt16(0x06000100, A3Addresses.PlayerSlots);

            Assert.Empty(new A3Game(m).ReadPlayerCache());
        }

        [Fact]
        public void ReadPlayers_WithoutPlayerRecords_IsEmpty()
        {
            Assert.Empty(new A3Game(new FakeGameMemory()).ReadPlayers(new ClubKey(14, 1)));
        }

        [Fact]
        public void ReadPlayers_SkipsPlayersOfAnotherCountry_AndKeepsTheRecordIfTheWorkingCopyIsUnreadable()
        {
            FakeGameMemory m = A3GameTests.Career();
            A3GameTests.PutPlayer(m, 1039, 14, 1, 1, 11);
            A3GameTests.PutPlayer(m, 2000, 3, 1, 1, 9);
            // A cache entry for 1039 whose compact data isn't readable.
            m.PutUInt32(A3Addresses.PlayerCacheRoot, 0x05000000);
            m.Map(0x05000000, 0x10);
            m.PutUInt32(0x05000000 + A3Addresses.CacheNodeEntry, 0x06000000);
            m.PutUInt16(0x06000000, 1039);

            Player player = Assert.Single(new A3Game(m).ReadPlayers(new ClubKey(14, 1)));

            Assert.Equal(1039, player.Index);
            Assert.False(player.HasWorkingCopy);
            Assert.Equal(11, player.Strength);
        }

        [Fact]
        public void NextOpponent_WithoutPlayerRecords_IsNull()
        {
            Assert.Null(new A3Game(new FakeGameMemory()).NextOpponent(new ClubKey(14, 1)));
        }

        [Fact]
        public void ReadName_OfAnUnreadableOrUnterminatedString()
        {
            FakeGameMemory m = A3GameTests.Career();
            m.PutUInt32(A3Addresses.NameTable + 5 * 4, 0x02F00000);            // points nowhere
            m.PutUInt32(A3Addresses.NameTable + 6 * 4, 0x02000000);
            m.Put(0x02000000, Encoding.ASCII.GetBytes(new string('x', 32)));  // no terminator within 32 bytes
            var game = new A3Game(m);

            Assert.Equal("", game.ReadName(5));
            Assert.Equal(new string('x', 32), game.ReadName(6));
            Assert.Equal("", game.ReadName(7));                                 // null pointer
        }

        [Fact]
        public void SavePlayer_WithAnUnreadableRecord_StillWritesTheChanges()
        {
            var m = new FakeGameMemory();
            var original = new Player { Index = 5, Foot = 1, SkinColor = 0 };
            Player edited = original.Clone();
            edited.Foot = 2;
            edited.SkinColor = 3;

            new A3Game(m).SavePlayer(original, edited);

            Assert.Equal(new byte[] { 2 << 2 }, m.WrittenAt(Record(5) + RecordOffset.FootAndReputation));
            Assert.Equal(new byte[] { 3 }, m.WrittenAt(Record(5) + RecordOffset.Appearance));
        }

        [Fact]
        public void ReadClubInfo_WithoutACareer_IsNull()
        {
            Assert.Null(new A3Game(new FakeGameMemory()).ReadClubInfo());
        }

        [Fact]
        public void SaveClubInfo_WritesThePrivateMoney()
        {
            var m = new FakeGameMemory();
            var original = new ClubInfo { SeasonStartMoney = 1, ManagerPrivateMoney = 2 };
            ClubInfo edited = original.Clone();
            edited.ManagerPrivateMoney = 5000;

            new A3Game(m).SaveClubInfo(original, edited);

            MemoryWrite write = Assert.Single(m.Writes);
            Assert.Equal(A3Addresses.ManagerPrivateMoney, write.Address);
            Assert.Equal(BitConverter.GetBytes(5000), write.Data);
        }
    }
}
