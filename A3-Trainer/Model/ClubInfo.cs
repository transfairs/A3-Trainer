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

    /// <summary>Club and manager data shown on the club page (human manager 1).</summary>
    internal sealed class ClubInfo
    {
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
        /// <summary>Club image, 10 to 50 (Tables.ClubImage).</summary>
        internal int Image { get; set; }
        /// <summary>Trouble byte: bits 0-4 trouble with the association (Tables.Association), the rest game state.</summary>
        internal int Trouble { get; set; }

        /// <summary>Manager 1's first name.</summary>
        internal string ManagerFirstName { get; set; } = "";
        /// <summary>Manager 1's last name.</summary>
        internal string ManagerLastName { get; set; } = "";
        /// <summary>Manager 1's fixed salary, DM per year.</summary>
        internal int ManagerSalary { get; set; }
        /// <summary>Manager 1's private money, DM.</summary>
        internal int ManagerPrivateMoney { get; set; }
        /// <summary>Manager 1's popularity with the media, 0 to 100.</summary>
        internal int MediaPopularity { get; set; }
        /// <summary>Manager 1's popularity with the public, 0 to 100.</summary>
        internal int PublicPopularity { get; set; }
        /// <summary>Base of manager 1's popularity with the fans, 0 to 100 (the game scales it by the team's mood).</summary>
        internal int FanPopularity { get; set; }
        /// <summary>Percent the game weights the fans' base with (FUN_006A5A7D).</summary>
        internal int FanWeight { get; set; } = 100;
        /// <summary>Manager 1's popularity with the fans as the game shows it, 0 to 100.</summary>
        internal int ShownFanPopularity => ShownFans(this.FanPopularity);
        /// <summary>What the game would show for a fans' base of <paramref name="value"/>.</summary>
        internal int ShownFans(int value) => System.Math.Min(System.Math.Max(value * this.FanWeight / 100, 0), 100);
        /// <summary>Base of the board's trust in manager 1.</summary>
        internal int BoardTrustBase { get; set; }
        /// <summary>What the game adds to the base (FUN_007701B2).</summary>
        internal int BoardTrustBonus { get; set; }
        /// <summary>The board's trust as the game shows it, 0 to 100 percent.</summary>
        internal int BoardTrust => System.Math.Min(System.Math.Max(this.BoardTrustBase + this.BoardTrustBonus, 0), 100);

        /// <summary>A copy to edit without changing this one.</summary>
        internal ClubInfo Clone() => (ClubInfo)this.MemberwiseClone();
    }
}
