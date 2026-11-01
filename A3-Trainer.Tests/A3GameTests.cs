using A3_Trainer.Controller;
using A3_Trainer.Model;
using System;
using System.Linq;
using System.Text;
using Xunit;

namespace A3_Trainer.Tests
{
    public class A3GameTests
    {
        private static uint Record(int index) => A3Addresses.PlayerArray + (uint)(index * A3Addresses.PlayerSize);
        private static uint Club(int index) => A3Addresses.ClubArray + (uint)(index * A3Addresses.ClubSize);

        // A minimal career: build check vtables, manager 1 at 1.FV München (14/1), the club index
        // table, the calendar on 4.7.1999 and the name table.
        internal static FakeGameMemory Career()
        {
            var m = new FakeGameMemory();
            m.Map(A3Addresses.PlayerArray, A3Addresses.PlayerSlots * A3Addresses.PlayerSize);
            m.PutUInt32(A3Addresses.PlayerArray, A3Addresses.PlayerVtable);
            m.Map(A3Addresses.ClubArray, A3Addresses.ClubCount * A3Addresses.ClubSize);
            m.PutUInt32(A3Addresses.ClubArray, A3Addresses.ClubVtable);
            PutClub(m, 1, "1.FV München");
            PutClub(m, 10, "Hamburger FC");
            m.Map(A3Addresses.ClubIndexTable, 0x200);
            m.PutUInt16(A3Addresses.ManagerClubCountry, 14);
            m.PutUInt16(A3Addresses.ManagerClubNumber, 1);
            m.PutUInt16(A3Addresses.ManagerCount, 1);
            m.PutUInt16(A3Addresses.ManagerOrder + 2, 1);
            m.PutUInt16(A3Addresses.ManagerReferences + 2, 1);
            m.PutUInt16(A3Addresses.BaseYear, 1999);
            m.PutUInt16(A3Addresses.CalendarBase, 184);
            m.PutUInt16(A3Addresses.DayCounter, 0);
            m.Put(A3Addresses.Calendar + 184 * 4, 0xCF, 0x07, 184, 0);
            m.PutUInt32(A3Addresses.PlayerCacheRoot, 0);
            m.Map(A3Addresses.NameTable, A3Addresses.NameTableSlots * 4);
            return m;
        }

        internal static void PutClub(FakeGameMemory m, int index, string name)
        {
            m.PutUInt32(Club(index), A3Addresses.ClubVtable);
            m.Put(Club(index) + A3Addresses.ClubName, Encoding.GetEncoding(1252).GetBytes(name + "\0"));
        }

        internal static void PutName(FakeGameMemory m, int nameIndex, string name, uint stringAddress)
        {
            m.PutUInt32(A3Addresses.NameTable + (uint)(nameIndex * 4), stringAddress);
            m.Put(stringAddress, Encoding.GetEncoding(1252).GetBytes(name.PadRight(31, '\0') + "\0"));
        }

        internal static void PutPlayer(FakeGameMemory m, int index, int country, int number, int position, int strength)
        {
            byte[] r = PlayerCodecTests.HuhnRecord();
            BitConverter.GetBytes((ushort)index).CopyTo(r, RecordOffset.Index);
            r[RecordOffset.ClubCountry] = (byte)country;
            r[RecordOffset.ClubNumber] = (byte)number;
            r[RecordOffset.Position] = (byte)position;
            r[RecordOffset.Strength] = (byte)strength;
            m.Put(Record(index), r);
        }

        // One cache node per player, chained to the right; the entry's compact data is a copy of
        // the record's club and strength.
        internal static void PutCache(FakeGameMemory m, params int[] players)
        {
            uint node = 0x05000000;
            m.PutUInt32(A3Addresses.PlayerCacheRoot, node);
            for (int i = 0; i < players.Length; i++)
            {
                uint entry = 0x06000000 + (uint)(i * 0x100);
                uint next = i + 1 < players.Length ? node + 0x10 : 0;
                m.Map(node, 0x10);
                m.PutUInt32(node + A3Addresses.CacheNodeRight, next);
                m.PutUInt32(node + A3Addresses.CacheNodeEntry, entry);
                m.Map(entry, 0xD8);
                m.PutUInt16(entry, players[i]);
                byte[] record = m.Read(Record(players[i]), A3Addresses.PlayerSize);
                m.Put(entry + 8 + CompactOffset.ClubCountry, record[RecordOffset.ClubCountry], record[RecordOffset.ClubNumber]);
                m.Put(entry + 8 + CompactOffset.Strength, (byte)(record[RecordOffset.Strength] + 1), record[RecordOffset.Position]);
                node += 0x10;
            }
        }

        [Fact]
        public void IsSupportedBuild_ChecksBothVtables()
        {
            FakeGameMemory m = Career();
            Assert.True(new A3Game(m).IsSupportedBuild());

            m.PutUInt32(A3Addresses.ClubArray, 0x12345678);
            Assert.False(new A3Game(m).IsSupportedBuild());
            Assert.False(new A3Game(new FakeGameMemory()).IsSupportedBuild());
        }

        [Fact]
        public void Today_ComesFromTheCalendar()
        {
            GameDate? today = new A3Game(Career()).Today();

            Assert.True(today.HasValue);
            Assert.Equal("4.7.1999", today.Value.ToString());
        }

        [Fact]
        public void OwnClub_IsManager1sClub_AndNullWithoutACareer()
        {
            FakeGameMemory m = Career();
            Assert.Equal(new ClubKey(14, 1), new A3Game(m).OwnClub());
            Assert.Equal("1.FV München", new A3Game(m).ClubName(new ClubKey(14, 1)));

            m.PutUInt16(A3Addresses.ManagerClubNumber, 2); // empty club slot
            Assert.Null(new A3Game(m).OwnClub());
        }

        [Fact]
        public void ClubAddress_AddsTheCountrysFirstClubNumber()
        {
            FakeGameMemory m = Career();
            m.PutUInt16(A3Addresses.ClubIndexTable + 3 * 2, 9);

            Assert.Equal(Club(10), new A3Game(m).ClubAddress(new ClubKey(3, 1)));
            Assert.Null(new A3Game(m).ClubAddress(new ClubKey(3, 200)));
        }

        [Fact]
        public void ReadPlayerCache_WalksTheTree_AndSurvivesACycle()
        {
            FakeGameMemory m = Career();
            PutPlayer(m, 1039, 14, 1, 1, 11);
            PutPlayer(m, 1041, 14, 1, 2, 12);
            PutCache(m, 1039, 1041);
            m.PutUInt32(0x05000010 + A3Addresses.CacheNodeLeft, 0x05000000); // back edge

            var cache = new A3Game(m).ReadPlayerCache();

            Assert.Equal(new[] { 1039, 1041 }, cache.Keys.OrderBy(k => k));
            Assert.Equal(0x06000000u, cache[1039]);
        }

        [Fact]
        public void ReadPlayers_ReturnsTheClubsPlayers_WithNamesAndWorkingCopies_SortedByPosition()
        {
            FakeGameMemory m = Career();
            PutPlayer(m, 1041, 14, 1, 2, 12);
            PutPlayer(m, 1039, 14, 1, 1, 11);
            PutPlayer(m, 1255, 14, 10, 1, 10);
            PutName(m, 38, "Oliver", 0x02000000);
            PutName(m, 1871, "Huhn", 0x02000040);
            PutCache(m, 1039);

            var players = new A3Game(m).ReadPlayers(new ClubKey(14, 1));

            Assert.Equal(new[] { 1039, 1041 }, players.Select(p => p.Index));
            Assert.Equal("Oliver Huhn", players[0].FullName);
            Assert.True(players[0].HasWorkingCopy);
            Assert.Equal(12, players[0].Strength); // from the working copy, not the record's 11
            Assert.False(players[1].HasWorkingCopy);
        }

        [Fact]
        public void NextOpponent_IsTheOtherClubInTheCache()
        {
            FakeGameMemory m = Career();
            PutPlayer(m, 1039, 14, 1, 1, 11);
            PutPlayer(m, 1255, 14, 10, 1, 10);
            PutPlayer(m, 1256, 14, 10, 3, 8);
            PutCache(m, 1039, 1255, 1256);

            Assert.Equal(new ClubKey(14, 10), new A3Game(m).NextOpponent(new ClubKey(14, 1)));
        }

        [Fact]
        public void NextOpponent_IsNullWhenOnlyTheOwnSquadIsLoaded()
        {
            FakeGameMemory m = Career();
            PutPlayer(m, 1039, 14, 1, 1, 11);
            PutCache(m, 1039);

            Assert.Null(new A3Game(m).NextOpponent(new ClubKey(14, 1)));
        }

        [Fact]
        public void SavePlayer_WritesThroughToMemory()
        {
            FakeGameMemory m = Career();
            PutPlayer(m, 1039, 14, 1, 1, 11);
            PutCache(m, 1039);
            var game = new A3Game(m);
            Player original = game.ReadPlayers(new ClubKey(14, 1)).Single();
            Player edited = original.Clone();
            edited.Strength = 13;

            game.SavePlayer(original, edited);

            Assert.Equal(13, m.Get(Record(1039) + RecordOffset.Strength));
            Assert.Equal(13, m.Get(0x06000000 + 8 + CompactOffset.Strength));
        }

        [Fact]
        public void ClubInfo_ReadsAndWritesOnlyWhatChanged()
        {
            FakeGameMemory m = Career();
            m.PutUInt32(Club(1) + A3Addresses.ClubSeasonStartMoney, 10000000);
            m.PutUInt32(A3Addresses.ManagerPrivateMoney, 10000);
            m.PutUInt32(A3Addresses.ManagerSalary, 375000);
            m.Put(A3Addresses.ManagerFirstName, Encoding.ASCII.GetBytes("Test\0\0\0\0\0\0"));
            m.Put(A3Addresses.ManagerLastName, Encoding.ASCII.GetBytes("Trainer\0\0\0\0\0\0\0\0\0"));
            m.Map(Club(1) + A3Addresses.ClubShortName, 0x30);
            var game = new A3Game(m);

            ClubInfo info = game.ReadClubInfo();
            Assert.Equal("1.FV München", info.Name);
            Assert.Equal("Test", info.ManagerFirstName);
            Assert.Equal(375000, info.ManagerSalary);

            ClubInfo edited = info.Clone();
            edited.SeasonStartMoney = 20000000;
            game.SaveClubInfo(info, edited);

            MemoryWrite write = Assert.Single(m.Writes);
            Assert.Equal(Club(1) + A3Addresses.ClubSeasonStartMoney, write.Address);
            Assert.Equal(BitConverter.GetBytes(20000000), write.Data);
        }

        // A name pool with "Der", "Oliver" and "Huhn" (indices 0-2) and the table pointing into it.
        internal static FakeGameMemory WithNamePool(FakeGameMemory m)
        {
            uint data = A3Addresses.NamePool + A3Addresses.NamePoolData;
            m.Map(A3Addresses.NamePool, A3Addresses.NamePoolCount + 2);
            uint at = data;
            string[] names = { "Der", "Oliver", "Huhn" };
            for (int i = 0; i < names.Length; i++)
            {
                byte[] bytes = Encoding.GetEncoding(1252).GetBytes(names[i] + "\0");
                m.Put(at, bytes);
                m.PutUInt32(A3Addresses.NameTable + (uint)(i * 4), at);
                at += (uint)bytes.Length;
            }
            m.PutUInt32(A3Addresses.NamePool + A3Addresses.NamePoolWrite, at);
            m.PutUInt16(A3Addresses.NamePool + A3Addresses.NamePoolCount, 3);
            return m;
        }

        [Fact]
        public void NameIndex_ReusesAnExistingName_AndAppendsANewOneAsTheGameDoes()
        {
            FakeGameMemory m = WithNamePool(Career());
            var game = new A3Game(m);
            uint write = m.ReadUInt32(A3Addresses.NamePool + A3Addresses.NamePoolWrite);

            Assert.Equal(2, game.NameIndex("Huhn"));
            Assert.Empty(m.Writes);

            Assert.Equal(3, game.NameIndex("Zé Roberto"));
            Assert.Equal("Zé Roberto", game.ReadName(3));
            Assert.Equal(write, m.ReadUInt32(A3Addresses.NameTable + 3 * 4));
            Assert.Equal(write + 11, m.ReadUInt32(A3Addresses.NamePool + A3Addresses.NamePoolWrite));
            Assert.Equal(new byte[] { 4, 0 }, m.WrittenAt(A3Addresses.NamePool + A3Addresses.NamePoolCount));
            Assert.Equal(new byte[] { 4, 0 }, m.WrittenAt(A3Addresses.NameCount));
            Assert.Equal(3, game.NameIndex("Zé Roberto"));   // found now
            Assert.Equal(1, game.NameIndex("Oliver"));
        }

        [Fact]
        public void NameIndex_RefusesAFullOrUnreadablePool()
        {
            FakeGameMemory m = WithNamePool(Career());
            m.PutUInt32(A3Addresses.NamePool + A3Addresses.NamePoolWrite, A3Addresses.NamePool + A3Addresses.NamePoolCount - 2);
            Assert.Equal("Namenstabelle voll", Assert.Throws<InvalidOperationException>(() => new A3Game(m).NameIndex("Neu")).Message);

            m = WithNamePool(Career());
            m.PutUInt16(A3Addresses.NamePool + A3Addresses.NamePoolCount, 0);
            Assert.Equal("Namenstabelle nicht lesbar", Assert.Throws<InvalidOperationException>(() => new A3Game(m).NameIndex("Neu")).Message);

            // No pool mapped at all.
            Assert.Throws<InvalidOperationException>(() => new A3Game(Career()).NameIndex("Neu"));
        }

        [Fact]
        public void SavePlayer_Names_GoToTheRecord_TheArtistNameWithItsFlag()
        {
            FakeGameMemory m = WithNamePool(Career());
            PutPlayer(m, 1039, 14, 1, 1, 11);
            m.PutUInt16(Record(1039) + RecordOffset.FirstName, 1);
            m.PutUInt16(Record(1039) + RecordOffset.LastName, 2);
            m.PutUInt32(Record(1039) + RecordOffset.MoreFlags, 0x10);
            var game = new A3Game(m);
            Player original = game.ReadPlayers(new ClubKey(14, 1)).Single();
            Assert.Equal("Oliver Huhn", original.FullName);

            Player edited = original.Clone();
            edited.FirstName = " ";              // empty: kept
            edited.LastName = "Kahn ";
            edited.ArtistName = "Titan";
            game.SavePlayer(original, edited);

            Assert.Null(m.WrittenAt(Record(1039) + RecordOffset.FirstName));
            Assert.Equal(new byte[] { 3, 0 }, m.WrittenAt(Record(1039) + RecordOffset.LastName));
            Assert.Equal(new byte[] { 4, 0 }, m.WrittenAt(Record(1039) + RecordOffset.ArtistName));
            Assert.Equal(BitConverter.GetBytes(0x80000010u), m.WrittenAt(Record(1039) + RecordOffset.MoreFlags));

            Player titan = game.ReadPlayers(new ClubKey(14, 1)).Single();
            Assert.Equal("Oliver Kahn", titan.FullName);
            Assert.Equal("Titan", titan.ArtistName);

            Player plain = titan.Clone();
            plain.FirstName = "Olli";
            plain.ArtistName = "";
            game.SavePlayer(titan, plain);
            Assert.Equal(new byte[] { 5, 0 }, m.WrittenAt(Record(1039) + RecordOffset.FirstName));
            Assert.Equal(new byte[] { 0, 0 }, m.WrittenAt(Record(1039) + RecordOffset.ArtistName));
            Assert.Equal(BitConverter.GetBytes(0x10u), m.WrittenAt(Record(1039) + RecordOffset.MoreFlags));
        }

        [Fact]
        public void SaveClubInfo_ClubNames_GoToRecordAndWorkingCopy_CutToTheirFields()
        {
            FakeGameMemory m = Career();
            uint copy = 0x07000000;
            m.Map(A3Addresses.ClubCache, A3Addresses.ClubCacheSlots * A3Addresses.ClubCacheEntrySize);
            m.Put(A3Addresses.ClubCache, 1, 14, 1);
            m.PutUInt32(A3Addresses.ClubCache + A3Addresses.ClubCacheObject, copy);
            m.Map(copy, 0x1390);
            m.Map(Club(1) + A3Addresses.ClubShortName, 0x30);
            var game = new A3Game(m);
            ClubInfo info = game.ReadClubInfo();
            ClubInfo edited = info.Clone();
            edited.Name = "FC Sternenhimmel Nord-Ost";   // longer than 19
            edited.ShortName = "FCS";
            edited.City = "Sternstadt";

            game.SaveClubInfo(info, edited);

            byte[] name = Encoding.ASCII.GetBytes("FC Sternenhimmel No\0");
            Assert.Equal(name, m.WrittenAt(Club(1) + A3Addresses.ClubName));
            Assert.Equal(name, m.WrittenAt(copy + A3Addresses.ClubCopyName));
            Assert.Equal(Encoding.ASCII.GetBytes("FCS\0"), m.WrittenAt(Club(1) + A3Addresses.ClubShortName));
            Assert.Equal("Sternstadt", game.ReadClubInfo().City);
        }

        [Theory]
        [InlineData(1, true)]    // exactly one trainer entry carries the old names: it follows
        [InlineData(2, false)]   // two: none is touched
        [InlineData(0, false)]
        public void SaveClubInfo_RenamesTheManager_AndTheOneMatchingTrainerEntry(int entries, bool follows)
        {
            FakeGameMemory m = WithNamePool(Career());   // "Oliver" = 1, "Huhn" = 2
            m.Put(A3Addresses.ManagerFirstName, Encoding.ASCII.GetBytes("Oliver").Concat(new byte[4]).ToArray());
            m.Put(A3Addresses.ManagerLastName, Encoding.ASCII.GetBytes("Huhn").Concat(new byte[12]).ToArray());
            m.Map(A3Addresses.TrainerList, A3Addresses.TrainerSlots * A3Addresses.TrainerSize);
            for (int i = 0; i < entries; i++)
                m.Put(A3Addresses.TrainerList + (uint)((5 + i) * A3Addresses.TrainerSize), 1, 0, 2, 0);
            var game = new A3Game(m);
            ClubInfo info = game.ReadClubInfo();
            ClubInfo edited = info.Clone();
            edited.ManagerFirstName = "Jupp";
            edited.ManagerLastName = "Huhn";

            game.SaveClubInfo(info, edited);

            Assert.Equal(Encoding.ASCII.GetBytes("Jupp\0\0\0\0\0\0"), m.WrittenAt(A3Addresses.ManagerFirstName));
            Assert.Equal(Encoding.ASCII.GetBytes("Huhn").Concat(new byte[12]), m.WrittenAt(A3Addresses.ManagerLastName));
            uint entry = A3Addresses.TrainerList + 5 * A3Addresses.TrainerSize;
            if (follows)
            {
                Assert.Equal(new byte[] { 3, 0 }, m.WrittenAt(entry));   // "Jupp" appended as name 3
                Assert.Equal(new byte[] { 2, 0 }, m.WrittenAt(entry + 2));
            }
            else
                Assert.Null(m.WrittenAt(entry));
        }

        [Fact]
        public void ReadCountryNames_StopsAtTheFirstEmptyEntry()
        {
            FakeGameMemory m = Career();
            m.Map(A3Addresses.CountryNames, A3Addresses.CountrySlots * 4);
            m.Put(0x01000000, Encoding.GetEncoding(1252).GetBytes("Albanien\0"));
            m.Put(0x01000100, Encoding.GetEncoding(1252).GetBytes("Deutschland\0"));
            m.PutUInt32(A3Addresses.CountryNames, 0x01000000);
            m.PutUInt32(A3Addresses.CountryNames + 4, 0x01000100);

            Assert.Equal(new[] { "Albanien", "Deutschland" }, new A3Game(m).ReadCountryNames());
            Assert.Empty(new A3Game(Career()).ReadCountryNames());
        }

        [Fact]
        public void ReadName_RejectsIndicesOutsideTheTable()
        {
            var game = new A3Game(Career());
            Assert.Equal("", game.ReadName(0));
            Assert.Equal("", game.ReadName(A3Addresses.NameTableSlots));
        }
    }
}
