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

        /// <summary>The club of the manager whose turn it is, or null if no career is loaded.</summary>
        internal ClubKey? OwnClub() => this.OwnClub(this.ActiveManager());

        /// <summary>The club of human manager <paramref name="manager"/>, or null if he has none.</summary>
        internal ClubKey? OwnClub(int manager)
        {
            ClubKey key = this.ManagerClub(manager);
            // Country 0 means no club; it would name the first German club through the index table.
            return key.Country > 0 && this.ClubAddress(key).HasValue ? key : (ClubKey?)null;
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

        /// <summary>
        /// Every club that has players, in the game's club order (country by country). Taken from the
        /// player records, because the index table can't tell a country without clubs from one whose
        /// clubs start at number 0.
        /// </summary>
        internal List<ClubKey> ReadClubs()
        {
            var clubs = new Dictionary<ClubKey, int>();
            byte[] all = this.memory.Read(A3Addresses.PlayerArray, A3Addresses.PlayerSlots * A3Addresses.PlayerSize);
            if (all == null)
                return new List<ClubKey>();

            for (int index = 0; index < A3Addresses.PlayerSlots; index++)
            {
                int offset = index * A3Addresses.PlayerSize;
                if (!PlayerCodec.IsUsed(all, offset))
                    continue;
                var key = new ClubKey(all[offset + RecordOffset.ClubCountry], all[offset + RecordOffset.ClubNumber]);
                if (clubs.ContainsKey(key))
                    continue;
                uint? address = this.ClubAddress(key);
                if (address.HasValue)
                    clubs[key] = (int)address.Value;
            }
            return clubs.OrderBy(c => c.Value).Select(c => c.Key).ToList();
        }

        /// <summary>
        /// Sets condition and/or freshness (hundredths; null leaves it) of every player of the club whose
        /// value differs, in the working copy and the record. Returns how many players were changed.
        /// </summary>
        internal int SetTeamFitness(ClubKey club, int? condition, int? freshness)
        {
            int changed = 0;
            foreach (Player player in this.ReadPlayers(club))
            {
                Player edited = player.Clone();
                if (condition.HasValue)
                    edited.Condition = condition.Value;
                if (freshness.HasValue)
                    edited.Freshness = freshness.Value;
                if (edited.Condition == player.Condition && edited.Freshness == player.Freshness)
                    continue;
                this.SavePlayer(player, edited);
                changed++;
            }
            return changed;
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

        /// <summary>
        /// The 100 first names the game picks partners and children from for a name country (FUN_0067503A);
        /// empty strings where a name can't be read.
        /// </summary>
        internal string[] ReadFirstNameList(int nameCountry)
        {
            byte[] indices = this.memory.Read(A3Addresses.FirstNameLists + (uint)(nameCountry * A3Addresses.FirstNameListSize * 2), A3Addresses.FirstNameListSize * 2);
            return Enumerable.Range(0, A3Addresses.FirstNameListSize)
                .Select(i => indices == null ? "" : this.ReadName(BitConverter.ToInt16(indices, i * 2))).ToArray();
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
        internal int NameIndex(string name) => this.NameIndex(name, add: true);

        /// <summary>The first name table index with exactly this text; -1 if there is none and <paramref name="add"/> is false.</summary>
        private int NameIndex(string name, bool add)
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
            if (!add)
                return -1;

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

        /// <summary>Club and data of the manager whose turn it is, or null if no career is loaded.</summary>
        internal ClubInfo ReadClubInfo() => this.ReadClubInfo(this.ActiveManager());

        /// <summary>Club and data of human manager <paramref name="manager"/> (1-4), or null if he has no club.</summary>
        internal ClubInfo ReadClubInfo(int manager)
        {
            ClubKey? own = this.OwnClub(manager);
            if (!own.HasValue)
                return null;
            uint club = this.ClubAddress(own.Value).Value;
            uint copy = this.ClubCopy(own.Value);
            // The finance block of the loaded copy (what the game uses), else the record's, at the record's offsets.
            uint finances = copy != 0 ? copy - (uint)A3Addresses.ClubCopyFinanceShift : club;
            byte[] trust = this.memory.Read(this.ManagerField(manager, A3Addresses.ManagerBoardTrust), 6) ?? new byte[6];
            uint contract = this.ManagerField(manager, A3Addresses.ManagerSalary);

            return new ClubInfo
            {
                Manager = manager,
                Key = own.Value,
                Name = this.ReadString(club + A3Addresses.ClubName, 0x14),
                ShortName = this.ReadString(club + A3Addresses.ClubShortName, 4),
                City = this.ReadString(club + A3Addresses.ClubCity, 0x14),
                SeasonStartMoney = (int)this.ReadUInt32(finances + A3Addresses.ClubSeasonStartMoney),
                HasAdsContract = this.ReadByte(finances + A3Addresses.ClubAdsContract) != 0,
                Ads = (int)this.ReadUInt32(finances + A3Addresses.ClubAds),
                HasOutfitter = this.ReadByte(finances + A3Addresses.ClubOutfitterContract) != 0,
                Outfitter = (int)this.ReadUInt32(finances + A3Addresses.ClubOutfitter),
                ManagerFirstName = this.ReadString(this.ManagerField(manager, A3Addresses.ManagerFirstName), 10),
                ManagerLastName = this.ReadString(this.ManagerField(manager, A3Addresses.ManagerLastName), 0x10),
                ManagerSalary = (int)this.ReadUInt32(contract),
                PointBonus = (int)this.ReadUInt32(contract + ContractOffset.PointBonus),
                CupBonus = (int)this.ReadUInt32(contract + ContractOffset.CupBonus),
                TitleBonus = (int)this.ReadUInt32(contract + ContractOffset.TitleBonus),
                GoalBonus = (int)this.ReadUInt32(contract + ContractOffset.GoalBonus),
                SeasonGoal = this.ReadByte(copy != 0 ? copy + A3Addresses.ClubCopySeasonGoal : club + A3Addresses.ClubSeasonGoal),
                ContractYears = this.ReadUInt16(contract + ContractOffset.Years),
                ExitClause = this.ReadByte(contract + ContractOffset.ExitClause) != 0,
                ManagerPrivateMoney = (int)this.ReadUInt32(this.ManagerField(manager, A3Addresses.ManagerPrivateMoney)),
                // The loaded club's working copy is what the game uses; both hold the same otherwise.
                Image = this.ReadByte(copy != 0 ? copy + A3Addresses.ClubCopyImage : club + A3Addresses.ClubImage),
                Trouble = this.ReadByte(copy != 0 ? copy + A3Addresses.ClubCopyTrouble : club + A3Addresses.ClubTrouble),
                MediaPopularity = this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerMediaPopularity)),
                PublicPopularity = this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerPublicPopularity)),
                FanPopularity = this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerFanPopularity)),
                FanWeight = this.FanWeight(manager),
                BoardTrustBase = BitConverter.ToInt16(trust, 0),
                BoardTrustBonus = trust[2] + trust[3] + (sbyte)trust[4] + (sbyte)trust[5],
            };
        }

        /// <summary>Writes the editable club/manager values that changed.</summary>
        internal void SaveClubInfo(ClubInfo original, ClubInfo edited)
        {
            int manager = edited.Manager;
            if (edited.ManagerFirstName != original.ManagerFirstName || edited.ManagerLastName != original.ManagerLastName)
                this.RenameManager(original, edited);
            WriteManager(A3Addresses.ManagerSalary + ContractOffset.Salary, original.ManagerSalary, edited.ManagerSalary, 4);
            WriteManager(A3Addresses.ManagerSalary + ContractOffset.PointBonus, original.PointBonus, edited.PointBonus, 4);
            WriteManager(A3Addresses.ManagerSalary + ContractOffset.CupBonus, original.CupBonus, edited.CupBonus, 4);
            WriteManager(A3Addresses.ManagerSalary + ContractOffset.TitleBonus, original.TitleBonus, edited.TitleBonus, 4);
            WriteManager(A3Addresses.ManagerSalary + ContractOffset.GoalBonus, original.GoalBonus, edited.GoalBonus, 4);
            WriteManager(A3Addresses.ManagerSalary + ContractOffset.Years, original.ContractYears, edited.ContractYears, 2);
            WriteManager(A3Addresses.ManagerSalary + ContractOffset.ExitClause, original.ExitClause ? 1 : 0, edited.ExitClause ? 1 : 0, 1);
            WriteManager(A3Addresses.ManagerPrivateMoney, original.ManagerPrivateMoney, edited.ManagerPrivateMoney, 4);
            WriteManager(A3Addresses.ManagerMediaPopularity, original.MediaPopularity, edited.MediaPopularity, 1);
            WriteManager(A3Addresses.ManagerPublicPopularity, original.PublicPopularity, edited.PublicPopularity, 1);
            WriteManager(A3Addresses.ManagerFanPopularity, original.FanPopularity, edited.FanPopularity, 1);
            WriteManager(A3Addresses.ManagerBoardTrust, original.BoardTrustBase, edited.BoardTrustBase, 2);

            // The club's values live in its record and, while the game has it loaded, in the working copy.
            if (edited.Image == original.Image && edited.Trouble == original.Trouble && edited.SeasonStartMoney == original.SeasonStartMoney && edited.SeasonGoal == original.SeasonGoal &&
                edited.Ads == original.Ads && edited.Outfitter == original.Outfitter &&
                edited.Name == original.Name && edited.ShortName == original.ShortName && edited.City == original.City)
                return;
            uint? club = this.ClubAddress(edited.Key);
            uint copy = this.ClubCopy(edited.Key);
            if (club.HasValue)
            {
                WriteByte(club.Value + A3Addresses.ClubImage, original.Image, edited.Image);
                WriteByte(club.Value + A3Addresses.ClubTrouble, original.Trouble, edited.Trouble);
                WriteText(club.Value + A3Addresses.ClubName, original.Name, edited.Name, A3Addresses.ClubNameSize);
                WriteText(club.Value + A3Addresses.ClubShortName, original.ShortName, edited.ShortName, A3Addresses.ClubShortNameSize);
                WriteText(club.Value + A3Addresses.ClubCity, original.City, edited.City, A3Addresses.ClubNameSize);
                WriteFinances(club.Value);
                WriteByte(club.Value + A3Addresses.ClubSeasonGoal, original.SeasonGoal, edited.SeasonGoal);
            }
            if (copy != 0)
            {
                WriteByte(copy + A3Addresses.ClubCopyImage, original.Image, edited.Image);
                WriteByte(copy + A3Addresses.ClubCopyTrouble, original.Trouble, edited.Trouble);
                WriteText(copy + A3Addresses.ClubCopyName, original.Name, edited.Name, A3Addresses.ClubNameSize);
                WriteFinances(copy - (uint)A3Addresses.ClubCopyFinanceShift);
                WriteByte(copy + A3Addresses.ClubCopySeasonGoal, original.SeasonGoal, edited.SeasonGoal);
            }

            void WriteText(uint address, string was, string now, int size)
            {
                if (now != was)
                    this.memory.Write(address, Field(now, size));
            }

            void WriteByte(uint address, int was, int now)
            {
                if (now != was)
                    this.memory.Write(address, new[] { (byte)now });
            }

            // The finance block, addressed with the record's offsets from the record or the copy shifted to match.
            void WriteFinances(uint finances)
            {
                WriteDword(finances + A3Addresses.ClubSeasonStartMoney, original.SeasonStartMoney, edited.SeasonStartMoney);
                WriteDword(finances + A3Addresses.ClubAds, original.Ads, edited.Ads);
                WriteDword(finances + A3Addresses.ClubOutfitter, original.Outfitter, edited.Outfitter);
            }

            void WriteDword(uint address, int was, int now)
            {
                if (now != was)
                    this.memory.Write(address, BitConverter.GetBytes(now));
            }

            void WriteManager(uint field, int was, int now, int size)
            {
                if (now != was)
                    this.WriteManagerField(manager, field, size == 4 ? BitConverter.GetBytes(now) : size == 2 ? BitConverter.GetBytes((ushort)now) : new[] { (byte)now });
            }
        }

        // As FUN_006A61E5 and FUN_006A5A7D: the image level plus its flags picks the factor.
        private int FanWeight(int manager)
        {
            int level = this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerImageLevel));
            int flags = this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerImageFlags));
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
            byte[] factor = this.memory.Read(this.ManagerField(manager, A3Addresses.ManagerFanFactors + (uint)(level * A3Addresses.ManagerFanFactorStride)), 2);
            return (factor == null ? 0 : BitConverter.ToInt16(factor, 0)) * 4 + 80;
        }

        // The manager's name is text in the manager object. When the career started the game also entered it
        // in the name table for the trainer list; the one trainer entry with the old names follows the change.
        private void RenameManager(ClubInfo original, ClubInfo edited)
        {
            int oldFirst = this.NameIndex(original.ManagerFirstName, add: false);
            int oldLast = this.NameIndex(original.ManagerLastName, add: false);
            byte[] trainers = this.memory.Read(A3Addresses.TrainerList, A3Addresses.TrainerSlots * A3Addresses.TrainerSize);
            int[] matches = trainers == null || oldFirst <= 0 || oldLast <= 0 ? new int[0] : Enumerable.Range(1, A3Addresses.TrainerSlots - 1)
                .Where(t => BitConverter.ToInt16(trainers, t * A3Addresses.TrainerSize) == oldFirst &&
                            BitConverter.ToInt16(trainers, t * A3Addresses.TrainerSize + 2) == oldLast).ToArray();

            this.WriteManagerField(edited.Manager, A3Addresses.ManagerFirstName, Field(edited.ManagerFirstName, A3Addresses.ManagerFirstNameSize));
            this.WriteManagerField(edited.Manager, A3Addresses.ManagerLastName, Field(edited.ManagerLastName, A3Addresses.ManagerLastNameSize));
            if (matches.Length == 1)
            {
                uint entry = A3Addresses.TrainerList + (uint)(matches[0] * A3Addresses.TrainerSize);
                this.memory.Write(entry, BitConverter.GetBytes((ushort)this.NameIndex(edited.ManagerFirstName)));
                this.memory.Write(entry + 2, BitConverter.GetBytes((ushort)this.NameIndex(edited.ManagerLastName)));
            }
        }

        /// <summary>Numbers (1-4) of the human managers in turn order.</summary>
        internal List<int> Managers()
        {
            var managers = new List<int>();
            int count = this.ReadUInt16(A3Addresses.ManagerCount);
            for (int turn = 1; turn <= count && turn <= A3Addresses.MaxManagers; turn++)
            {
                int number = this.ReadUInt16(A3Addresses.ManagerOrder + (uint)(turn * 2));
                if (number >= 1 && number <= A3Addresses.MaxManagers && !managers.Contains(number))
                    managers.Add(number);
            }
            return managers;
        }

        /// <summary>
        /// The manager whose turn it is: the one whose club the game has loaded as its own (club cache slot 0),
        /// otherwise the first one; manager 1 if the list can't be read.
        /// </summary>
        internal int ActiveManager()
        {
            List<int> managers = this.Managers();
            byte[] slot = this.memory.Read(A3Addresses.ClubCache, 3);
            if (managers.Count > 1 && slot != null && slot[0] != 0)
            {
                var loaded = new ClubKey(slot[1], slot[2]);
                foreach (int manager in managers)
                    if (this.ManagerClub(manager).Equals(loaded))
                        return manager;
            }
            return managers.Count > 0 ? managers[0] : 1;
        }

        /// <summary>The club manager <paramref name="manager"/> works for (country 0 = none).</summary>
        internal ClubKey ManagerClub(int manager) =>
            new ClubKey(this.ReadUInt16(this.ManagerField(manager, A3Addresses.ManagerClubCountry)),
                        this.ReadUInt16(this.ManagerField(manager, A3Addresses.ManagerClubNumber)));

        /// <summary>
        /// Where a field of manager <paramref name="manager"/> (given by manager 1's address in the object) holds
        /// the current value: in the object while the game holds it, otherwise in the manager's data.
        /// </summary>
        internal uint ManagerField(int manager, uint field)
        {
            uint inObject = field + (uint)((manager - 1) * A3Addresses.ManagerStride);
            if (this.ReadUInt16(A3Addresses.ManagerReferences + (uint)(manager * 2)) > 0)
                return inObject;
            int? data = ManagerDataOffset(field);
            return data.HasValue ? A3Addresses.ManagerData + (uint)((manager - 1) * A3Addresses.ManagerDataStride + data.Value) : inObject;
        }

        // Writes a manager field to the object and to the data the game writes it back to.
        private void WriteManagerField(int manager, uint field, byte[] value)
        {
            this.memory.Write(field + (uint)((manager - 1) * A3Addresses.ManagerStride), value);
            int? data = ManagerDataOffset(field);
            if (data.HasValue)
                this.memory.Write(A3Addresses.ManagerData + (uint)((manager - 1) * A3Addresses.ManagerDataStride + data.Value), (byte[])value.Clone());
        }

        private static int? ManagerDataOffset(uint field)
        {
            int offset = (int)(field - A3Addresses.Manager);
            foreach (int[] range in A3Addresses.ManagerDataMap)
                if (offset >= range[0] && offset < range[0] + range[2])
                    return range[1] + offset - range[0];
            return null;
        }

        // A text field of fixed size: the text, cut to leave room for the terminating zero, then zeros.
        private static byte[] Field(string text, int size)
        {
            byte[] field = new byte[size];
            byte[] bytes = Text.GetBytes(text);
            Array.Copy(bytes, field, Math.Min(bytes.Length, size - 1));
            return field;
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
