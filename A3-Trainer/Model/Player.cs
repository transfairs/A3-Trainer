namespace A3_Trainer.Model
{
    /// <summary>
    /// One player as the trainer shows and edits it. Values are taken from the working copy when
    /// the game has one (own squad, next opponent), otherwise from the player's record.
    /// </summary>
    internal sealed class Player
    {
        internal int Index { get; set; }
        internal string FirstName { get; set; } = "";
        internal string LastName { get; set; } = "";
        internal string FullName => (this.FirstName + " " + this.LastName).Trim();

        internal int ClubCountry { get; set; }
        internal int ClubNumber { get; set; }
        internal GameDate BirthDate { get; set; }
        internal int Nationality { get; set; }

        internal int Strength { get; set; }
        internal int Position { get; set; }
        internal int SidePosition1 { get; set; }
        internal int SidePosition2 { get; set; }
        internal int Talent { get; set; }
        internal int ShirtNumber { get; set; }
        internal int Foot { get; set; }

        internal int Skills { get; set; }
        internal int Skills2 { get; set; }
        internal int Traits { get; set; }
        internal int Character { get; set; }

        /// <summary>Stored as hundredths (5509 = 55.09).</summary>
        internal int Condition { get; set; }
        /// <summary>Stored as hundredths.</summary>
        internal int Freshness { get; set; }
        internal int Morale { get; set; }

        /// <summary>Days until fit; only known for players with a working copy.</summary>
        internal int InjuryDays { get; set; }
        internal int InjuryType { get; set; }
        internal bool Groggy { get; set; }
        /// <summary>Whether the record shows an injury (for players without a working copy).</summary>
        internal bool RecordInjured { get; set; }

        /// <summary>1000 DM per year.</summary>
        internal int Salary { get; set; }
        /// <summary>100 DM per match.</summary>
        internal int MatchBonus { get; set; }
        internal int ContractEndYear { get; set; }

        /// <summary>Address of the working copy's cache entry, or 0 if the game has none.</summary>
        internal uint CacheEntry { get; set; }
        internal bool HasWorkingCopy => this.CacheEntry != 0;

        internal bool IsInjured => this.HasWorkingCopy ? this.InjuryDays > 0 : this.RecordInjured;

        internal Player Clone() => (Player)this.MemberwiseClone();
    }
}
