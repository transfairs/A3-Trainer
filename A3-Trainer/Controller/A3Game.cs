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
            if (nameIndex <= 0 || nameIndex >= A3Addresses.NameTableCount)
                return "";
            uint pointer = this.ReadUInt32(A3Addresses.NameTable + (uint)(nameIndex * 4));
            return pointer == 0 ? "" : this.ReadString(pointer, 32);
        }

        /// <summary>Writes every change between the two states of a player (see PlayerCodec.Changes).</summary>
        internal void SavePlayer(Player original, Player edited)
        {
            uint record = A3Addresses.PlayerArray + (uint)(edited.Index * A3Addresses.PlayerSize);
            byte[] foot = this.memory.Read(record + RecordOffset.FootAndReputation, 1);
            foreach (MemoryWrite write in PlayerCodec.Changes(original, edited, this.BaseYear(), foot != null ? foot[0] : 0))
                this.memory.Write(write.Address, write.Data);
        }

        /// <summary>Club and manager 1 data, or null if no career is loaded.</summary>
        internal ClubInfo ReadClubInfo()
        {
            ClubKey? own = this.OwnClub();
            if (!own.HasValue)
                return null;
            uint club = this.ClubAddress(own.Value).Value;

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
            };
        }

        /// <summary>Writes the editable club/manager values that changed.</summary>
        internal void SaveClubInfo(ClubInfo original, ClubInfo edited)
        {
            if (edited.SeasonStartMoney != original.SeasonStartMoney)
                this.memory.Write(A3Addresses.SeasonStartMoney, BitConverter.GetBytes(edited.SeasonStartMoney));
            if (edited.ManagerPrivateMoney != original.ManagerPrivateMoney)
                this.memory.Write(A3Addresses.ManagerPrivateMoney, BitConverter.GetBytes(edited.ManagerPrivateMoney));
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
