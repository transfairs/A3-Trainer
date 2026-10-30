namespace A3_Trainer.Model
{
    /// <summary>A club as identified by the game: country plus number within that country.</summary>
    internal struct ClubKey
    {
        internal int Country { get; }
        internal int Number { get; }

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
        internal ClubKey Key { get; set; }
        internal string Name { get; set; } = "";
        internal string ShortName { get; set; } = "";
        internal string City { get; set; } = "";
        /// <summary>DM; the bank balance is this plus the season's result so far.</summary>
        internal int SeasonStartMoney { get; set; }

        internal string ManagerFirstName { get; set; } = "";
        internal string ManagerLastName { get; set; } = "";
        internal int ManagerSalary { get; set; }
        internal int ManagerPrivateMoney { get; set; }
    }
}
