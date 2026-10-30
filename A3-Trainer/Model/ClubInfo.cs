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

        /// <summary>Manager 1's first name.</summary>
        internal string ManagerFirstName { get; set; } = "";
        /// <summary>Manager 1's last name.</summary>
        internal string ManagerLastName { get; set; } = "";
        /// <summary>Manager 1's fixed salary, DM per year.</summary>
        internal int ManagerSalary { get; set; }
        /// <summary>Manager 1's private money, DM.</summary>
        internal int ManagerPrivateMoney { get; set; }

        /// <summary>A copy to edit without changing this one.</summary>
        internal ClubInfo Clone() => (ClubInfo)this.MemberwiseClone();
    }
}
