using A3_Trainer.Model;
using System;
using System.Collections.Generic;

namespace A3_Trainer.Controller
{
    /// <summary>One write into game memory.</summary>
    internal struct MemoryWrite
    {
        /// <summary>Absolute address to write to.</summary>
        internal uint Address { get; }
        /// <summary>The bytes to write.</summary>
        internal byte[] Data { get; }

        /// <summary>One write of <paramref name="data"/> at <paramref name="address"/>.</summary>
        internal MemoryWrite(uint address, byte[] data)
        {
            this.Address = address;
            this.Data = data;
        }
    }

    /// <summary>
    /// Translates between the game's player memory (record and compact working copy) and the
    /// trainer's Player model. Pure byte handling, no process access.
    /// </summary>
    internal static class PlayerCodec
    {
        // A field the game keeps in the record and, for loaded players, in the working copy too.
        private sealed class Field
        {
            internal Func<Player, int> Get;
            internal int Record;
            internal int Compact;
            internal int Size;
        }

        private static readonly Field[] MirroredFields =
        {
            new Field { Get = p => p.Strength,      Record = RecordOffset.Strength,      Compact = CompactOffset.Strength,      Size = 1 },
            new Field { Get = p => p.Nationality,   Record = RecordOffset.Nationality,   Compact = CompactOffset.Nationality,   Size = 1 },
            new Field { Get = p => p.SecondNationality, Record = RecordOffset.SecondNationality, Compact = -1,                  Size = 1 },
            new Field { Get = p => p.Position,      Record = RecordOffset.Position,      Compact = CompactOffset.Position,      Size = 1 },
            new Field { Get = p => p.SidePosition1, Record = RecordOffset.SidePosition1, Compact = CompactOffset.SidePosition1, Size = 1 },
            new Field { Get = p => p.SidePosition2, Record = RecordOffset.SidePosition2, Compact = CompactOffset.SidePosition2, Size = 1 },
            new Field { Get = p => p.Talent,        Record = RecordOffset.Talent,        Compact = CompactOffset.Talent,        Size = 1 },
            new Field { Get = p => p.ShirtNumber,   Record = RecordOffset.ShirtNumber,   Compact = CompactOffset.ShirtNumber,   Size = 1 },
            new Field { Get = p => p.Skills,        Record = RecordOffset.Skills,        Compact = CompactOffset.Skills,        Size = 2 },
            new Field { Get = p => p.Skills2,       Record = RecordOffset.Skills2,       Compact = CompactOffset.Skills2,       Size = 2 },
            new Field { Get = p => p.Traits,        Record = RecordOffset.Traits,        Compact = CompactOffset.Traits,        Size = 2 },
            new Field { Get = p => p.Character,     Record = RecordOffset.Character,     Compact = CompactOffset.Character,     Size = 2 },
            new Field { Get = p => p.Condition,     Record = RecordOffset.Condition,     Compact = CompactOffset.Condition,     Size = 2 },
            new Field { Get = p => p.Freshness,     Record = RecordOffset.Freshness,     Compact = CompactOffset.Freshness,     Size = 2 },
            new Field { Get = p => p.Morale,        Record = RecordOffset.Morale,        Compact = CompactOffset.Morale,        Size = 1 },
            new Field { Get = p => p.Salary,        Record = RecordOffset.Salary,        Compact = CompactOffset.Salary,        Size = 2 },
            new Field { Get = p => p.MatchBonus,    Record = RecordOffset.MatchBonus,    Compact = CompactOffset.MatchBonus,    Size = 2 },
            new Field { Get = p => p.GoalBonus,     Record = RecordOffset.GoalBonus,     Compact = CompactOffset.GoalBonus,     Size = 2 },
            new Field { Get = p => p.ReleaseClause, Record = RecordOffset.ReleaseClause, Compact = CompactOffset.ReleaseClause, Size = 2 },
            new Field { Get = p => p.ContractFlags, Record = RecordOffset.ContractFlags, Compact = CompactOffset.ContractFlags, Size = 2 },
            new Field { Get = p => p.Health,        Record = RecordOffset.Health,        Compact = CompactOffset.Health,        Size = 2 },
            new Field { Get = p => p.NationalTeam,  Record = RecordOffset.NationalTeam,  Compact = CompactOffset.NationalTeam,  Size = 1 },
        };

        /// <summary>Whether a record slot holds a player (empty slots are all zero after the header).</summary>
        internal static bool IsUsed(byte[] data, int offset) => BitConverter.ToUInt32(data, offset + RecordOffset.Used) != 0;

        /// <summary>Decodes the record at <paramref name="offset"/> in <paramref name="data"/>; names stay empty.</summary>
        internal static Player FromRecord(byte[] data, int offset, int index)
        {
            int Byte(int o) => data[offset + o];
            int Word(int o) => BitConverter.ToUInt16(data, offset + o);

            return new Player
            {
                Index = index,
                ClubCountry = Byte(RecordOffset.ClubCountry),
                ClubNumber = Byte(RecordOffset.ClubNumber),
                BirthDate = new GameDate(Word(RecordOffset.BirthYear), Byte(RecordOffset.BirthMonth), Byte(RecordOffset.BirthDay)),
                Nationality = Byte(RecordOffset.Nationality),
                SecondNationality = Byte(RecordOffset.SecondNationality),
                SkinColor = Byte(RecordOffset.Appearance) & 7,
                Beard = Byte(RecordOffset.Appearance) >> 4,
                HairColor = Byte(RecordOffset.Hair) & 0x0F,
                HairType = Byte(RecordOffset.Hair) >> 4,
                HomegrownForeigner = (Byte(RecordOffset.FootAndReputation) & StatusFlag.HomegrownForeigner) != 0,
                NationalPlayer = (Byte(RecordOffset.FootAndReputation) & StatusFlag.NationalPlayer) != 0,
                CaptainRetired = (Byte(RecordOffset.FootAndReputation) & StatusFlag.CaptainRetired) != 0,
                NationalTeam = Byte(RecordOffset.NationalTeam),
                Relationship = Byte(RecordOffset.Relationship),
                Hobby = Byte(RecordOffset.Hobby),
                Flags = BitConverter.ToUInt32(data, offset + RecordOffset.Flags),
                Strength = Byte(RecordOffset.Strength),
                Position = Byte(RecordOffset.Position),
                SidePosition1 = Byte(RecordOffset.SidePosition1),
                SidePosition2 = Byte(RecordOffset.SidePosition2),
                Talent = Byte(RecordOffset.Talent),
                ShirtNumber = Byte(RecordOffset.ShirtNumber),
                Foot = (Byte(RecordOffset.FootAndReputation) >> 2) & 3,
                Reputation = Byte(RecordOffset.FootAndReputation) & 3,
                Skills = Word(RecordOffset.Skills),
                Skills2 = Word(RecordOffset.Skills2),
                Traits = Word(RecordOffset.Traits),
                Character = Word(RecordOffset.Character),
                Condition = Word(RecordOffset.Condition),
                Freshness = Word(RecordOffset.Freshness),
                Morale = Byte(RecordOffset.Morale),
                FormBase = (sbyte)Byte(RecordOffset.FormBase),
                Health = Word(RecordOffset.Health),
                InjuryType = Byte(RecordOffset.InjuryType),
                RecordInjured = Word(RecordOffset.InjuryTo) != 0,
                Groggy = Word(RecordOffset.Groggy) != 0,
                Salary = Word(RecordOffset.Salary),
                MatchBonus = Word(RecordOffset.MatchBonus),
                GoalBonus = Word(RecordOffset.GoalBonus),
                ReleaseClause = Word(RecordOffset.ReleaseClause),
                ContractEndYear = Word(RecordOffset.ContractEnd),
                ContractFlags = Word(RecordOffset.ContractFlags),
            };
        }

        /// <summary>Overlays the working copy's values, which are the ones the game currently uses.</summary>
        internal static void ApplyCompact(Player player, byte[] compact, int baseYear)
        {
            int Byte(int o) => compact[o];
            int Word(int o) => BitConverter.ToUInt16(compact, o);

            player.Strength = Byte(CompactOffset.Strength);
            player.Position = Byte(CompactOffset.Position);
            player.SidePosition1 = Byte(CompactOffset.SidePosition1);
            player.SidePosition2 = Byte(CompactOffset.SidePosition2);
            player.ClubCountry = Byte(CompactOffset.ClubCountry);
            player.ClubNumber = Byte(CompactOffset.ClubNumber);
            player.Talent = Byte(CompactOffset.Talent);
            player.Skills = Word(CompactOffset.Skills);
            player.Skills2 = Word(CompactOffset.Skills2);
            player.Flags = BitConverter.ToUInt32(compact, CompactOffset.Flags);
            player.Traits = Word(CompactOffset.Traits);
            player.Character = Word(CompactOffset.Character);
            player.Health = Word(CompactOffset.Health);
            player.Condition = Word(CompactOffset.Condition);
            player.Freshness = Word(CompactOffset.Freshness);
            player.Nationality = Byte(CompactOffset.Nationality);
            player.ShirtNumber = Byte(CompactOffset.ShirtNumber);
            player.InjuryDays = BitConverter.ToInt16(compact, CompactOffset.InjuryDays);
            player.InjuryType = Byte(CompactOffset.InjuryType);
            player.Groggy = Byte(CompactOffset.Groggy) != 0;
            player.Salary = Word(CompactOffset.Salary);
            player.MatchBonus = Word(CompactOffset.MatchBonus);
            player.GoalBonus = Word(CompactOffset.GoalBonus);
            player.ReleaseClause = Word(CompactOffset.ReleaseClause);
            player.ContractEndYear = (sbyte)Byte(CompactOffset.ContractEnd) + baseYear;
            player.ContractFlags = Word(CompactOffset.ContractFlags);
            player.NationalTeam = Byte(CompactOffset.NationalTeam);
            player.Morale = Byte(CompactOffset.Morale);
        }

        /// <summary>
        /// The writes that turn <paramref name="original"/> into <paramref name="edited"/>: every
        /// changed field goes to the record and, if the game has a working copy, to that copy too
        /// (which is what the game shows and what it writes back on save).
        /// <paramref name="footByte"/> is the record's current foot/status byte, of which only
        /// the changed bits are replaced; likewise <paramref name="appearanceByte"/> for skin colour and beard.
        /// </summary>
        internal static List<MemoryWrite> Changes(Player original, Player edited, int baseYear, int footByte, int appearanceByte = 0)
        {
            var writes = new List<MemoryWrite>();
            uint record = A3Addresses.PlayerArray + (uint)(edited.Index * A3Addresses.PlayerSize);
            uint compact = edited.HasWorkingCopy ? edited.CacheEntry + A3Addresses.CacheEntryCompact : 0;

            void Write(int recordOffset, int compactOffset, int value, int size)
            {
                byte[] data = size == 1 ? new[] { (byte)value } : BitConverter.GetBytes((ushort)value);
                writes.Add(new MemoryWrite(record + (uint)recordOffset, data));
                if (compact != 0 && compactOffset >= 0)
                    writes.Add(new MemoryWrite(compact + (uint)compactOffset, (byte[])data.Clone()));
            }

            foreach (Field field in MirroredFields)
            {
                int value = field.Get(edited);
                if (value != field.Get(original))
                    Write(field.Record, field.Compact, value, field.Size);
            }

            if (edited.ContractEndYear != original.ContractEndYear)
            {
                writes.Add(new MemoryWrite(record + RecordOffset.ContractEnd, BitConverter.GetBytes((ushort)edited.ContractEndYear)));
                if (compact != 0)
                    writes.Add(new MemoryWrite(compact + CompactOffset.ContractEnd, new[] { (byte)(edited.ContractEndYear - baseYear) }));
            }

            // The game shows a loaded player's birth date from the working copy (a dword copy of
            // the record's day, month, year); the copy isn't written back, so both are set.
            if (!edited.BirthDate.Equals(original.BirthDate))
            {
                byte[] date = new byte[4];
                date[0] = (byte)edited.BirthDate.Day;
                date[1] = (byte)edited.BirthDate.Month;
                BitConverter.GetBytes((ushort)edited.BirthDate.Year).CopyTo(date, 2);
                writes.Add(new MemoryWrite(record + RecordOffset.BirthDay, date));
                if (compact != 0)
                    writes.Add(new MemoryWrite(compact + CompactOffset.BirthDate, (byte[])date.Clone()));
            }

            // Reputation (bits 0-1), foot (bits 2-3) and the status flags only exist in the record; the
            // bits that weren't changed stay as the game holds them.
            int status = footByte;
            if (edited.Foot != original.Foot)
                status = (status & ~0x0C) | ((edited.Foot & 3) << 2);
            if (edited.Reputation != original.Reputation)
                status = (status & ~3) | (edited.Reputation & 3);
            status = SetFlag(status, StatusFlag.HomegrownForeigner, original.HomegrownForeigner, edited.HomegrownForeigner);
            status = SetFlag(status, StatusFlag.NationalPlayer, original.NationalPlayer, edited.NationalPlayer);
            status = SetFlag(status, StatusFlag.CaptainRetired, original.CaptainRetired, edited.CaptainRetired);
            if (edited.Foot != original.Foot || edited.Reputation != original.Reputation || edited.HomegrownForeigner != original.HomegrownForeigner ||
                edited.NationalPlayer != original.NationalPlayer || edited.CaptainRetired != original.CaptainRetired)
                writes.Add(new MemoryWrite(record + RecordOffset.FootAndReputation, new[] { (byte)status }));

            // The crowd flags share their dword with other game state; the game keeps it in both places.
            if (edited.Flags != original.Flags)
            {
                byte[] flags = BitConverter.GetBytes(edited.Flags);
                writes.Add(new MemoryWrite(record + RecordOffset.Flags, flags));
                if (compact != 0)
                    writes.Add(new MemoryWrite(compact + CompactOffset.Flags, (byte[])flags.Clone()));
            }

            // The form value only exists in the record; the game reads it there for loaded players too.
            if (edited.FormBase != original.FormBase)
                writes.Add(new MemoryWrite(record + RecordOffset.FormBase, new[] { (byte)(sbyte)edited.FormBase }));

            // Skin colour (bits 0-2) and beard (bits 4-7) only exist in the record; bit 3 stays.
            if (edited.SkinColor != original.SkinColor || edited.Beard != original.Beard)
            {
                int looks = appearanceByte;
                if (edited.SkinColor != original.SkinColor)
                    looks = (looks & ~7) | (edited.SkinColor & 7);
                if (edited.Beard != original.Beard)
                    looks = (looks & 0x0F) | ((edited.Beard & 0x0F) << 4);
                writes.Add(new MemoryWrite(record + RecordOffset.Appearance, new[] { (byte)looks }));
            }

            // Hair colour and type make up the whole hair byte, which only exists in the record.
            if (edited.HairColor != original.HairColor || edited.HairType != original.HairType)
                writes.Add(new MemoryWrite(record + RecordOffset.Hair, new[] { (byte)((edited.HairColor & 0x0F) | ((edited.HairType & 0x0F) << 4)) }));

            // Private life only exists in the record; the game reads it there for loaded players too.
            if (edited.Relationship != original.Relationship)
                writes.Add(new MemoryWrite(record + RecordOffset.Relationship, new[] { (byte)edited.Relationship }));
            if (edited.Hobby != original.Hobby)
                writes.Add(new MemoryWrite(record + RecordOffset.Hobby, new[] { (byte)edited.Hobby }));

            if (original.IsInjured && !edited.IsInjured || original.Groggy && !edited.Groggy)
                writes.AddRange(HealWrites(record, compact));

            return writes;
        }

        private static int SetFlag(int value, int flag, bool was, bool now) =>
            was == now ? value : now ? value | flag : value & ~flag;

        // Healthy in the game's terms (FUN_00637177): no injury days, not groggy, injury dates and
        // flag of the record cleared.
        private static IEnumerable<MemoryWrite> HealWrites(uint record, uint compact)
        {
            byte[] zeroWord = { 0, 0 };
            yield return new MemoryWrite(record + RecordOffset.InjuryStart, zeroWord);
            yield return new MemoryWrite(record + RecordOffset.InjuryFrom, zeroWord);
            yield return new MemoryWrite(record + RecordOffset.InjuryTo, zeroWord);
            yield return new MemoryWrite(record + RecordOffset.Groggy, zeroWord);
            yield return new MemoryWrite(record + RecordOffset.InjuryFlag, new byte[] { 0 });
            if (compact != 0)
            {
                yield return new MemoryWrite(compact + CompactOffset.InjuryStart, zeroWord);
                yield return new MemoryWrite(compact + CompactOffset.InjuryDays, zeroWord);
                yield return new MemoryWrite(compact + CompactOffset.Groggy, new byte[] { 0 });
            }
        }
    }
}
