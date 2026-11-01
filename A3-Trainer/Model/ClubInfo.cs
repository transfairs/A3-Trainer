namespace A3_Trainer.Model
{
    /// <summary>A club as identified by the game: country plus number within that country.</summary>
    internal struct ClubKey
    {
        /// <summary>Country number.</summary>
        internal int Country { get; }
        /// <summary>Club number within the country.</summary>
        internal int Number { get; }

        /// <summary>A club key from country and club number.</summary>
        internal ClubKey(int country, int number)
        {
            this.Country = country;
            this.Number = number;
        }

        public override bool Equals(object obj) => obj is ClubKey other && other.Country == this.Country && other.Number == this.Number;
        public override int GetHashCode() => (this.Country << 8) ^ this.Number;
        public override string ToString() => $"{this.Country}/{this.Number}";
    }

    /// <summary>Club and manager data shown on the club and manager pages, for one human manager.</summary>
    internal sealed class ClubInfo
    {
        /// <summary>The manager's number, 1 to 4.</summary>
        internal int Manager { get; set; } = 1;
        /// <summary>The club's key.</summary>
        internal ClubKey Key { get; set; }
        /// <summary>Club name.</summary>
        internal string Name { get; set; } = "";
        /// <summary>Short name (up to four letters).</summary>
        internal string ShortName { get; set; } = "";
        /// <summary>Home city.</summary>
        internal string City { get; set; } = "";
        /// <summary>DM; the bank balance is this plus the season's result so far.</summary>
        internal int SeasonStartMoney { get; set; }
        /// <summary>Whether the club has a contract for its advertising boards.</summary>
        internal bool HasAdsContract { get; set; }
        /// <summary>DM per season from the advertising boards contract.</summary>
        internal int Ads { get; set; }
        /// <summary>Whether the club has an outfitter.</summary>
        internal bool HasOutfitter { get; set; }
        /// <summary>DM per season from the outfitter.</summary>
        internal int Outfitter { get; set; }
        /// <summary>Club image, 10 to 50 (Tables.ClubImage).</summary>
        internal int Image { get; set; }
        /// <summary>Trouble byte: bits 0-4 trouble with the association (Tables.Association), the rest game state.</summary>
        internal int Trouble { get; set; }

        /// <summary>The manager's first name.</summary>
        internal string ManagerFirstName { get; set; } = "";
        /// <summary>The manager's last name.</summary>
        internal string ManagerLastName { get; set; } = "";
        /// <summary>The manager's fixed salary, DM per year.</summary>
        internal int ManagerSalary { get; set; }
        /// <summary>The manager's bonus per point, DM.</summary>
        internal int PointBonus { get; set; }
        /// <summary>The manager's bonus per cup round, DM.</summary>
        internal int CupBonus { get; set; }
        /// <summary>The manager's bonus per title or promotion, DM.</summary>
        internal int TitleBonus { get; set; }
        /// <summary>The manager's bonus for reaching the season goal, DM.</summary>
        internal int GoalBonus { get; set; }
        /// <summary>The club's season goal (Tables.SeasonGoals), shown with the manager's contract.</summary>
        internal int SeasonGoal { get; set; }
        /// <summary>Seasons left on the manager's contract.</summary>
        internal int ContractYears { get; set; }
        /// <summary>Whether the manager may leave on an offer while the contract runs.</summary>
        internal bool ExitClause { get; set; }
        /// <summary>The manager's private money, DM.</summary>
        internal int ManagerPrivateMoney { get; set; }
        /// <summary>The manager's popularity with the media, 0 to 100.</summary>
        internal int MediaPopularity { get; set; }
        /// <summary>The manager's popularity with the public, 0 to 100.</summary>
        internal int PublicPopularity { get; set; }
        /// <summary>Base of the manager's popularity with the fans, 0 to 100 (the game scales it by the team's mood).</summary>
        internal int FanPopularity { get; set; }
        /// <summary>Percent the game weights the fans' base with (FUN_006A5A7D).</summary>
        internal int FanWeight { get; set; } = 100;
        /// <summary>The manager's popularity with the fans as the game shows it, 0 to 100.</summary>
        internal int ShownFanPopularity => ShownFans(this.FanPopularity);
        /// <summary>What the game would show for a fans' base of <paramref name="value"/>.</summary>
        internal int ShownFans(int value) => System.Math.Min(System.Math.Max(value * this.FanWeight / 100, 0), 100);
        /// <summary>Base of the board's trust in the manager.</summary>
        internal int BoardTrustBase { get; set; }
        /// <summary>What the game adds to the base (FUN_007701B2).</summary>
        internal int BoardTrustBonus { get; set; }
        /// <summary>The board's trust as the game shows it, 0 to 100 percent.</summary>
        internal int BoardTrust => System.Math.Min(System.Math.Max(this.BoardTrustBase + this.BoardTrustBonus, 0), 100);

        /// <summary>A copy to edit without changing this one.</summary>
        internal ClubInfo Clone() => (ClubInfo)this.MemberwiseClone();
    }
}
