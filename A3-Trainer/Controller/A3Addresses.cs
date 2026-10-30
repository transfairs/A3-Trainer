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
        internal const int NameTableCount = 19866;

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
        internal const int ClubName = 0x20;
        internal const int ClubShortName = 0x34;
        internal const int ClubCity = 0x38;

        // --- Human manager 1 and the finances of his club.
        internal const uint ManagerFirstName = 0x01B995E8;
        internal const uint ManagerLastName = 0x01B995F2;
        internal const uint ManagerClubCountry = 0x01B99606;  // word
        internal const uint ManagerClubNumber = 0x01B99608;   // word, number within the country
        internal const uint ManagerSalary = 0x01B99638;       // dword, DM per year
        internal const uint ManagerPrivateMoney = 0x01B99680; // dword, DM
        internal const uint SeasonStartMoney = 0x00A6C1C8;    // dword, DM; bank balance = this + season result

        // --- Calendar: current date = Calendar[CalendarBase + DayCounter] = (year, day of year).
        internal const uint BaseYear = 0x00B5CBCA;            // word, contract years are stored relative to it
        internal const uint DayCounter = 0x00B5CBD4;          // word
        internal const uint CalendarBase = 0x00B5CBE0;        // word
        internal const uint Calendar = 0x00B1F874;            // (word year, word day of year) per day
    }

    /// <summary>Offsets inside a CDatenSpieler record (0x354 bytes).</summary>
    internal static class RecordOffset
    {
        internal const int Used = 0x20;           // dword, 0 = empty slot
        internal const int ClubCountry = 0x30;
        internal const int ClubNumber = 0x31;
        internal const int Salary = 0x150;        // word, 1000 DM
        internal const int MatchBonus = 0x152;    // word, 100 DM
        internal const int ContractEnd = 0x15C;   // word, year
        internal const int BirthDay = 0x168;
        internal const int BirthMonth = 0x169;
        internal const int BirthYear = 0x16A;     // word
        internal const int Index = 0x1BC;         // word
        internal const int FirstName = 0x1BE;     // word, name table index
        internal const int LastName = 0x1C0;      // word, name table index
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
        internal const int Strength = 0x21C;
        internal const int Position = 0x21D;
        internal const int SidePosition1 = 0x21E;
        internal const int SidePosition2 = 0x21F;
        internal const int Talent = 0x222;
        internal const int ShirtNumber = 0x223;
        internal const int Appearance = 0x21A;    // bits 0-2: skin colour
        internal const int Nationality = 0x225;
        internal const int SecondNationality = 0x226; // 0 = none
        internal const int InjuryType = 0x228;
        internal const int InjuryFlag = 0x270;
        internal const int Flags = 0x1B0;         // dword: 0x400 hated, 0x10000 darling
        internal const int Morale = 0x296;
        internal const int FootAndReputation = 0x282; // bits 2-3: strong foot
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
        internal const int ContractEnd = 0x94;    // byte, year - BaseYear
        internal const int Flags = 0x20;          // dword, see RecordOffset.Flags
        internal const int Morale = 0xCA;
        internal const int BirthDate = 0xCC;      // dword: day, month, year (word); copy of record +0x168 (FUN_00636638)
    }
}
