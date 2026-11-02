namespace A3_Trainer.Controller
{
    /// <summary>
    /// Fixed addresses and record layouts of Anstoss 3 v1.40 (the anstoss3.exe for Windows 8 and
    /// later, 4,980,803 bytes, built July 2003). Found with Ghidra and checked against a running
    /// game; the research notes are in the local re/anstoss3/STRUKTUREN.md. The CD exes (v1.0 and
    /// the official patch 1.10) are separate builds with a different layout and are not supported.
    /// </summary>
    internal static class A3Addresses
    {
        // --- Build check: vtables the first slot of each array must carry.
        internal const uint PlayerVtable = 0x0083271C;   // CDatenSpieler
        internal const uint ClubVtable = 0x008317AC;     // CDatenMannschaft

        // --- Player records (CDatenSpieler), indexed by player number.
        internal const uint PlayerArray = 0x00C02550;
        internal const int PlayerSize = 0x354;
        internal const int PlayerSlots = 17006;

        // --- Name table: char* per name; players store first/last name indices into it.
        internal const uint NameTable = 0x019D3B68;
        internal const int NameTableSlots = 34000;        // room up to the next global (0x019F4ED8); the database fills 19,866

        // --- Name pool: the strings behind the name table, one after another (index order). A save game
        // stores count, size and the pool and rebuilds the table from it on loading (FUN_00678F07), so a
        // name appended like the game does (FUN_0068F8A0) is kept.
        internal const uint NamePool = 0x00BA10D0;
        internal const int NamePoolWrite = 0x20;          // char*, end of the used part
        internal const int NamePoolData = 0x28;           // first string
        internal const int NamePoolCount = 0x3D0B8;      // word, number of names; also the end of the pool
        internal const uint NameCount = 0x00BDE190;       // word, copy of the count set on loading
        internal const int MaxNameLength = 20;            // longest name of the database

        // --- First names for relatives: per name country 100 words (name table indices), see FUN_0067503A.
        internal const uint FirstNameLists = 0x00B9F048;
        internal const int FirstNameListSize = 100;
        internal const int MaxChildren = 5;                // children a player can have (record +0x174 holds their birth dates)

        // --- Country names: char* per country number (nationalities and clubs); the next table starts at ClubIndexTable.
        internal const uint CountryNames = 0x00AE5940;
        internal const int CountrySlots = 364;

        // --- Working-copy cache ("CSpieler"): binary tree, the game edits players of the loaded
        // teams (own squad, next opponent) here and writes them back to the record on save.
        internal const uint PlayerCacheRoot = 0x00A3D3C4;
        internal const int CacheNodeLeft = 0x0;
        internal const int CacheNodeRight = 0x4;
        internal const int CacheNodeEntry = 0xC;
        internal const int CacheEntryIndex = 0x0;        // word, player number
        internal const int CacheEntryCompact = 0x8;      // compact player data starts here
        internal const int CompactSize = 0xD0;

        // --- Clubs (CDatenMannschaft), indexed by club number.
        internal const uint ClubArray = 0x01A72FF8;
        internal const int ClubSize = 0x990;
        internal const int ClubCount = 492;
        internal const uint ClubIndexTable = 0x00AE5EF0;  // word per country: first club number
        internal const int ClubName = 0x20;        // char[20]
        internal const int ClubShortName = 0x34;   // char[4]
        internal const int ClubCity = 0x38;        // char[20]
        internal const int ClubNameSize = 20;
        internal const int ClubShortNameSize = 4;
        internal const int ClubTrouble = 0x12C;    // bits 0-4: trouble with the association (Tables.AssociationLevel), higher bits game state
        internal const int ClubImage = 0x138;      // byte, 10-50, see Tables.ClubImages

        // --- Working copies of the loaded clubs (own club, next opponent; FUN_00639E63): 6 entries of
        // 12 bytes {used, country, number, ...; object pointer at +8}. The game writes the object back to
        // the record (FUN_0063C908), so the trainer writes both.
        internal const uint ClubCache = 0x00A6AB10;
        internal const int ClubCacheSlots = 6;
        internal const int ClubCacheEntrySize = 12;
        internal const int ClubCacheObject = 8;
        internal const int ClubCopyName = 0x02;    // copied from the record when the club is loaded (FUN_0063B75A)
        internal const int ClubCopyTrouble = 0x102;
        internal const int ClubCopyImage = 0x10F;
        // Finances: 250 dwords from the record's +0x488, the copy's +0x210 (FUN_0063C908); the first is the wealth at
        // the start of the season, the bank balance is it plus the season's bookings after it.
        internal const int ClubSeasonStartMoney = 0x488;
        internal const int ClubCopySeasonStartMoney = 0x210;
        // Sponsor contracts in that block (record offset; the copy's is 0x278 lower), each {byte years, byte signed,
        // dword DM per season}. Without a board contract the game sums up the single boards instead (FUN_0061F4E6);
        // without an outfitter it pays nothing (FUN_0061F572).
        internal const int ClubAdsContract = 0x819;
        internal const int ClubAds = 0x81C;
        internal const int ClubOutfitterContract = 0x829;
        internal const int ClubOutfitter = 0x82C;
        internal const int ClubCopyFinanceShift = ClubSeasonStartMoney - ClubCopySeasonStartMoney;

        // --- Human managers (hot seat, up to 4). The constants below are manager 1's fields; manager n's are
        // (n - 1) * ManagerStride further. The game works on these objects only while it holds them (reference
        // count above 0, FUN_006A426B) and writes them back to the managers' data on every release
        // (FUN_006A4930); ManagerDataMap says where each field lies there.
        internal const uint Manager = 0x01B995E8;
        internal const int ManagerStride = 0x1A70;
        internal const int ManagerDataStride = 0x2158;
        internal const uint ManagerReferences = 0x01B9FFAA;   // word per manager number (1-4)
        internal const uint ManagerCount = 0x00B5CBCE;        // word, number of human managers
        internal const uint ManagerOrder = 0x00B5CB90;        // word per turn (1-4): manager number (FUN_006A421A)
        internal const int MaxManagers = 4;
        /// <summary>{object offset, data offset, length} of every field the trainer uses (FUN_006A426B).</summary>
        internal static readonly int[][] ManagerDataMap =
        {
            new[] { 0x00, 0x20, 10 },       // first name
            new[] { 0x0A, 0x2A, 16 },       // last name
            new[] { 0x1E, 0x3E, 2 },        // club country
            new[] { 0x20, 0x40, 2 },        // club number
            new[] { 0x22, 0x42, 2 },        // image level at +0x23
            new[] { 0x3A, 0x146, 3 },       // popularity
            new[] { 0x50, 0x17C, 0x20 },    // contract
            new[] { 0x98, 0x138, 4 },       // private money
            new[] { 0x9C, 0x1A4, 6 },       // board trust
            new[] { 0x17BC, 0x19CC, 0x1E }, // image flags at +0x17D9
            new[] { 0x17FA, 0x54, 0xE0 },   // fan factors from +0x1804
        };

        // --- Fields of human manager 1.
        internal const uint ManagerFirstName = 0x01B995E8;   // char[10]
        internal const uint ManagerLastName = 0x01B995F2;    // char[16]
        internal const int ManagerFirstNameSize = 10;
        internal const int ManagerLastNameSize = 16;

        // --- Trainer list (FUN_006317C6): 0x54 bytes per trainer, numbers 1 to 0x5DB; first and last name
        // as name table indices (words at +0 and +2). Number 0 is "Der Teamchef".
        internal const uint TrainerList = 0x00BE3940;
        internal const int TrainerSize = 0x54;
        internal const int TrainerSlots = 0x5DC;
        internal const uint ManagerClubCountry = 0x01B99606;  // word
        internal const uint ManagerClubNumber = 0x01B99608;   // word, number within the country
        internal const uint ManagerSalary = 0x01B99638;       // dword, DM per year; first field of the contract
        // The contract: 0x20 bytes from ManagerSalary, see ContractOffset.
        internal const uint ManagerData = 0x00A62578;         // CDatenMitspieler of manager 1, ManagerDataStride bytes per manager
        internal const int ManagerDataContract = 0x17C;       // see ManagerDataMap
        internal const int ContractSize = 0x20;
        internal const uint ManagerPrivateMoney = 0x01B99680; // dword, DM
        internal const uint ManagerMediaPopularity = 0x01B99622;  // byte 0-100 (Beliebtheit bei den Medien)
        internal const uint ManagerPublicPopularity = 0x01B99623; // byte 0-100 (in der Öffentlichkeit)
        internal const uint ManagerFanPopularity = 0x01B99624;    // byte 0-100, base of the fans' view (FUN_006A5A7D)
        // The fans see the base weighted by the manager's image (FUN_006A5A7D): (factor * 4 + 80) percent, the
        // factor from a table picked by the image level (+0x23) and its flags (+0x17D9, FUN_006A61E5).
        internal const uint ManagerImageLevel = 0x01B9960B;
        internal const uint ManagerImageFlags = 0x01B9ADC1;
        internal const uint ManagerFanFactors = 0x01B9ADEC;     // word every 0xE bytes, indexed by 0-15
        internal const int ManagerFanFactorStride = 0xE;
        internal const uint ManagerBoardTrust = 0x01B99684;       // word, base of the board's trust; the game adds the next 4 bytes (FUN_007701B2)
        internal const uint SeasonStartMoney = 0x00A6C1C8;    // ClubCopySeasonStartMoney of the own club's copy (slot 0, fixed at 0x00A6BFB8)

        // --- Calendar: current date = Calendar[CalendarBase + DayCounter] = (year, day of year).
        internal const uint BaseYear = 0x00B5CBCA;            // word, contract years are stored relative to it
        internal const uint DayCounter = 0x00B5CBD4;          // word
        internal const uint CalendarBase = 0x00B5CBE0;        // word
        internal const uint Calendar = 0x00B1F874;            // (word year, word day of year) per day
    }

    /// <summary>
    /// Offsets inside a manager's contract (manager object +0x50), in the order of the career score's
    /// contract page (texts 3168-3176).
    /// </summary>
    internal static class ContractOffset
    {
        internal const int Salary = 0x00;        // dword, DM per year (Festgehalt)
        internal const int PointBonus = 0x04;    // dword, DM per point
        internal const int CupBonus = 0x08;      // dword, DM per cup round
        internal const int TitleBonus = 0x0C;    // dword, DM per title or promotion
        internal const int GoalBonus = 0x10;     // dword, DM when the season goal is reached (FUN_0062292A)
        // 0x14: word, meaning unknown; compared with the level the club reached for the bonus (FUN_0062292A). Not the
        // season goal the career score shows, which hasn't been found yet.
        internal const int Years = 0x16;         // word, seasons left; the season change counts it down (FUN_00624AD4)
        internal const int ClubOption = 0x18;    // byte, club option to extend (Option Verein auf Verlängerung), seen on the career score
        internal const int ExitClause = 0x1C;    // byte, may leave on an offer while the contract runs (FUN_00442F85)
    }

    /// <summary>Offsets inside a CDatenSpieler record (0x354 bytes).</summary>
    internal static class RecordOffset
    {
        internal const int Used = 0x20;           // dword, 0 = empty slot
        internal const int ClubCountry = 0x30;
        internal const int ClubNumber = 0x31;
        internal const int Salary = 0x150;        // word, 1000 DM
        internal const int MatchBonus = 0x152;    // word, 100 DM
        internal const int GoalBonus = 0x154;     // word, 100 DM per goal
        internal const int ReleaseClause = 0x156; // word, 10,000 DM; 0 = none
        internal const int ContractEnd = 0x15C;   // word, year
        internal const int ContractFlags = 0x15E; // word, see ContractFlag
        internal const int BirthDay = 0x168;
        internal const int BirthMonth = 0x169;
        internal const int BirthYear = 0x16A;     // word
        internal const int Index = 0x1BC;         // word
        internal const int FirstName = 0x1BE;     // word, name table index
        internal const int LastName = 0x1C0;      // word, name table index
        internal const int ArtistName = 0x1C2;    // word, name table index, 0 = none
        internal const int Skills = 0x1C4;
        internal const int Skills2 = 0x1C6;
        internal const int Traits = 0x1C8;
        internal const int Character = 0x1CA;
        internal const int Condition = 0x1CE;     // word, x100
        internal const int Freshness = 0x1D0;     // word, x100
        internal const int InjuryStart = 0x1D8;   // word
        internal const int InjuryFrom = 0x1DA;    // word
        internal const int InjuryTo = 0x1DC;      // word
        internal const int Groggy = 0x1DE;        // word
        internal const int Health = 0x218;        // word: bits 0-6 one health type (Tables.HealthTypes), higher bits game state
        internal const int Strength = 0x21C;
        internal const int Position = 0x21D;
        internal const int SidePosition1 = 0x21E;
        internal const int SidePosition2 = 0x21F;
        internal const int Talent = 0x222;
        internal const int ShirtNumber = 0x223;
        internal const int Appearance = 0x21A;    // bits 0-2: skin colour, bits 4-7: beard (Tables.Beards)
        internal const int Hair = 0x21B;          // bits 0-3: hair colour, bits 4-7: hair type (Tables.HairColors / HairTypes)
        internal const int Nationality = 0x225;
        internal const int SecondNationality = 0x226; // 0 = none
        internal const int InjuryType = 0x228;
        internal const int InjuryFlag = 0x270;
        internal const int Flags = 0x1B0;         // dword, see CrowdFlag
        internal const int MoreFlags = 0x1B4;     // dword, see MoreFlag
        internal const int Morale = 0x296;
        internal const int FootAndReputation = 0x282; // bits 2-3: strong foot, higher bits see StatusFlag
        internal const int NationalTeam = 0x26F;  // bit 1: retired from the national team (StatusFlag.NationalTeamRetired)
        internal const int FormBase = 0x353;      // signed byte 0..20 (start 10), half of the shown form (FUN_006FF037)
        // Private life (the player info's "Privat" page, FUN_0055F1D8); record only.
        internal const int ChildBirthDates = 0x174; // 4 bytes per child like the birth date (day, month, word year)
        internal const int ChildCount = 0x1F6;    // word, 0-5
        internal const int Relationship = 0x27E;  // bits 0-1 Tables.Relationships, bits 2-4 Tables.RelationshipStates, higher bits game state
        internal const int PartnerName = 0x283;   // byte, index into the first-name list of NameCountry
        internal const int NameCountry = 0x286;   // first-name list for partner and children
        internal const int ChildNames = 0x288;    // one byte per child, like PartnerName
        internal const int Hobby = 0x28D;         // Tables.Hobbies, 0 = none
    }

    /// <summary>Bits of the contract flags (record +0x15E, working copy +0x96), see the player info (FUN_0055A57E).</summary>
    internal static class ContractFlag
    {
        internal const int ValidityMask = 0x0003;   // Tables.ContractValidity
        internal const int PlayerOption = 0x1000;   // Option Spieler auf Verlängerung
        internal const int ClubOption = 0x2000;     // Option Verein auf Verlängerung
        internal const int RegularPlace = 0x4000;   // Stammplatzgarantie
    }

    /// <summary>
    /// Status bits of the editor's "Daten 2" page, as the game imports them from the database
    /// (FUN_0068C903); meanings from the editor help.
    /// </summary>
    internal static class StatusFlag
    {
        // In RecordOffset.FootAndReputation (record only).
        internal const int HomegrownForeigner = 0x10; // Fußballinländer (database: nationality + 0x80)
        internal const int CaptainRetired = 0x40;     // Als Kapitän zurückgetreten
        internal const int NationalPlayer = 0x80;     // Nationalspieler (no naturalisation any more)
        // In RecordOffset.NationalTeam / CompactOffset.NationalTeam.
        internal const int NationalTeamRetired = 0x02; // Aus Nationalmannschaft zurückgetreten
    }

    /// <summary>Bits of the second flags dword (record +0x1B4); the others are game state.</summary>
    internal static class MoreFlag
    {
        internal const uint HasArtistName = 0x80000000; // database column 15; the game shows the artist name only with it
    }

    /// <summary>
    /// The crowd's current view of a player in the flags dword (record +0x1B0, working copy +0x20).
    /// The game recomputes it from his performance after every match (FUN_00770BAE); the lasting
    /// reputation in the low bits of RecordOffset.FootAndReputation is what the player info shows otherwise.
    /// </summary>
    internal static class CrowdFlag
    {
        internal const uint Hated = 0x400;
        internal const uint Darling = 0x10000;
    }

    /// <summary>Offsets inside the compact working copy (cache entry + 8), see FUN_00637177.</summary>
    internal static class CompactOffset
    {
        internal const int Index = 0x00;
        internal const int Strength = 0x02;
        internal const int Position = 0x03;
        internal const int SidePosition1 = 0x04;
        internal const int SidePosition2 = 0x05;
        internal const int ClubCountry = 0x06;
        internal const int ClubNumber = 0x07;
        internal const int Talent = 0x08;
        internal const int Skills = 0x0C;
        internal const int Skills2 = 0x0E;
        internal const int Traits = 0x10;
        internal const int Character = 0x12;
        internal const int Health = 0x14;
        internal const int Condition = 0x16;
        internal const int Freshness = 0x18;
        internal const int Nationality = 0x24;
        internal const int ShirtNumber = 0x26;
        internal const int InjuryStart = 0x2A;    // word
        internal const int InjuryDays = 0x2C;     // short, days until fit
        internal const int InjuryType = 0x2E;
        internal const int Groggy = 0x2F;
        internal const int Salary = 0x88;
        internal const int MatchBonus = 0x8A;
        internal const int GoalBonus = 0x8C;
        internal const int ReleaseClause = 0x8E;
        internal const int ContractEnd = 0x94;    // byte, year - BaseYear
        internal const int ContractFlags = 0x96;
        internal const int NationalTeam = 0xB3;   // copy of RecordOffset.NationalTeam (FUN_00636638)
        internal const int Flags = 0x20;          // dword, see RecordOffset.Flags
        internal const int Morale = 0xCA;
        internal const int BirthDate = 0xCC;      // dword: day, month, year (word); copy of record +0x168 (FUN_00636638)
    }
}
