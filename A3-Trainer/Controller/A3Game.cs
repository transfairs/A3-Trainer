using A3_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace A3_Trainer.Controller
{
    /// <summary>Reads and writes Anstoss 3's data through <see cref="IGameMemory"/>.</summary>
    internal sealed class A3Game
    {
        private static readonly Encoding Text = Encoding.GetEncoding(1252);

        // Guards the cache walk against a tree caught mid-update by the game.
        private const int MaxCacheNodes = 5000;

        private readonly IGameMemory memory;

        /// <summary>Creates the game access on top of <paramref name="memory"/>.</summary>
        internal A3Game(IGameMemory memory)
        {
            this.memory = memory;
        }

        /// <summary>
        /// Whether the attached process is the supported build: the first player and club slots
        /// must carry their classes' vtables at the expected fixed addresses.
        /// </summary>
        internal bool IsSupportedBuild() =>
            this.ReadUInt32(A3Addresses.PlayerArray) == A3Addresses.PlayerVtable &&
            this.ReadUInt32(A3Addresses.ClubArray) == A3Addresses.ClubVtable;

        /// <summary>Year the game stores contract ends relative to.</summary>
        internal int BaseYear() => this.ReadUInt16(A3Addresses.BaseYear);

        /// <summary>The game's current date, or null if the calendar isn't set up (no game loaded).</summary>
        internal GameDate? Today()
        {
            int day = this.ReadUInt16(A3Addresses.CalendarBase) + (short)this.ReadUInt16(A3Addresses.DayCounter);
            byte[] entry = this.memory.Read(A3Addresses.Calendar + (uint)(day * 4), 4);
            if (entry == null)
                return null;
            int year = BitConverter.ToInt16(entry, 0);
            int dayOfYear = BitConverter.ToInt16(entry, 2);
            if (year < 1900 || dayOfYear < 0 || dayOfYear > 365)
                return null;
            return GameDate.FromDayOfYear(year, dayOfYear);
        }

        /// <summary>The club of human manager 1, or null if no career is loaded.</summary>
        internal ClubKey? OwnClub()
        {
            ClubKey key = new ClubKey(this.ReadUInt16(A3Addresses.ManagerClubCountry), this.ReadUInt16(A3Addresses.ManagerClubNumber));
            return this.ClubAddress(key).HasValue ? key : (ClubKey?)null;
        }

        /// <summary>Address of a club's CDatenMannschaft, or null if the key doesn't name one.</summary>
        internal uint? ClubAddress(ClubKey key)
        {
            if (key.Country < 0 || key.Country > 255 || key.Number < 0 || key.Number > 255)
                return null;
            int index = this.ReadUInt16(A3Addresses.ClubIndexTable + (uint)(key.Country * 2)) + key.Number;
            if (index < 0 || index >= A3Addresses.ClubCount)
                return null;
            uint address = A3Addresses.ClubArray + (uint)(index * A3Addresses.ClubSize);
            if (this.ReadUInt32(address) != A3Addresses.ClubVtable || this.ReadString(address + A3Addresses.ClubName, 0x14).Length == 0)
                return null;
            return address;
        }

        /// <summary>The club's name, or "" if unknown.</summary>
        internal string ClubName(ClubKey key)
        {
            uint? address = this.ClubAddress(key);
            return address.HasValue ? this.ReadString(address.Value + A3Addresses.ClubName, 0x14) : "";
        }

        /// <summary>Player number -> address of the game's working copy, for every player it has one of.</summary>
        internal Dictionary<int, uint> ReadPlayerCache()
        {
            var entries = new Dictionary<int, uint>();
            var visited = new HashSet<uint>();
            var pending = new Stack<uint>();
            uint root = this.ReadUInt32(A3Addresses.PlayerCacheRoot);
            if (root != 0)
                pending.Push(root);

            while (pending.Count > 0 && visited.Count < MaxCacheNodes)
            {
                uint node = pending.Pop();
                if (!visited.Add(node))
                    continue;
                byte[] data = this.memory.Read(node, 0x10);
                if (data == null)
                    continue;

                uint left = BitConverter.ToUInt32(data, A3Addresses.CacheNodeLeft);
                uint right = BitConverter.ToUInt32(data, A3Addresses.CacheNodeRight);
                uint entry = BitConverter.ToUInt32(data, A3Addresses.CacheNodeEntry);
                if (left != 0) pending.Push(left);
                if (right != 0) pending.Push(right);

                byte[] index = entry != 0 ? this.memory.Read(entry + A3Addresses.CacheEntryIndex, 2) : null;
                if (index == null)
                    continue;
                int player = BitConverter.ToInt16(index, 0);
                if (player >= 0 && player < A3Addresses.PlayerSlots)
                    entries[player] = entry;
            }
            return entries;
        }

        /// <summary>All players of the given club, sorted by position and strength.</summary>
        internal List<Player> ReadPlayers(ClubKey club)
        {
            var players = new List<Player>();
            byte[] all = this.memory.Read(A3Addresses.PlayerArray, A3Addresses.PlayerSlots * A3Addresses.PlayerSize);
            if (all == null)
                return players;

            Dictionary<int, uint> cache = this.ReadPlayerCache();
            int baseYear = this.BaseYear();

            for (int index = 0; index < A3Addresses.PlayerSlots; index++)
            {
                int offset = index * A3Addresses.PlayerSize;
                if (!PlayerCodec.IsUsed(all, offset))
                    continue;

                Player player = PlayerCodec.FromRecord(all, offset, index);
                if (cache.TryGetValue(index, out uint entry))
                {
                    byte[] compact = this.memory.Read(entry + A3Addresses.CacheEntryCompact, A3Addresses.CompactSize);
                    if (compact != null)
                    {
                        PlayerCodec.ApplyCompact(player, compact, baseYear);
                        player.CacheEntry = entry;
                    }
                }

                if (player.ClubCountry != club.Country || player.ClubNumber != club.Number)
                    continue;

                player.FirstName = this.ReadName(BitConverter.ToUInt16(all, offset + RecordOffset.FirstName));
                player.LastName = this.ReadName(BitConverter.ToUInt16(all, offset + RecordOffset.LastName));
                player.ArtistName = this.ReadName(BitConverter.ToUInt16(all, offset + RecordOffset.ArtistName));
                int nameCountry = all[offset + RecordOffset.NameCountry];
                if ((player.Relationship & Tables.RelationshipMask) != 0)
                    player.PartnerName = this.ReadFirstName(nameCountry, all[offset + RecordOffset.PartnerName]);
                int children = Math.Min(Math.Max((int)BitConverter.ToInt16(all, offset + RecordOffset.ChildCount), 0), A3Addresses.MaxChildren);
                player.ChildNames = Enumerable.Range(0, children)
                    .Select(i => this.ReadFirstName(nameCountry, all[offset + RecordOffset.ChildNames + i])).ToArray();
                players.Add(player);
            }

            return players.OrderBy(p => p.Position).ThenByDescending(p => p.Strength).ThenBy(p => p.LastName).ToList();
        }

        /// <summary>
        /// The club whose players the game has loaded besides the own squad - the next opponent -
        /// or null if there is none right now.
        /// </summary>
        internal ClubKey? NextOpponent(ClubKey own)
        {
            byte[] all = this.memory.Read(A3Addresses.PlayerArray, A3Addresses.PlayerSlots * A3Addresses.PlayerSize);
            if (all == null)
                return null;

            var counts = new Dictionary<ClubKey, int>();
            foreach (int index in this.ReadPlayerCache().Keys)
            {
                int offset = index * A3Addresses.PlayerSize;
                ClubKey key = new ClubKey(all[offset + RecordOffset.ClubCountry], all[offset + RecordOffset.ClubNumber]);
                if (key.Equals(own))
                    continue;
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            if (counts.Count == 0)
                return null;
            return counts.OrderByDescending(c => c.Value).First().Key;
        }

        /// <summary>A name from the game's name table, or "" if it can't be read.</summary>
        internal string ReadName(int nameIndex)
        {
            if (nameIndex <= 0 || nameIndex >= A3Addresses.NameTableSlots)
                return "";
            uint pointer = this.ReadUInt32(A3Addresses.NameTable + (uint)(nameIndex * 4));
            return pointer == 0 ? "" : this.ReadString(pointer, 32);
        }

        /// <summary>A first name from the list the game picks partners and children from, or "" if it can't be read.</summary>
        internal string ReadFirstName(int nameCountry, int number)
        {
            if (number < 0 || number >= A3Addresses.FirstNameListSize)
                return "";
            byte[] index = this.memory.Read(A3Addresses.FirstNameLists + (uint)((nameCountry * A3Addresses.FirstNameListSize + number) * 2), 2);
            return index == null ? "" : this.ReadName(BitConverter.ToInt16(index, 0));
        }

        /// <summary>The country names by country number (the nationality values), up to the first unreadable one.</summary>
        internal List<string> ReadCountryNames()
        {
            var names = new List<string>();
            byte[] pointers = this.memory.Read(A3Addresses.CountryNames, A3Addresses.CountrySlots * 4);
            if (pointers == null)
                return names;

            for (int i = 0; i < A3Addresses.CountrySlots; i++)
            {
                uint pointer = BitConverter.ToUInt32(pointers, i * 4);
                string name = pointer == 0 ? "" : this.ReadString(pointer, 32);
                if (name.Length == 0 && i > 0)
                    break;
                names.Add(name);
            }
            return names;
        }

        /// <summary>Writes every change between the two states of a player (see PlayerCodec.Changes).</summary>
        internal void SavePlayer(Player original, Player edited)
        {
            uint record = A3Addresses.PlayerArray + (uint)(edited.Index * A3Addresses.PlayerSize);
            byte[] foot = this.memory.Read(record + RecordOffset.FootAndReputation, 1);
            byte[] appearance = this.memory.Read(record + RecordOffset.Appearance, 1);
            foreach (MemoryWrite write in PlayerCodec.Changes(original, edited, this.BaseYear(), foot != null ? foot[0] : 0, appearance != null ? appearance[0] : 0))
                this.memory.Write(write.Address, write.Data);

            // Names are indices into the shared name table and only exist in the record. A first or
            // last name can't be empty; an empty artist name removes it.
            string first = edited.FirstName.Trim(), last = edited.LastName.Trim(), artist = edited.ArtistName.Trim();
            if (first.Length > 0 && first != original.FirstName)
                this.WriteNameIndex(record + RecordOffset.FirstName, this.NameIndex(first));
            if (last.Length > 0 && last != original.LastName)
                this.WriteNameIndex(record + RecordOffset.LastName, this.NameIndex(last));
            if (artist != original.ArtistName)
            {
                // The game shows the artist name only while this flag is set (FUN_006FEADD).
                this.WriteNameIndex(record + RecordOffset.ArtistName, artist.Length > 0 ? this.NameIndex(artist) : 0);
                uint flags = this.ReadUInt32(record + RecordOffset.MoreFlags);
                flags = artist.Length > 0 ? flags | MoreFlag.HasArtistName : flags & ~MoreFlag.HasArtistName;
                this.memory.Write(record + RecordOffset.MoreFlags, BitConverter.GetBytes(flags));
            }
        }

        private void WriteNameIndex(uint address, int index) => this.memory.Write(address, BitConverter.GetBytes((ushort)index));

        /// <summary>
        /// The name table index of <paramref name="name"/>: the first entry with exactly that text, or a
        /// new one appended to the name pool as the game does when it imports names (FUN_0068F8A0).
        /// </summary>
        internal int NameIndex(string name)
        {
            uint data = A3Addresses.NamePool + A3Addresses.NamePoolData;
            uint end = A3Addresses.NamePool + A3Addresses.NamePoolCount;
            uint write = this.ReadUInt32(A3Addresses.NamePool + A3Addresses.NamePoolWrite);
            int count = this.ReadUInt16(A3Addresses.NamePool + A3Addresses.NamePoolCount);
            if (write < data || write > end || count <= 0 || count > A3Addresses.NameTableSlots)
                throw new InvalidOperationException("Namenstabelle nicht lesbar");

            byte[] pool = write > data ? this.memory.Read(data, (int)(write - data)) : new byte[0];
            if (pool == null)
                throw new InvalidOperationException("Namenstabelle nicht lesbar");
            string[] names = Text.GetString(pool).Split('\0');
            for (int i = 1; i < count && i < names.Length; i++)
                if (names[i] == name)
                    return i;

            byte[] bytes = Text.GetBytes(name + "\0");
            if (write + bytes.Length > end || count >= A3Addresses.NameTableSlots)
                throw new InvalidOperationException("Namenstabelle voll");
            // String and table entry first, the count last: until then the game doesn't see the new name.
            this.memory.Write(write, bytes);
            this.memory.Write(A3Addresses.NameTable + (uint)(count * 4), BitConverter.GetBytes(write));
            this.memory.Write(A3Addresses.NamePool + A3Addresses.NamePoolWrite, BitConverter.GetBytes(write + (uint)bytes.Length));
            byte[] newCount = BitConverter.GetBytes((ushort)(count + 1));
            this.memory.Write(A3Addresses.NamePool + A3Addresses.NamePoolCount, newCount);
            this.memory.Write(A3Addresses.NameCount, newCount);
            return count;
        }

        /// <summary>Club and manager 1 data, or null if no career is loaded.</summary>
        internal ClubInfo ReadClubInfo()
        {
            ClubKey? own = this.OwnClub();
            if (!own.HasValue)
                return null;
            uint club = this.ClubAddress(own.Value).Value;
            uint copy = this.ClubCopy(own.Value);
            byte[] trust = this.memory.Read(A3Addresses.ManagerBoardTrust, 6) ?? new byte[6];

            return new ClubInfo
            {
                Key = own.Value,
                Name = this.ReadString(club + A3Addresses.ClubName, 0x14),
                ShortName = this.ReadString(club + A3Addresses.ClubShortName, 4),
                City = this.ReadString(club + A3Addresses.ClubCity, 0x14),
                SeasonStartMoney = (int)this.ReadUInt32(A3Addresses.SeasonStartMoney),
                ManagerFirstName = this.ReadString(A3Addresses.ManagerFirstName, 10),
                ManagerLastName = this.ReadString(A3Addresses.ManagerLastName, 0x10),
                ManagerSalary = (int)this.ReadUInt32(A3Addresses.ManagerSalary),
                ManagerPrivateMoney = (int)this.ReadUInt32(A3Addresses.ManagerPrivateMoney),
                // The loaded club's working copy is what the game uses; both hold the same otherwise.
                Image = this.ReadByte(copy != 0 ? copy + A3Addresses.ClubCopyImage : club + A3Addresses.ClubImage),
                Trouble = this.ReadByte(copy != 0 ? copy + A3Addresses.ClubCopyTrouble : club + A3Addresses.ClubTrouble),
                MediaPopularity = this.ReadByte(A3Addresses.ManagerMediaPopularity),
                PublicPopularity = this.ReadByte(A3Addresses.ManagerPublicPopularity),
                FanPopularity = this.ReadByte(A3Addresses.ManagerFanPopularity),
                FanWeight = this.FanWeight(),
                BoardTrustBase = BitConverter.ToInt16(trust, 0),
                BoardTrustBonus = trust[2] + trust[3] + (sbyte)trust[4] + (sbyte)trust[5],
            };
        }

        /// <summary>Writes the editable club/manager values that changed.</summary>
        internal void SaveClubInfo(ClubInfo original, ClubInfo edited)
        {
            if (edited.SeasonStartMoney != original.SeasonStartMoney)
                this.memory.Write(A3Addresses.SeasonStartMoney, BitConverter.GetBytes(edited.SeasonStartMoney));
            if (edited.ManagerSalary != original.ManagerSalary)
                this.memory.Write(A3Addresses.ManagerSalary, BitConverter.GetBytes(edited.ManagerSalary));
            if (edited.ManagerPrivateMoney != original.ManagerPrivateMoney)
                this.memory.Write(A3Addresses.ManagerPrivateMoney, BitConverter.GetBytes(edited.ManagerPrivateMoney));
            WriteByte(A3Addresses.ManagerMediaPopularity, original.MediaPopularity, edited.MediaPopularity);
            WriteByte(A3Addresses.ManagerPublicPopularity, original.PublicPopularity, edited.PublicPopularity);
            WriteByte(A3Addresses.ManagerFanPopularity, original.FanPopularity, edited.FanPopularity);
            if (edited.BoardTrustBase != original.BoardTrustBase)
                this.memory.Write(A3Addresses.ManagerBoardTrust, BitConverter.GetBytes((short)edited.BoardTrustBase));

            // The club's values live in its record and, while the game has it loaded, in the working copy.
            if (edited.Image == original.Image && edited.Trouble == original.Trouble)
                return;
            uint? club = this.ClubAddress(edited.Key);
            uint copy = this.ClubCopy(edited.Key);
            if (club.HasValue)
            {
                WriteByte(club.Value + A3Addresses.ClubImage, original.Image, edited.Image);
                WriteByte(club.Value + A3Addresses.ClubTrouble, original.Trouble, edited.Trouble);
            }
            if (copy != 0)
            {
                WriteByte(copy + A3Addresses.ClubCopyImage, original.Image, edited.Image);
                WriteByte(copy + A3Addresses.ClubCopyTrouble, original.Trouble, edited.Trouble);
            }

            void WriteByte(uint address, int was, int now)
            {
                if (now != was)
                    this.memory.Write(address, new[] { (byte)now });
            }
        }

        // As FUN_006A61E5 and FUN_006A5A7D: the image level plus its flags picks the factor.
        private int FanWeight()
        {
            int level = this.ReadByte(A3Addresses.ManagerImageLevel);
            int flags = this.ReadByte(A3Addresses.ManagerImageFlags);
            if (level < 12)
            {
                int[] bonus = { 0, -1, 3, 3, 1, 2, 1, 2 };   // flags 0x01 .. 0x80
                for (int bit = 1; bit < 8; bit++)
                    if ((flags & (1 << bit)) != 0)
                        level += bonus[bit];
                level = Math.Min(level, 12);
            }
            else if ((flags & 2) != 0)
                level--;
            level = Math.Min(Math.Max(level, 0), 15);
            byte[] factor = this.memory.Read(A3Addresses.ManagerFanFactors + (uint)(level * A3Addresses.ManagerFanFactorStride), 2);
            return (factor == null ? 0 : BitConverter.ToInt16(factor, 0)) * 4 + 80;
        }

        /// <summary>The working copy the game holds of a loaded club (own club, next opponent), or 0 if it has none.</summary>
        internal uint ClubCopy(ClubKey key)
        {
            byte[] entries = this.memory.Read(A3Addresses.ClubCache, A3Addresses.ClubCacheSlots * A3Addresses.ClubCacheEntrySize);
            if (entries == null)
                return 0;
            for (int i = 0; i < A3Addresses.ClubCacheSlots; i++)
            {
                int entry = i * A3Addresses.ClubCacheEntrySize;
                if (entries[entry] != 0 && entries[entry + 1] == key.Country && entries[entry + 2] == key.Number)
                    return BitConverter.ToUInt32(entries, entry + A3Addresses.ClubCacheObject);
            }
            return 0;
        }

        private int ReadByte(uint address)
        {
            byte[] data = this.memory.Read(address, 1);
            return data == null ? 0 : data[0];
        }

        private uint ReadUInt32(uint address)
        {
            byte[] data = this.memory.Read(address, 4);
            return data == null ? 0 : BitConverter.ToUInt32(data, 0);
        }

        private int ReadUInt16(uint address)
        {
            byte[] data = this.memory.Read(address, 2);
            return data == null ? 0 : BitConverter.ToUInt16(data, 0);
        }

        private string ReadString(uint address, int length)
        {
            byte[] data = this.memory.Read(address, length);
            if (data == null)
                return "";
            int end = Array.IndexOf(data, (byte)0);
            return Text.GetString(data, 0, end < 0 ? data.Length : end).Trim();
        }
    }
}
