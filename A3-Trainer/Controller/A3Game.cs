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
            if (edited.OnTransferList != original.OnTransferList)
                this.SetTransferListed(edited, edited.OnTransferList);

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
            ClubInfo info = this.ReadClub(own.Value);
            byte[] trust = this.memory.Read(this.ManagerField(manager, A3Addresses.ManagerBoardTrust), 6) ?? new byte[6];
            uint contract = this.ManagerField(manager, A3Addresses.ManagerSalary);

            info.Manager = manager;
            info.ManagerFirstName = this.ReadString(this.ManagerField(manager, A3Addresses.ManagerFirstName), 10);
            info.ManagerLastName = this.ReadString(this.ManagerField(manager, A3Addresses.ManagerLastName), 0x10);
            info.ManagerSalary = (int)this.ReadUInt32(contract);
            info.PointBonus = (int)this.ReadUInt32(contract + ContractOffset.PointBonus);
            info.CupBonus = (int)this.ReadUInt32(contract + ContractOffset.CupBonus);
            info.TitleBonus = (int)this.ReadUInt32(contract + ContractOffset.TitleBonus);
            info.GoalBonus = (int)this.ReadUInt32(contract + ContractOffset.GoalBonus);
            info.ClubOption = this.ReadByte(contract + ContractOffset.ClubOption) != 0;
            info.ContractYears = this.ReadUInt16(contract + ContractOffset.Years);
            info.ExitClause = this.ReadByte(contract + ContractOffset.ExitClause) != 0;
            info.ManagerPrivateMoney = (int)this.ReadUInt32(this.ManagerField(manager, A3Addresses.ManagerPrivateMoney));
            info.MediaPopularity = this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerMediaPopularity));
            info.PublicPopularity = this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerPublicPopularity));
            info.FanPopularity = this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerFanPopularity));
            info.FanWeight = this.FanWeight(manager);
            info.BoardTrustBase = BitConverter.ToInt16(trust, 0);
            info.BoardTrustBonus = trust[2] + trust[3] + (sbyte)trust[4] + (sbyte)trust[5];
            return info;
        }

        /// <summary>The club data of any club (no manager data), or null if the key names no club.</summary>
        internal ClubInfo ReadClub(ClubKey key)
        {
            uint? record = this.ClubAddress(key);
            if (!record.HasValue)
                return null;
            uint club = record.Value;
            uint copy = this.ClubCopy(key);
            // The finance block of the loaded copy (what the game uses), else the record's, at the record's offsets.
            uint finances = copy != 0 ? copy - (uint)A3Addresses.ClubCopyFinanceShift : club;
            return new ClubInfo
            {
                Manager = 0,
                Key = key,
                Name = this.ReadString(club + A3Addresses.ClubName, A3Addresses.ClubNameSize),
                ShortName = this.ReadString(club + A3Addresses.ClubShortName, A3Addresses.ClubShortNameSize),
                City = this.ReadString(club + A3Addresses.ClubCity, A3Addresses.ClubNameSize),
                SeasonStartMoney = (int)this.ReadUInt32(finances + A3Addresses.ClubSeasonStartMoney),
                HasAdsContract = this.ReadByte(finances + A3Addresses.ClubAdsContract) != 0,
                Ads = (int)this.ReadUInt32(finances + A3Addresses.ClubAds),
                HasOutfitter = this.ReadByte(finances + A3Addresses.ClubOutfitterContract) != 0,
                Outfitter = (int)this.ReadUInt32(finances + A3Addresses.ClubOutfitter),
                // The loaded club's working copy is what the game uses; both hold the same otherwise.
                Image = this.ReadByte(copy != 0 ? copy + A3Addresses.ClubCopyImage : club + A3Addresses.ClubImage),
                Trouble = this.ReadByte(copy != 0 ? copy + A3Addresses.ClubCopyTrouble : club + A3Addresses.ClubTrouble),
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
            WriteManager(A3Addresses.ManagerSalary + ContractOffset.ClubOption, original.ClubOption ? 1 : 0, edited.ClubOption ? 1 : 0, 1);
            WriteManager(A3Addresses.ManagerPrivateMoney, original.ManagerPrivateMoney, edited.ManagerPrivateMoney, 4);
            WriteManager(A3Addresses.ManagerMediaPopularity, original.MediaPopularity, edited.MediaPopularity, 1);
            WriteManager(A3Addresses.ManagerPublicPopularity, original.PublicPopularity, edited.PublicPopularity, 1);
            WriteManager(A3Addresses.ManagerFanPopularity, original.FanPopularity, edited.FanPopularity, 1);
            WriteManager(A3Addresses.ManagerBoardTrust, original.BoardTrustBase, edited.BoardTrustBase, 2);

            this.SaveClub(original, edited);

            void WriteManager(uint field, int was, int now, int size)
            {
                if (now != was)
                    this.WriteManagerField(manager, field, size == 4 ? BitConverter.GetBytes(now) : size == 2 ? BitConverter.GetBytes((ushort)now) : new[] { (byte)now });
            }
        }

        /// <summary>Writes the club values (not the manager's) that changed, for any club.</summary>
        internal void SaveClub(ClubInfo original, ClubInfo edited)
        {
            // The club's values live in its record and, while the game has it loaded, in the working copy.
            if (edited.Image == original.Image && edited.Trouble == original.Trouble && edited.SeasonStartMoney == original.SeasonStartMoney &&
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
            }
            if (copy != 0)
            {
                WriteByte(copy + A3Addresses.ClubCopyImage, original.Image, edited.Image);
                WriteByte(copy + A3Addresses.ClubCopyTrouble, original.Trouble, edited.Trouble);
                WriteText(copy + A3Addresses.ClubCopyName, original.Name, edited.Name, A3Addresses.ClubNameSize);
                WriteFinances(copy - (uint)A3Addresses.ClubCopyFinanceShift);
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
        }

        // As FUN_006A5A7D: the charisma of the level in force.
        private int FanWeight(int manager)
        {
            int level = ManagerPerson.InForce(this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerLevel)),
                                              this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerLevelFlags)));
            byte[] charisma = this.memory.Read(this.ManagerField(manager, CompetencyField(level, A3Addresses.ManagerCharisma)), 2);
            return (charisma == null ? 0 : BitConverter.ToInt16(charisma, 0)) * 4 + 80;
        }

        private static uint CompetencyField(int level, int competency) =>
            A3Addresses.ManagerCompetencies + (uint)(level * A3Addresses.ManagerCompetencyRow + competency * 2);

        /// <summary>Birthday, nerves, level and competencies of human manager <paramref name="manager"/> (1-4).</summary>
        internal ManagerPerson ReadPerson(int manager)
        {
            byte[] birthday = this.memory.Read(this.ManagerBirthday(manager), 4);
            byte[] table = this.memory.Read(this.ManagerField(manager, A3Addresses.ManagerCompetencies),
                                            A3Addresses.ManagerLevels * A3Addresses.ManagerCompetencyRow);
            byte[] nerves = this.memory.Read(this.ManagerField(manager, A3Addresses.ManagerNerves), 2);
            if (birthday == null || table == null || nerves == null)
                return null;
            return new ManagerPerson
            {
                Manager = manager,
                Birthday = new GameDate(BitConverter.ToInt16(birthday, 2), birthday[1], birthday[0]),
                Nerves = BitConverter.ToInt16(nerves, 0),
                Level = this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerLevel)),
                LevelFlags = this.ReadByte(this.ManagerField(manager, A3Addresses.ManagerLevelFlags)),
                Competencies = Enumerable.Range(0, A3Addresses.ManagerLevels)
                    .Select(level => Enumerable.Range(0, Tables.Competencies.Length)
                        .Select(c => (int)BitConverter.ToInt16(table, level * A3Addresses.ManagerCompetencyRow + c * 2)).ToArray())
                    .ToArray(),
            };
        }

        /// <summary>Writes the birthday, nerves, level and competencies that changed.</summary>
        internal void SavePerson(ManagerPerson original, ManagerPerson edited)
        {
            int manager = edited.Manager;
            if (!edited.Birthday.Equals(original.Birthday))
            {
                GameDate date = edited.Birthday;
                byte[] year = BitConverter.GetBytes((short)date.Year);
                this.memory.Write(this.ManagerBirthday(manager), new[] { (byte)date.Day, (byte)date.Month, year[0], year[1] });
            }
            if (edited.Nerves != original.Nerves)
                this.WriteManagerField(manager, A3Addresses.ManagerNerves, BitConverter.GetBytes((short)edited.Nerves));
            if (edited.Level != original.Level)
                this.WriteManagerField(manager, A3Addresses.ManagerLevel, new[] { (byte)edited.Level });
            for (int level = 0; level < A3Addresses.ManagerLevels; level++)
                for (int c = 0; c < Tables.Competencies.Length; c++)
                    if (edited.Competencies[level][c] != original.Competencies[level][c])
                        this.WriteManagerField(manager, CompetencyField(level, c), BitConverter.GetBytes((short)edited.Competencies[level][c]));
        }

        private uint ManagerBirthday(int manager) =>
            A3Addresses.ManagerData + (uint)((manager - 1) * A3Addresses.ManagerDataStride + A3Addresses.ManagerDataBirthday);

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

        /// <summary>The club's stadium, or null if the club or its stadium can't be found.</summary>
        internal Stadium ReadStadium(ClubKey key)
        {
            uint? address = this.FindStadium(key);
            byte[] data = address.HasValue ? this.memory.Read(address.Value, 0x70) : null;
            if (data == null)
                return null;
            short Word(int offset) => BitConverter.ToInt16(data, offset);
            int tribunes = StadiumAddress.Tribunes;
            return new Stadium
            {
                Address = address.Value,
                Name = this.ReadString(address.Value + StadiumOffset.Name, StadiumOffset.NameSize),
                City = this.ReadString(address.Value + StadiumOffset.City, StadiumOffset.CitySize),
                Vip = Enumerable.Range(0, tribunes).Select(t => (int)Word(StadiumOffset.Vip + t * 2)).ToArray(),
                Seats = Enumerable.Range(0, tribunes).Select(t => (int)Word(StadiumOffset.Seats + t * 2)).ToArray(),
                Standing = Enumerable.Range(0, tribunes).Select(t => (int)Word(StadiumOffset.Standing + t * 2)).ToArray(),
                Condition = Enumerable.Range(0, tribunes).Select(t => (int)data[StadiumOffset.Condition + t]).ToArray(),
                TribuneBits = (ushort)Word(StadiumOffset.TribuneBits),
                Comfort = (ushort)Word(StadiumOffset.Comfort),
                Scoreboard = data[StadiumOffset.Scoreboard],
            };
        }

        /// <summary>Writes what changed; the capacity the game keeps follows the places.</summary>
        internal void SaveStadium(Stadium original, Stadium edited)
        {
            foreach (uint address in this.StadiumCopies(edited.Address))
                this.WriteStadium(address, original, edited);
        }

        // The stadium and, while the game's stadium page shows it, the page's copies of it.
        private IEnumerable<uint> StadiumCopies(uint stadium)
        {
            yield return stadium;
            uint page = this.ReadUInt32(this.ReadUInt32(StadiumAddress.PageHolder) + StadiumAddress.CurrentPage);
            if (page == 0 || this.ReadUInt32(page) != StadiumAddress.PageVtable)
                yield break;
            uint copy = this.ReadUInt32(page + StadiumAddress.PageClubCopy);
            if (copy == 0)
                yield break;
            var club = new ClubKey(this.ReadByte(copy + A3Addresses.ClubCopyCountry), this.ReadByte(copy + A3Addresses.ClubCopyNumber));
            if (this.FindStadium(club) != stadium)
                yield break;
            // Slot 0 is the current stadium, the others are being built.
            yield return page + StadiumAddress.PageSlots;
            if (this.ReadUInt16(page + StadiumAddress.PageSlot) == 0)
                yield return page + StadiumAddress.PageShown;
        }

        private void WriteStadium(uint address, Stadium original, Stadium edited)
        {
            if (edited.Name != original.Name && edited.Name.Length > 0)
                this.memory.Write(address + StadiumOffset.Name, Field(edited.Name, StadiumOffset.NameSize));
            if (edited.City != original.City && edited.City.Length > 0)
                this.memory.Write(address + StadiumOffset.City, Field(edited.City, StadiumOffset.CitySize));
            for (int t = 0; t < StadiumAddress.Tribunes; t++)
            {
                WriteWord(StadiumOffset.Vip + t * 2, original.Vip[t], edited.Vip[t]);
                WriteWord(StadiumOffset.Seats + t * 2, original.Seats[t], edited.Seats[t]);
                WriteWord(StadiumOffset.Standing + t * 2, original.Standing[t], edited.Standing[t]);
                if (edited.Condition[t] != original.Condition[t])
                    this.memory.Write(address + (uint)(StadiumOffset.Condition + t), new[] { (byte)edited.Condition[t] });
            }
            WriteWord(StadiumOffset.TribuneBits, original.TribuneBits, edited.TribuneBits);
            WriteWord(StadiumOffset.Comfort, original.Comfort, edited.Comfort);
            if (edited.Scoreboard != original.Scoreboard)
                this.memory.Write(address + StadiumOffset.Scoreboard, new[] { (byte)edited.Scoreboard });
            if (edited.Capacity != original.Capacity)
            {
                this.memory.Write(address + StadiumOffset.Capacity, BitConverter.GetBytes(edited.Capacity));
                this.memory.Write(address + StadiumOffset.Capacity2, BitConverter.GetBytes(edited.Capacity));
            }

            void WriteWord(int offset, int was, int now)
            {
                if (now != was)
                    this.memory.Write(address + (uint)offset, BitConverter.GetBytes((ushort)now));
            }
        }

        // The current stadium of a club (FUN_00638335), or null if the club has none the trainer knows.
        private uint? FindStadium(ClubKey key)
        {
            if (!this.ClubAddress(key).HasValue)
                return null;
            int index = this.ReadUInt16(A3Addresses.ClubIndexTable + (uint)(key.Country * 2)) + key.Number;
            int otherFirst = this.ReadUInt16(StadiumAddress.OtherFirst);
            uint address = index < otherFirst
                ? StadiumAddress.CityStadiums + (uint)(index * StadiumAddress.CityStadiumSize)
                : StadiumAddress.OtherStadiums + (uint)((index - otherFirst) * StadiumAddress.OtherStadiumSize);
            return this.ReadString(address + StadiumAddress.Data + StadiumOffset.Name, StadiumOffset.NameSize).Length > 0
                ? address + StadiumAddress.Data
                : (uint?)null;
        }

        /// <summary>Fans, board, fan friendship and arch rival of a club, or null if there is no such club.</summary>
        internal ClubIdentity ReadIdentity(ClubKey key)
        {
            uint? club = this.ClubAddress(key);
            if (!club.HasValue)
                return null;
            // The loaded club's working copy is what the game uses; the fan name lives only in the record.
            uint copy = this.ClubCopy(key);
            int Value(int recordOffset, int copyOffset) =>
                this.ReadByte(copy != 0 ? copy + (uint)copyOffset : club.Value + (uint)recordOffset);
            return new ClubIdentity
            {
                Key = key,
                FanName = this.ReadString(club.Value + A3Addresses.ClubFanName, A3Addresses.ClubFanNameSize),
                FanCrowd = Value(A3Addresses.ClubFanCrowd, A3Addresses.ClubCopyFanCrowd),
                FanType = Value(A3Addresses.ClubFanType, A3Addresses.ClubCopyFanType),
                Board = Value(A3Addresses.ClubBoard, A3Addresses.ClubCopyBoard),
                Friend = (sbyte)Value(A3Addresses.ClubFriend, A3Addresses.ClubCopyFriend),
                Rival = (sbyte)Value(A3Addresses.ClubRival, A3Addresses.ClubCopyRival),
            };
        }

        /// <summary>
        /// Writes the identity values that changed. A fan friendship is mutual, as in the game's club editor: the new
        /// friend gets this club as its friend, and whoever was a friend of either club before loses that friendship.
        /// </summary>
        internal void SaveIdentity(ClubIdentity original, ClubIdentity edited)
        {
            uint club = this.ClubAddress(edited.Key).Value;
            if (edited.FanName != original.FanName)
                this.memory.Write(club + A3Addresses.ClubFanName, Field(edited.FanName, A3Addresses.ClubFanNameSize));
            this.WriteClubField(edited.Key, A3Addresses.ClubFanCrowd, A3Addresses.ClubCopyFanCrowd, original.FanCrowd, edited.FanCrowd);
            this.WriteClubField(edited.Key, A3Addresses.ClubFanType, A3Addresses.ClubCopyFanType, original.FanType, edited.FanType);
            this.WriteClubField(edited.Key, A3Addresses.ClubBoard, A3Addresses.ClubCopyBoard, original.Board, edited.Board);
            this.WriteClubField(edited.Key, A3Addresses.ClubRival, A3Addresses.ClubCopyRival, original.Rival, edited.Rival);
            if (edited.Friend == original.Friend)
                return;

            this.WriteFriend(edited.Key, original.Friend, edited.Friend);
            this.EndFriendship(new ClubKey(edited.Key.Country, original.Friend), edited.Key.Number);
            if (edited.Friend < 0)
                return;
            var friend = new ClubKey(edited.Key.Country, edited.Friend);
            ClubIdentity theirs = this.ReadIdentity(friend);
            if (theirs == null)
                return;
            if (theirs.Friend != edited.Key.Number)
                this.EndFriendship(new ClubKey(edited.Key.Country, theirs.Friend), edited.Friend);
            this.WriteFriend(friend, theirs.Friend, edited.Key.Number);
        }

        private void WriteFriend(ClubKey key, int was, int now) =>
            this.WriteClubField(key, A3Addresses.ClubFriend, A3Addresses.ClubCopyFriend, was, now);

        // Clears the friendship of club, if it is with the club number partner.
        private void EndFriendship(ClubKey club, int partner)
        {
            if (club.Number < 0 || club.Number == partner)
                return;
            ClubIdentity identity = this.ReadIdentity(club);
            if (identity != null && identity.Friend == partner)
                this.WriteFriend(club, partner, -1);
        }

        // A byte in the club's record and, while the game has the club loaded, its working copy.
        private void WriteClubField(ClubKey key, int recordOffset, int copyOffset, int was, int now)
        {
            if (now == was)
                return;
            uint? club = this.ClubAddress(key);
            if (!club.HasValue)
                return;
            var value = new[] { (byte)now };
            this.memory.Write(club.Value + (uint)recordOffset, value);
            uint copy = this.ClubCopy(key);
            if (copy != 0)
                this.memory.Write(copy + (uint)copyOffset, value);
        }

        // The finance blocks of a club at the record's offsets: the loaded copy's (what the game uses) first, then the record's.
        private List<uint> FinanceBlocks(ClubKey key)
        {
            var blocks = new List<uint>();
            uint? record = this.ClubAddress(key);
            if (!record.HasValue)
                return blocks;
            uint copy = this.ClubCopy(key);
            if (copy != 0)
                blocks.Add(copy - (uint)A3Addresses.ClubCopyFinanceShift);
            blocks.Add(record.Value);
            return blocks;
        }

        /// <summary>Overdraft and loans of any club, or null if the key names no club.</summary>
        internal ClubFinances ReadFinances(ClubKey key)
        {
            List<uint> blocks = this.FinanceBlocks(key);
            if (blocks.Count == 0)
                return null;
            uint finances = blocks[0];
            var result = new ClubFinances
            {
                Key = key,
                Overdraft = this.ReadUInt16(finances + A3Addresses.ClubOverdraft) * A3Addresses.OverdraftUnit,
            };
            foreach (long loan in this.RunningLoans(finances))
            {
                result.LoanCount++;
                result.LoanTotal += loan;
            }
            return result;
        }

        private IEnumerable<long> RunningLoans(uint finances)
        {
            byte[] loans = this.memory.Read(finances + A3Addresses.ClubLoans, A3Addresses.ClubLoanSlots * A3Addresses.ClubLoanSize);
            if (loans == null)
                yield break;
            for (int slot = 0; slot < A3Addresses.ClubLoanSlots; slot++)
            {
                int at = slot * A3Addresses.ClubLoanSize;
                if ((loans[at + A3Addresses.ClubLoanFlags] & 1) != 0)
                    yield return BitConverter.ToInt64(loans, at);
            }
        }

        /// <summary>Writes the overdraft if it changed, to the record and the loaded copy.</summary>
        internal void SaveFinances(ClubFinances original, ClubFinances edited)
        {
            if (edited.Overdraft == original.Overdraft)
                return;
            byte[] value = BitConverter.GetBytes((ushort)(edited.Overdraft / A3Addresses.OverdraftUnit));
            foreach (uint finances in this.FinanceBlocks(edited.Key))
                this.memory.Write(finances + A3Addresses.ClubOverdraft, value);
        }

        /// <summary>
        /// Ends the club's running loans without paying them back: the loans count as money in, so their sum moves to the
        /// wealth at the start of the season and the bank balance stays as it was. Returns the DM forgiven.
        /// </summary>
        internal long ForgiveLoans(ClubKey key)
        {
            long forgiven = 0;
            List<uint> blocks = this.FinanceBlocks(key);
            for (int i = 0; i < blocks.Count; i++)
            {
                uint finances = blocks[i];
                byte[] loans = this.memory.Read(finances + A3Addresses.ClubLoans, A3Addresses.ClubLoanSlots * A3Addresses.ClubLoanSize);
                if (loans == null)
                    continue;
                long sum = 0;
                for (int slot = 0; slot < A3Addresses.ClubLoanSlots; slot++)
                {
                    int at = slot * A3Addresses.ClubLoanSize;
                    if ((loans[at + A3Addresses.ClubLoanFlags] & 1) == 0)
                        continue;
                    sum += BitConverter.ToInt64(loans, at);
                    uint entry = finances + A3Addresses.ClubLoans + (uint)at;
                    this.memory.Write(entry, new byte[8]);
                    this.memory.Write(entry + A3Addresses.ClubLoanFlags, new byte[1]);
                }
                if (sum != 0)
                {
                    byte[] start = this.memory.Read(finances + A3Addresses.ClubSeasonStartMoney, 8) ?? new byte[8];
                    this.memory.Write(finances + A3Addresses.ClubSeasonStartMoney, BitConverter.GetBytes(BitConverter.ToInt64(start, 0) + sum));
                }
                if (i == 0)
                    forgiven = sum;
            }
            return forgiven;
        }

        /// <summary>The interest rates every club pays or gets.</summary>
        internal InterestRates ReadRates() => new InterestRates
        {
            Interest = this.ReadInt16(A3Addresses.InterestRate),
            Overdraft = this.ReadInt16(A3Addresses.OverdraftRate),
            Savings = this.ReadInt16(A3Addresses.SavingsRate),
            Loan = this.ReadInt16(A3Addresses.LoanRate),
        };

        /// <summary>Writes the interest rates that changed.</summary>
        internal void SaveRates(InterestRates original, InterestRates edited)
        {
            Write(A3Addresses.InterestRate, original.Interest, edited.Interest);
            Write(A3Addresses.OverdraftRate, original.Overdraft, edited.Overdraft);
            Write(A3Addresses.SavingsRate, original.Savings, edited.Savings);
            Write(A3Addresses.LoanRate, original.Loan, edited.Loan);

            void Write(uint address, int was, int now)
            {
                if (now != was)
                    this.memory.Write(address, BitConverter.GetBytes((short)now));
            }
        }

        /// <summary>The players on the transfer list, in its order.</summary>
        internal List<int> TransferListPlayers()
        {
            var players = new List<int>();
            int count = this.ReadInt16(A3Addresses.TransferList + A3Addresses.TransferListCount);
            if (count <= 0 || count > A3Addresses.TransferListMax)
                return players;
            byte[] entries = this.memory.Read(A3Addresses.TransferList + A3Addresses.TransferListEntries, count * A3Addresses.TransferListEntrySize);
            if (entries != null)
                for (int i = 0; i < count; i++)
                    players.Add(BitConverter.ToInt16(entries, i * A3Addresses.TransferListEntrySize));
            return players;
        }

        // Adds the player to the transfer list as FUN_00758B34 does (offered for sale from today) or takes him off it,
        // closing the gap. The potential and form values the game computes for sorting are approximated.
        private void SetTransferListed(Player player, bool listed)
        {
            List<int> players = this.TransferListPlayers();
            int at = players.IndexOf(player.Index);
            uint list = A3Addresses.TransferList;
            uint entries = list + A3Addresses.TransferListEntries;
            int size = A3Addresses.TransferListEntrySize;
            if (listed && at < 0 && players.Count < A3Addresses.TransferListMax)
            {
                var entry = new byte[size];
                BitConverter.GetBytes((short)player.Index).CopyTo(entry, 0);
                entry[2] = (byte)player.ClubCountry;
                BitConverter.GetBytes((short)player.ClubNumber).CopyTo(entry, 4);
                entry[6] = (byte)player.Position;
                entry[7] = (byte)player.Strength;
                entry[8] = (byte)player.Strength;
                BitConverter.GetBytes((short)this.ReadInt16(A3Addresses.DayCounter)).CopyTo(entry, 0xA);
                entry[0xC] = 1;
                entry[0xD] = 3;
                BitConverter.GetBytes((short)100).CopyTo(entry, 0xE);
                this.memory.Write(entries + (uint)(players.Count * size), entry);
                this.memory.Write(list + A3Addresses.TransferListCount, BitConverter.GetBytes((short)(players.Count + 1)));
            }
            else if (!listed && at >= 0)
            {
                byte[] all = this.memory.Read(entries, players.Count * size);
                var moved = new byte[players.Count * size];
                Array.Copy(all, 0, moved, 0, at * size);
                Array.Copy(all, (at + 1) * size, moved, at * size, (players.Count - at - 1) * size);
                this.memory.Write(entries, moved);
                this.memory.Write(list + A3Addresses.TransferListCount, BitConverter.GetBytes((short)(players.Count - 1)));
            }
        }

        /// <summary>The stocks on the market, in the market's order (clubs and companies).</summary>
        internal List<StockQuote> ReadStocks()
        {
            var stocks = new List<StockQuote>();
            int count = (int)this.ReadUInt32(A3Addresses.StockMarket + A3Addresses.StockCount);
            if (count <= 0 || count > 1000)
                return stocks;
            byte[] all = this.memory.Read(A3Addresses.StockMarket + A3Addresses.StockEntries, count * A3Addresses.StockSize);
            if (all == null)
                return stocks;
            for (int index = 0; index < count; index++)
            {
                int at = index * A3Addresses.StockSize;
                short country = BitConverter.ToInt16(all, at + A3Addresses.StockCountry);
                int week = BitConverter.ToInt16(all, at + A3Addresses.StockWeek);
                stocks.Add(new StockQuote
                {
                    Index = index,
                    Name = Text.GetString(all, at, A3Addresses.StockNameSize).Split('\0')[0],
                    Club = country < 0 ? (ClubKey?)null : new ClubKey(country, BitConverter.ToInt16(all, at + A3Addresses.StockClub)),
                    Shares = BitConverter.ToInt32(all, at + A3Addresses.StockShares),
                    Price = week >= 0 && week < A3Addresses.StockWeeks ? BitConverter.ToSingle(all, at + A3Addresses.StockHistory + week * 4) : 0,
                });
            }
            return stocks;
        }

        /// <summary>Sets the current week's price of a stock.</summary>
        internal void SetStockPrice(int stock, float price)
        {
            uint entry = A3Addresses.StockMarket + A3Addresses.StockEntries + (uint)(stock * A3Addresses.StockSize);
            int week = this.ReadInt16(entry + A3Addresses.StockWeek);
            if (week >= 0 && week < A3Addresses.StockWeeks)
                this.memory.Write(entry + A3Addresses.StockHistory + (uint)(week * 4), BitConverter.GetBytes(price));
        }

        private static uint DepotOf(int manager) => A3Addresses.Depot + (uint)((manager - 1) * A3Addresses.DepotStride);

        private static uint DepotSlot(int manager, int slot) =>
            DepotOf(manager) + A3Addresses.DepotSlotsAt + (uint)(slot * A3Addresses.DepotSlotSize);

        /// <summary>The positions in human manager <paramref name="manager"/>'s depot.</summary>
        internal List<DepotPosition> ReadDepot(int manager)
        {
            var positions = new List<DepotPosition>();
            byte[] depot = this.memory.Read(DepotOf(manager), A3Addresses.DepotStride);
            if (depot == null)
                return positions;
            for (int slot = 0; slot < A3Addresses.DepotSlots; slot++)
            {
                if (depot[A3Addresses.DepotUsed + slot] == 0)
                    continue;
                int at = A3Addresses.DepotSlotsAt + slot * A3Addresses.DepotSlotSize;
                positions.Add(new DepotPosition
                {
                    Slot = slot,
                    Stock = BitConverter.ToInt32(depot, at),
                    PurchasePrice = BitConverter.ToSingle(depot, at + 4),
                    Shares = BitConverter.ToInt32(depot, at + 8),
                });
            }
            return positions;
        }

        /// <summary>
        /// Writes a position into its slot; a slot of -1 takes the last free one, as the game does. Returns the slot,
        /// or -1 if the depot is full.
        /// </summary>
        internal int SavePosition(int manager, DepotPosition position)
        {
            int slot = position.Slot;
            if (slot < 0)
            {
                byte[] used = this.memory.Read(DepotOf(manager) + A3Addresses.DepotUsed, A3Addresses.DepotSlots);
                if (used == null)
                    return -1;
                slot = Enumerable.Range(0, A3Addresses.DepotSlots).Reverse().Where(i => used[i] == 0).DefaultIfEmpty(-1).First();
                if (slot < 0)
                    return -1;
            }
            var data = new byte[A3Addresses.DepotSlotSize];
            BitConverter.GetBytes(position.Stock).CopyTo(data, 0);
            BitConverter.GetBytes(position.PurchasePrice).CopyTo(data, 4);
            BitConverter.GetBytes(position.Shares).CopyTo(data, 8);
            this.memory.Write(DepotSlot(manager, slot), data);
            this.memory.Write(DepotOf(manager) + A3Addresses.DepotUsed + (uint)slot, new byte[] { 1 });
            return slot;
        }

        /// <summary>Empties a slot of the depot.</summary>
        internal void RemovePosition(int manager, int slot)
        {
            this.memory.Write(DepotOf(manager) + A3Addresses.DepotUsed + (uint)slot, new byte[] { 0 });
            this.memory.Write(DepotSlot(manager, slot), new byte[A3Addresses.DepotSlotSize]);
        }

        private static uint CareerOf(int manager) => A3Addresses.ManagerCareer + (uint)((manager - 1) * A3Addresses.ManagerCareerStride);

        private static uint ManagerDataOf(int manager) => A3Addresses.ManagerData + (uint)((manager - 1) * A3Addresses.ManagerDataStride);

        /// <summary>Titles, stations, years and games of human manager <paramref name="manager"/>.</summary>
        internal ManagerCareer ReadCareer(int manager)
        {
            uint career = CareerOf(manager);
            uint data = ManagerDataOf(manager);
            byte[] games = this.memory.Read(data + A3Addresses.ManagerDataLeagueGames, 0x14) ?? new byte[0x14];
            var result = new ManagerCareer
            {
                Manager = manager,
                Titles = A3Addresses.CareerTitles.Select(t => this.ReadInt16(career + (uint)t.Key)).ToArray(),
                Dismissed = this.ReadInt16(career + A3Addresses.CareerDismissed),
                Resigned = this.ReadInt16(career + A3Addresses.CareerResigned),
                Stations = this.ReadByte(career + A3Addresses.CareerStations),
                Years = this.ReadInt16(this.ManagerField(manager, A3Addresses.ManagerYears)),
            };
            result.Games[0] = BitConverter.ToInt32(games, 0);
            result.Wins[0] = BitConverter.ToInt32(games, 4);
            for (int i = 1; i < 4; i++)
            {
                result.Games[i] = BitConverter.ToInt16(games, 8 + (i - 1) * 4);
                result.Wins[i] = BitConverter.ToInt16(games, 10 + (i - 1) * 4);
            }
            return result;
        }

        /// <summary>Writes the career values that changed.</summary>
        internal void SaveCareer(ManagerCareer original, ManagerCareer edited)
        {
            uint career = CareerOf(edited.Manager);
            uint data = ManagerDataOf(edited.Manager);
            for (int i = 0; i < A3Addresses.CareerTitles.Length; i++)
                Word(career + (uint)A3Addresses.CareerTitles[i].Key, original.Titles[i], edited.Titles[i]);
            Word(career + A3Addresses.CareerDismissed, original.Dismissed, edited.Dismissed);
            Word(career + A3Addresses.CareerResigned, original.Resigned, edited.Resigned);
            if (edited.Years != original.Years)
                this.WriteManagerField(edited.Manager, A3Addresses.ManagerYears, BitConverter.GetBytes((short)edited.Years));
            Dword(data + A3Addresses.ManagerDataLeagueGames, original.Games[0], edited.Games[0]);
            Dword(data + A3Addresses.ManagerDataLeagueGames + 4, original.Wins[0], edited.Wins[0]);
            for (int i = 1; i < 4; i++)
            {
                Word(data + A3Addresses.ManagerDataCupGames + (uint)((i - 1) * 4), original.Games[i], edited.Games[i]);
                Word(data + A3Addresses.ManagerDataCupGames + (uint)((i - 1) * 4 + 2), original.Wins[i], edited.Wins[i]);
            }

            void Word(uint address, int was, int now)
            {
                if (now != was)
                    this.memory.Write(address, BitConverter.GetBytes((short)now));
            }

            void Dword(uint address, int was, int now)
            {
                if (now != was)
                    this.memory.Write(address, BitConverter.GetBytes(now));
            }
        }

        /// <summary>Whether boards can dismiss managers (the new game's option "mit Entlassungen").</summary>
        internal bool ReadDismissals() => this.ReadByte(A3Addresses.Dismissals) != 0;

        /// <summary>Switches dismissals on or off for every manager.</summary>
        internal void SaveDismissals(bool on) => this.memory.Write(A3Addresses.Dismissals, new[] { (byte)(on ? 1 : 0) });

        private int ReadInt16(uint address)
        {
            byte[] data = this.memory.Read(address, 2);
            return data == null ? 0 : BitConverter.ToInt16(data, 0);
        }

        /// <summary>President, opposition leader, founding date and eternal table of any club, or null if the key names no club.</summary>
        internal ClubBoard ReadBoard(ClubKey key)
        {
            uint? record = this.ClubAddress(key);
            if (!record.HasValue)
                return null;
            uint club = record.Value;
            return new ClubBoard
            {
                Key = key,
                President = this.ReadBoardMember(club + A3Addresses.ClubPresident),
                Opposition = this.ReadBoardMember(club + A3Addresses.ClubOpposition),
                Founded = this.ReadDate(club + A3Addresses.ClubFounded),
                Goals = (int)this.ReadUInt32(club + A3Addresses.ClubEternalGoals),
                Conceded = (int)this.ReadUInt32(club + A3Addresses.ClubEternalConceded),
                Games = (int)this.ReadUInt32(club + A3Addresses.ClubEternalGames),
                Points = (int)this.ReadUInt32(club + A3Addresses.ClubEternalPoints),
            };
        }

        /// <summary>Writes the board values that changed; they live in the club's record only.</summary>
        internal void SaveBoard(ClubBoard original, ClubBoard edited)
        {
            uint club = this.ClubAddress(edited.Key).Value;
            this.WriteBoardMember(club + A3Addresses.ClubPresident, original.President, edited.President);
            this.WriteBoardMember(club + A3Addresses.ClubOpposition, original.Opposition, edited.Opposition);
            if (!edited.Founded.Equals(original.Founded))
                this.WriteDate(club + A3Addresses.ClubFounded, edited.Founded);
            WriteDword(A3Addresses.ClubEternalGoals, original.Goals, edited.Goals);
            WriteDword(A3Addresses.ClubEternalConceded, original.Conceded, edited.Conceded);
            WriteDword(A3Addresses.ClubEternalGames, original.Games, edited.Games);
            WriteDword(A3Addresses.ClubEternalPoints, original.Points, edited.Points);

            void WriteDword(int offset, int was, int now)
            {
                if (now != was)
                    this.memory.Write(club + (uint)offset, BitConverter.GetBytes(now));
            }
        }

        private BoardMember ReadBoardMember(uint address) => new BoardMember
        {
            FirstName = this.ReadName(this.ReadUInt16(address)),
            LastName = this.ReadName(this.ReadUInt16(address + 2)),
            Birthday = this.ReadDate(address + 4),
        };

        // A changed name gets its index in the shared name table, which adds a new name the way the game does.
        private void WriteBoardMember(uint address, BoardMember was, BoardMember now)
        {
            if (now.FirstName != was.FirstName)
                this.WriteNameIndex(address, this.NameIndex(now.FirstName));
            if (now.LastName != was.LastName)
                this.WriteNameIndex(address + 2, this.NameIndex(now.LastName));
            if (!now.Birthday.Equals(was.Birthday))
                this.WriteDate(address + 4, now.Birthday);
        }

        // A date as {byte day, byte month, word year}.
        private GameDate ReadDate(uint address)
        {
            byte[] date = this.memory.Read(address, 4) ?? new byte[4];
            return new GameDate(BitConverter.ToUInt16(date, 2), date[1], date[0]);
        }

        private void WriteDate(uint address, GameDate date) =>
            this.memory.Write(address, new[] { (byte)date.Day, (byte)date.Month, (byte)date.Year, (byte)(date.Year >> 8) });

        /// <summary>The amateur team of <paramref name="club"/> (a club of its own in the same country), or null if it has none.</summary>
        internal ClubKey? AmateurClub(ClubKey club)
        {
            uint? record = this.ClubAddress(club);
            if (!record.HasValue)
                return null;
            int number = this.ReadByte(record.Value + A3Addresses.ClubAmateurs);
            if (number == 0xFF)
                return null;
            var amateurs = new ClubKey(club.Country, number);
            return this.ClubAddress(amateurs).HasValue ? amateurs : (ClubKey?)null;
        }

        /// <summary>The clubs of a country by their number in it, with names.</summary>
        internal List<KeyValuePair<int, string>> CountryClubs(int country)
        {
            var clubs = new List<KeyValuePair<int, string>>();
            // The index table only gives the first club; a number past the country's clubs lands in the next country.
            for (int number = 0; number < 256; number++)
            {
                uint? address = this.ClubAddress(new ClubKey(country, number));
                if (!address.HasValue)
                    continue;
                if (this.ReadByte(address.Value + A3Addresses.ClubOwnCountry) != country)
                    break;
                clubs.Add(new KeyValuePair<int, string>(number, this.ReadString(address.Value + A3Addresses.ClubName, A3Addresses.ClubNameSize)));
            }
            return clubs;
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
