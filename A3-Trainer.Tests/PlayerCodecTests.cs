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

        private static uint Record(int index) => A3Addresses.PlayerArray + (uint)(index * A3Addresses.PlayerSize);
    }
}
