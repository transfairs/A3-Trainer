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
        public void SaveClubInfo_WritesThePrivateMoney_ToTheManagerAndItsData()
        {
            var m = new FakeGameMemory();
            var original = new ClubInfo { SeasonStartMoney = 1, ManagerPrivateMoney = 2 };
            ClubInfo edited = original.Clone();
            edited.ManagerPrivateMoney = 5000;

            new A3Game(m).SaveClubInfo(original, edited);

            Assert.Equal(new[] { A3Addresses.ManagerPrivateMoney, A3Addresses.ManagerData + 0x138 }, m.Writes.Select(w => w.Address));
            Assert.All(m.Writes, w => Assert.Equal(BitConverter.GetBytes(5000), w.Data));
        }

        // Two human managers: 1 at 1.FV München (held by the game), 3 at Hamburger FC (not held, so his
        // values come from his data); the game has Hamburg loaded as the own club.
        internal static FakeGameMemory HotSeat()
        {
            FakeGameMemory m = A3GameTests.Career();
            m.PutUInt16(A3Addresses.ManagerCount, 2);
            m.PutUInt16(A3Addresses.ManagerOrder + 4, 3);
            uint data3 = A3Addresses.ManagerData + 2 * A3Addresses.ManagerDataStride;
            m.Map(data3, A3Addresses.ManagerDataStride);
            m.PutUInt16(data3 + 0x3E, 14);
            m.PutUInt16(data3 + 0x40, 10);
            m.Put(data3 + 0x20, Encoding.ASCII.GetBytes("Zweiter\0"));
            m.PutUInt32(data3 + 0x138, 777);
            m.Put(data3 + 0x146, 10, 20, 30);
            m.PutUInt32(data3 + 0x17C, 250000);
            m.Map(A3Addresses.ClubCache, A3Addresses.ClubCacheSlots * A3Addresses.ClubCacheEntrySize);
            m.Put(A3Addresses.ClubCache, 1, 14, 10);
            return m;
        }

        [Fact]
        public void Managers_InTurnOrder_AndTheActiveOneHasTheLoadedClub()
        {
            FakeGameMemory m = HotSeat();
            var game = new A3Game(m);

            Assert.Equal(new[] { 1, 3 }, game.Managers());
            Assert.Equal(3, game.ActiveManager());
            Assert.Equal(new ClubKey(14, 10), game.OwnClub());
            Assert.Equal(new ClubKey(14, 1), game.OwnClub(1));

            m.Put(A3Addresses.ClubCache, 1, 14, 1);
            Assert.Equal(1, game.ActiveManager());
            m.Put(A3Addresses.ClubCache, 0, 14, 10);  // slot unused: the first one
            Assert.Equal(1, game.ActiveManager());

            m.PutUInt16(A3Addresses.ManagerCount, 0);
            Assert.Empty(game.Managers());
            Assert.Equal(1, game.ActiveManager());
        }

        [Fact]
        public void Managers_SkipsInvalidAndRepeatedNumbers()
        {
            FakeGameMemory m = HotSeat();
            m.PutUInt16(A3Addresses.ManagerCount, 4);
            m.PutUInt16(A3Addresses.ManagerOrder + 6, 3);
            m.PutUInt16(A3Addresses.ManagerOrder + 8, 9);

            Assert.Equal(new[] { 1, 3 }, new A3Game(m).Managers());
        }

        [Fact]
        public void ClubInfo_OfAManagerTheGameDoesntHold_ComesFromAndGoesToHisData()
        {
            FakeGameMemory m = HotSeat();
            var game = new A3Game(m);
            uint object3 = (uint)(2 * A3Addresses.ManagerStride);
            uint data3 = A3Addresses.ManagerData + 2 * A3Addresses.ManagerDataStride;

            ClubInfo info = game.ReadClubInfo(3);
            Assert.Equal(3, info.Manager);
            Assert.Equal("Hamburger FC", info.Name);
            Assert.Equal("Zweiter", info.ManagerFirstName);
            Assert.Equal(777, info.ManagerPrivateMoney);
            Assert.Equal(20, info.PublicPopularity);
            Assert.Equal(250000, info.ManagerSalary);

            ClubInfo edited = info.Clone();
            edited.ManagerPrivateMoney = 999;
            game.SaveClubInfo(info, edited);

            Assert.Equal(999, (int)m.ReadUInt32(data3 + 0x138));
            Assert.Equal(BitConverter.GetBytes(999), m.WrittenAt(A3Addresses.ManagerPrivateMoney + object3));
            Assert.Equal(999, game.ReadClubInfo(3).ManagerPrivateMoney);
            Assert.Null(m.WrittenAt(A3Addresses.ManagerPrivateMoney)); // manager 1 untouched

            // A manager without a club (country 0) has no club page.
            m.PutUInt16(data3 + 0x3E, 0);
            m.PutUInt16(data3 + 0x40, 0);
            Assert.Null(game.ReadClubInfo(3));
        }

        [Fact]
        public void ManagerField_OutsideTheDataMap_StaysInTheObject()
        {
            FakeGameMemory m = HotSeat();
            uint field = A3Addresses.Manager + 0x1000;

            Assert.Equal(field + 2 * (uint)A3Addresses.ManagerStride, new A3Game(m).ManagerField(3, field));
        }
    }
}
