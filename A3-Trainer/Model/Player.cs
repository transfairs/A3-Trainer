namespace A3_Trainer.Model
{
    /// <summary>
    /// One player as the trainer shows and edits it. Values are taken from the working copy when
    /// the game has one (own squad, next opponent), otherwise from the player's record.
    /// </summary>
    internal sealed class Player
    {
        /// <summary>The player's number (index in the player records).</summary>
        internal int Index { get; set; }
        /// <summary>First name from the game's name table.</summary>
        internal string FirstName { get; set; } = "";
        /// <summary>Last name from the game's name table.</summary>
        internal string LastName { get; set; } = "";
        /// <summary>First and last name.</summary>
        internal string FullName => (this.FirstName + " " + this.LastName).Trim();
        /// <summary>"Nachname, Vorname" as the game's squad list and the A2G trainer show it.</summary>
        internal string ListName =>
            this.FirstName.Length == 0 ? this.LastName : this.LastName.Length == 0 ? this.FirstName : this.LastName + ", " + this.FirstName;

        /// <summary>Country number of the player's club.</summary>
        internal int ClubCountry { get; set; }
        /// <summary>Club number within that country.</summary>
        internal int ClubNumber { get; set; }
        /// <summary>Date of birth (invalid for empty slots).</summary>
        internal GameDate BirthDate { get; set; }
        /// <summary>Nationality (country number).</summary>
        internal int Nationality { get; set; }
        /// <summary>0 = none.</summary>
        internal int SecondNationality { get; set; }
        /// <summary>0 hell, 1 dunkel, 2 schwarz, 3 asiatisch.</summary>
        internal int SkinColor { get; set; }

        /// <summary>Record flags (+0x1B0): 0x400 = hated by the crowd, 0x10000 = darling.</summary>
        internal uint Flags { get; set; }
        /// <summary>Whether the crowd loves the player.</summary>
        internal bool Darling => (this.Flags & 0x10000) != 0;
        /// <summary>Whether the crowd hates the player.</summary>
        internal bool Hated => (this.Flags & 0x400) != 0;

        /// <summary>Strength, 1 to 13.</summary>
        internal int Strength { get; set; }
        /// <summary>Main position (1 TO to 10 ST, see Tables.Positions).</summary>
        internal int Position { get; set; }
        /// <summary>First secondary position, 0 = none.</summary>
        internal int SidePosition1 { get; set; }
        /// <summary>Second secondary position, 0 = none.</summary>
        internal int SidePosition2 { get; set; }
        /// <summary>Talent, 0 (riesig) to 4 (keins).</summary>
        internal int Talent { get; set; }
        /// <summary>Shirt number.</summary>
        internal int ShirtNumber { get; set; }
        /// <summary>Strong foot: 1 right, 2 left, 3 both.</summary>
        internal int Foot { get; set; }

        /// <summary>Skill bits, the editor's "+" column.</summary>
        internal int Skills { get; set; }
        /// <summary>Second set of skill bits, the editor's "−" column.</summary>
        internal int Skills2 { get; set; }
        /// <summary>Trait bits (Tables.Traits).</summary>
        internal int Traits { get; set; }
        /// <summary>Character bits (Tables.Character).</summary>
        internal int Character { get; set; }

        /// <summary>Stored as hundredths (5509 = 55.09).</summary>
        internal int Condition { get; set; }
        /// <summary>Stored as hundredths.</summary>
        internal int Freshness { get; set; }
        /// <summary>Motivation as the game last computed it (shown only).</summary>
        internal int Morale { get; set; }

        /// <summary>Days until fit; only known for players with a working copy.</summary>
        internal int InjuryDays { get; set; }
        /// <summary>Kind of injury.</summary>
        internal int InjuryType { get; set; }
        /// <summary>Whether the player is groggy (angeschlagen).</summary>
        internal bool Groggy { get; set; }
        /// <summary>Whether the record shows an injury (for players without a working copy).</summary>
        internal bool RecordInjured { get; set; }

        /// <summary>1000 DM per year.</summary>
        internal int Salary { get; set; }
        /// <summary>100 DM per match.</summary>
        internal int MatchBonus { get; set; }
        /// <summary>Year the contract ends (on 30 June).</summary>
        internal int ContractEndYear { get; set; }

        /// <summary>Address of the working copy's cache entry, or 0 if the game has none.</summary>
        internal uint CacheEntry { get; set; }
        /// <summary>Whether the game has a working copy of the player loaded.</summary>
        internal bool HasWorkingCopy => this.CacheEntry != 0;

        /// <summary>Whether the player is injured: from the working copy's days if loaded, else from the record.</summary>
        internal bool IsInjured => this.HasWorkingCopy ? this.InjuryDays > 0 : this.RecordInjured;

        /// <summary>A copy to edit without changing this one.</summary>
        internal Player Clone() => (Player)this.MemberwiseClone();
    }
}
