namespace A3_Trainer.Model
{
    /// <summary>A club's overdraft and loans.</summary>
    internal sealed class ClubFinances
    {
        /// <summary>The club.</summary>
        internal ClubKey Key { get; set; }
        /// <summary>Overdraft in DM (a multiple of 100,000).</summary>
        internal int Overdraft { get; set; }
        /// <summary>Number of running loans.</summary>
        internal int LoanCount { get; set; }
        /// <summary>DM owed for the running loans.</summary>
        internal long LoanTotal { get; set; }
        /// <summary>Whether the club is an AG (a stock company with shares on the market).</summary>
        internal bool IsPublic { get; set; }

        /// <summary>A copy to edit.</summary>
        internal ClubFinances Clone() => (ClubFinances)this.MemberwiseClone();
    }

    /// <summary>The interest rates every club pays or gets, in hundredths of a percent.</summary>
    internal sealed class InterestRates
    {
        /// <summary>Beyond the overdraft ("Zinssatz" on the balance page).</summary>
        internal int Interest { get; set; }
        /// <summary>Within the overdraft ("Dispokredit").</summary>
        internal int Overdraft { get; set; }
        /// <summary>On a positive balance.</summary>
        internal int Savings { get; set; }
        /// <summary>Base rate of new loans.</summary>
        internal int Loan { get; set; }
        /// <summary>On the overnight deposit (Tagesgeld) investment.</summary>
        internal int Deposit { get; set; }

        /// <summary>A copy to edit.</summary>
        internal InterestRates Clone() => (InterestRates)this.MemberwiseClone();
    }
}
