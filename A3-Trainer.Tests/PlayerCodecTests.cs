using A3_Trainer.Controller;
using A3_Trainer.Model;
using System;
using System.Linq;
using Xunit;

namespace A3_Trainer.Tests
{
    public class PlayerCodecTests
    {
        private const int BaseYear = 1999;

        // Oliver Huhn (player 1039) as found in the test career: TO, 1.FV München (14/1).
        internal static byte[] HuhnRecord()
        {
            var r = new byte[A3Addresses.PlayerSize];
            BitConverter.GetBytes(0x07CF0704u).CopyTo(r, RecordOffset.Used);
            r[RecordOffset.ClubCountry] = 14;
            r[RecordOffset.ClubNumber] = 1;
            BitConverter.GetBytes((ushort)1425).CopyTo(r, RecordOffset.Salary);
            BitConverter.GetBytes((ushort)178).CopyTo(r, RecordOffset.MatchBonus);
            BitConverter.GetBytes((ushort)2002).CopyTo(r, RecordOffset.ContractEnd);
            r[RecordOffset.BirthDay] = 15;
            r[RecordOffset.BirthMonth] = 6;
            BitConverter.GetBytes((ushort)1969).CopyTo(r, RecordOffset.BirthYear);
            BitConverter.GetBytes((ushort)1039).CopyTo(r, RecordOffset.Index);
            BitConverter.GetBytes((ushort)38).CopyTo(r, RecordOffset.FirstName);
            BitConverter.GetBytes((ushort)1871).CopyTo(r, RecordOffset.LastName);
            BitConverter.GetBytes((ushort)0x0006).CopyTo(r, RecordOffset.Skills);
            BitConverter.GetBytes((ushort)0x0001).CopyTo(r, RecordOffset.Traits);
            BitConverter.GetBytes((ushort)0x0002).CopyTo(r, RecordOffset.Character);
            BitConverter.GetBytes((ushort)5509).CopyTo(r, RecordOffset.Condition);
            BitConverter.GetBytes((ushort)6020).CopyTo(r, RecordOffset.Freshness);
            r[RecordOffset.Strength] = 11;
            r[RecordOffset.Position] = 1;
            r[RecordOffset.Talent] = 0;
            r[RecordOffset.ShirtNumber] = 1;
            r[RecordOffset.Nationality] = 14;
            r[RecordOffset.Morale] = 90;
            r[RecordOffset.FootAndReputation] = 0x87; // foot bits = 1 (rechts), other bits set
            return r;
        }

        [Fact]
        public void FromRecord_DecodesTheKnownFields()
        {
            Player p = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);

            Assert.Equal(1039, p.Index);
            Assert.Equal(14, p.ClubCountry);
            Assert.Equal(1, p.ClubNumber);
            Assert.Equal("15.6.1969", p.BirthDate.ToString());
            Assert.Equal(11, p.Strength);
            Assert.Equal(Tables.Goalkeeper, p.Position);
            Assert.Equal(1, p.Foot);
            Assert.Equal(0x0006, p.Skills);
            Assert.Equal(0x0001, p.Traits);
            Assert.Equal(0x0002, p.Character);
            Assert.Equal(5509, p.Condition);
            Assert.Equal(6020, p.Freshness);
            Assert.Equal(90, p.Morale);
            Assert.Equal(1425, p.Salary);
            Assert.Equal(178, p.MatchBonus);
            Assert.Equal(2002, p.ContractEndYear);
            Assert.False(p.HasWorkingCopy);
            Assert.False(p.IsInjured);
        }

        [Fact]
        public void IsUsed_IsFalseForAnEmptySlot()
        {
            Assert.False(PlayerCodec.IsUsed(new byte[A3Addresses.PlayerSize], 0));
            Assert.True(PlayerCodec.IsUsed(HuhnRecord(), 0));
        }

        [Fact]
        public void ApplyCompact_OverridesWithTheWorkingCopy_AndDecodesTheRelativeContractYear()
        {
            Player p = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            var c = new byte[A3Addresses.CompactSize];
            c[CompactOffset.Strength] = 13;
            c[CompactOffset.Position] = 1;
            c[CompactOffset.ClubCountry] = 14;
            c[CompactOffset.ClubNumber] = 1;
            c[CompactOffset.Talent] = 2;
            BitConverter.GetBytes((short)12).CopyTo(c, CompactOffset.InjuryDays);
            c[CompactOffset.ContractEnd] = 3;
            c[CompactOffset.Morale] = 77;

            PlayerCodec.ApplyCompact(p, c, BaseYear);

            Assert.Equal(13, p.Strength);
            Assert.Equal(2, p.Talent);
            Assert.Equal(12, p.InjuryDays);
            Assert.Equal(2002, p.ContractEndYear);
            Assert.Equal(77, p.Morale);
        }

        [Fact]
        public void Changes_WithoutChanges_WritesNothing()
        {
            Player p = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);

            Assert.Empty(PlayerCodec.Changes(p, p.Clone(), BaseYear, 0x87));
        }

        [Fact]
        public void Changes_WithoutWorkingCopy_WritesOnlyTheRecord()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            Player edited = original.Clone();
            edited.Strength = 12;

            var writes = PlayerCodec.Changes(original, edited, BaseYear, 0x87);

            MemoryWrite write = Assert.Single(writes);
            Assert.Equal(Record(1039) + RecordOffset.Strength, write.Address);
            Assert.Equal(new byte[] { 12 }, write.Data);
        }

        [Fact]
        public void Changes_BirthDate_IsWrittenToRecordAndWorkingCopy()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            original.CacheEntry = 0x065C11C8;
            Player edited = original.Clone();
            edited.BirthDate = new GameDate(1970, 7, 16);

            var writes = PlayerCodec.Changes(original, edited, BaseYear, 0x87);

            byte[] date = { 16, 7, 0xB2, 0x07 }; // 16.7.1970
            Assert.Equal(2, writes.Count);
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.BirthDay && w.Data.SequenceEqual(date));
            Assert.Contains(writes, w => w.Address == 0x065C11C8 + 8 + CompactOffset.BirthDate && w.Data.SequenceEqual(date));
        }

        [Fact]
        public void Changes_NationalityAndSkin_GoToTheRightPlaces()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            original.CacheEntry = 0x065C11C8;
            Player edited = original.Clone();
            edited.Nationality = 58;
            edited.SecondNationality = 8;
            edited.SkinColor = 3;

            var writes = PlayerCodec.Changes(original, edited, BaseYear, 0x87, 0xA8);

            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.Nationality && w.Data.SequenceEqual(new byte[] { 58 }));
            Assert.Contains(writes, w => w.Address == 0x065C11C8 + 8 + CompactOffset.Nationality && w.Data.SequenceEqual(new byte[] { 58 }));
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.SecondNationality && w.Data.SequenceEqual(new byte[] { 8 }));
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.Appearance && w.Data.SequenceEqual(new byte[] { 0xAB })); // other bits stay
            Assert.Equal(4, writes.Count);
        }

        [Theory]
        [InlineData(2000, 2, 29, true)]
        [InlineData(1999, 2, 29, false)]
        [InlineData(1969, 6, 31, false)]
        [InlineData(0, 0, 0, false)]
        public void GameDate_Exists_ChecksTheCalendar(int year, int month, int day, bool exists)
        {
            Assert.Equal(exists, new GameDate(year, month, day).Exists);
        }

        [Fact]
        public void Changes_WithWorkingCopy_WritesRecordAndCopy()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            original.CacheEntry = 0x065C11C8;
            Player edited = original.Clone();
            edited.Skills = 0x0016; // + Fausten

            var writes = PlayerCodec.Changes(original, edited, BaseYear, 0x87);

            Assert.Equal(2, writes.Count);
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.Skills && w.Data.SequenceEqual(new byte[] { 0x16, 0 }));
            Assert.Contains(writes, w => w.Address == 0x065C11C8 + 8 + CompactOffset.Skills && w.Data.SequenceEqual(new byte[] { 0x16, 0 }));
        }

        [Fact]
        public void Changes_ContractEnd_IsAYearInTheRecordAndAnOffsetInTheCopy()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            original.CacheEntry = 0x1000;
            Player edited = original.Clone();
            edited.ContractEndYear = 2004;

            var writes = PlayerCodec.Changes(original, edited, BaseYear, 0x87);

            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.ContractEnd && w.Data.SequenceEqual(BitConverter.GetBytes((ushort)2004)));
            Assert.Contains(writes, w => w.Address == 0x1000 + 8 + CompactOffset.ContractEnd && w.Data.SequenceEqual(new byte[] { 5 }));
        }

        [Fact]
        public void Changes_Foot_ReplacesOnlyTheFootBits()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            Player edited = original.Clone();
            edited.Foot = 3;

            MemoryWrite write = Assert.Single(PlayerCodec.Changes(original, edited, BaseYear, 0x87));

            Assert.Equal(Record(1039) + RecordOffset.FootAndReputation, write.Address);
            Assert.Equal(new byte[] { 0x8F }, write.Data);
        }

        [Fact]
        public void Changes_Healing_ClearsInjuryInRecordAndCopy()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            original.CacheEntry = 0x2000;
            original.InjuryDays = 14;
            original.Groggy = true;
            Player edited = original.Clone();
            edited.InjuryDays = 0;
            edited.Groggy = false;

            var writes = PlayerCodec.Changes(original, edited, BaseYear, 0x87);

            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.InjuryTo && w.Data.All(b => b == 0));
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.Groggy && w.Data.All(b => b == 0));
            Assert.Contains(writes, w => w.Address == 0x2000 + 8 + CompactOffset.InjuryDays && w.Data.All(b => b == 0));
            Assert.Contains(writes, w => w.Address == 0x2000 + 8 + CompactOffset.Groggy && w.Data.All(b => b == 0));
        }

        [Fact]
        public void Changes_ToAPlayerWhoStaysInjured_DoesNotHeal()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            original.CacheEntry = 0x2000;
            original.InjuryDays = 14;
            Player edited = original.Clone();
            edited.ShirtNumber = 7;

            var writes = PlayerCodec.Changes(original, edited, BaseYear, 0x87);

            Assert.DoesNotContain(writes, w => w.Address == Record(1039) + RecordOffset.InjuryTo);
        }

        [Fact]
        public void Changes_HealingAGroggyPlayerWithoutWorkingCopy_ClearsOnlyTheRecord()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            original.Groggy = true;
            Player edited = original.Clone();
            edited.Groggy = false;

            var writes = PlayerCodec.Changes(original, edited, BaseYear, 0x87);

            Assert.Equal(5, writes.Count);
            Assert.All(writes, w => Assert.InRange(w.Address, Record(1039), Record(1040) - 1));
        }

        [Fact]
        public void FromRecord_AndApplyCompact_ReadContractHealthAndForm()
        {
            byte[] r = HuhnRecord();
            BitConverter.GetBytes((ushort)25).CopyTo(r, RecordOffset.GoalBonus);
            BitConverter.GetBytes((ushort)300).CopyTo(r, RecordOffset.ReleaseClause);
            BitConverter.GetBytes((ushort)0x2041).CopyTo(r, RecordOffset.ContractFlags);
            BitConverter.GetBytes((ushort)0x1002).CopyTo(r, RecordOffset.Health);
            r[RecordOffset.FormBase] = 0xFF;

            Player p = PlayerCodec.FromRecord(r, 0, 1039);

            Assert.Equal(25, p.GoalBonus);
            Assert.Equal(300, p.ReleaseClause);
            Assert.Equal(0x2041, p.ContractFlags);
            Assert.Equal(0x1002, p.Health);
            Assert.Equal(-1, p.FormBase);

            var c = new byte[A3Addresses.CompactSize];
            BitConverter.GetBytes((ushort)40).CopyTo(c, CompactOffset.GoalBonus);
            BitConverter.GetBytes((ushort)500).CopyTo(c, CompactOffset.ReleaseClause);
            BitConverter.GetBytes((ushort)0x4002).CopyTo(c, CompactOffset.ContractFlags);
            BitConverter.GetBytes((ushort)0x0008).CopyTo(c, CompactOffset.Health);
            PlayerCodec.ApplyCompact(p, c, BaseYear);

            Assert.Equal(40, p.GoalBonus);
            Assert.Equal(500, p.ReleaseClause);
            Assert.Equal(0x4002, p.ContractFlags);
            Assert.Equal(0x0008, p.Health);
            Assert.Equal(-1, p.FormBase);   // only in the record
        }

        [Fact]
        public void Changes_ContractAndHealth_AreMirrored_FormOnlyInTheRecord()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            original.CacheEntry = 0x3000;
            Player edited = original.Clone();
            edited.GoalBonus = 50;
            edited.ReleaseClause = 1200;
            edited.ContractFlags = 0x6001;
            edited.Health = 0x0002;
            edited.FormBase = 20;

            var writes = PlayerCodec.Changes(original, edited, BaseYear, 0x87);

            uint copy = 0x3000 + 8;
            Assert.Equal(9, writes.Count);
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.GoalBonus && w.Data.SequenceEqual(BitConverter.GetBytes((ushort)50)));
            Assert.Contains(writes, w => w.Address == copy + CompactOffset.GoalBonus && w.Data.SequenceEqual(BitConverter.GetBytes((ushort)50)));
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.ReleaseClause && w.Data.SequenceEqual(BitConverter.GetBytes((ushort)1200)));
            Assert.Contains(writes, w => w.Address == copy + CompactOffset.ReleaseClause && w.Data.SequenceEqual(BitConverter.GetBytes((ushort)1200)));
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.ContractFlags && w.Data.SequenceEqual(BitConverter.GetBytes((ushort)0x6001)));
            Assert.Contains(writes, w => w.Address == copy + CompactOffset.ContractFlags && w.Data.SequenceEqual(BitConverter.GetBytes((ushort)0x6001)));
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.Health && w.Data.SequenceEqual(new byte[] { 2, 0 }));
            Assert.Contains(writes, w => w.Address == copy + CompactOffset.Health && w.Data.SequenceEqual(new byte[] { 2, 0 }));
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.FormBase && w.Data.SequenceEqual(new byte[] { 20 }));
        }

        [Fact]
        public void FromRecord_AndApplyCompact_ReadLooksAndNationalTeamStatus()
        {
            byte[] r = HuhnRecord();
            r[RecordOffset.Appearance] = 0x62;   // beard Bart + Schnurrbart, skin schwarz
            r[RecordOffset.Hair] = 0x24;         // Wuschelkopf, schwarz
            r[RecordOffset.FootAndReputation] = 0x54; // Fußballinländer, Kapitän zurückgetreten, foot right
            r[RecordOffset.NationalTeam] = 0x08;

            Player p = PlayerCodec.FromRecord(r, 0, 1039);

            Assert.Equal(2, p.SkinColor);
            Assert.Equal(0x6, p.Beard);
            Assert.Equal(4, p.HairColor);
            Assert.Equal(2, p.HairType);
            Assert.True(p.HomegrownForeigner);
            Assert.True(p.CaptainRetired);
            Assert.False(p.NationalPlayer);
            Assert.Equal(0x08, p.NationalTeam);

            var c = new byte[A3Addresses.CompactSize];
            c[CompactOffset.NationalTeam] = 0x0A;
            PlayerCodec.ApplyCompact(p, c, BaseYear);
            Assert.Equal(0x0A, p.NationalTeam);
        }

        [Fact]
        public void Changes_LooksAndStatus_KeepTheOtherBits_NationalTeamIsMirrored()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            original.CacheEntry = 0x3000;
            Player edited = original.Clone();
            edited.Beard = 0x8;
            edited.HairColor = 5;
            edited.HairType = 3;
            edited.NationalPlayer = false;
            edited.HomegrownForeigner = true;
            edited.NationalTeam = 0x02;

            var writes = PlayerCodec.Changes(original, edited, BaseYear, 0xA7, 0x1B);

            uint copy = 0x3000 + 8;
            Assert.Equal(5, writes.Count);
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.Appearance && w.Data.SequenceEqual(new byte[] { 0x8B })); // skin and bit 3 stay
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.Hair && w.Data.SequenceEqual(new byte[] { 0x35 }));
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.FootAndReputation && w.Data.SequenceEqual(new byte[] { 0x37 })); // foot, reputation, 0x20 stay
            Assert.Contains(writes, w => w.Address == Record(1039) + RecordOffset.NationalTeam && w.Data.SequenceEqual(new byte[] { 0x02 }));
            Assert.Contains(writes, w => w.Address == copy + CompactOffset.NationalTeam && w.Data.SequenceEqual(new byte[] { 0x02 }));
        }

        [Fact]
        public void Changes_CaptainRetired_ClearsOnlyItsBit()
        {
            Player original = PlayerCodec.FromRecord(HuhnRecord(), 0, 1039);
            original.CaptainRetired = true;
            Player edited = original.Clone();
            edited.CaptainRetired = false;

            MemoryWrite write = Assert.Single(PlayerCodec.Changes(original, edited, BaseYear, 0xC7));
            Assert.Equal(new byte[] { 0x87 }, write.Data);
        }

        private static uint Record(int index) => A3Addresses.PlayerArray + (uint)(index * A3Addresses.PlayerSize);
    }
}
