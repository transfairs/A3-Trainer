namespace A3_Trainer.Model
{
    /// <summary>
    /// A running investment of a club. Its value is base * the partner's index now / the index at purchase
    /// (FUN_00647D30); selling books the value minus the amount paid in as gain or loss.
    /// </summary>
    internal sealed class Investment
    {
        /// <summary>The slot (0-5) in the club's finances.</summary>
        internal int Slot { get; set; }
        /// <summary>Index into <see cref="Tables.InvestmentPartners"/>.</summary>
        internal int Partner { get; set; }
        /// <summary>DM paid in.</summary>
        internal long Paid { get; set; }
        /// <summary>DM the value is scaled from.</summary>
        internal long Base { get; set; }
        /// <summary>The partner's index when bought.</summary>
        internal int IndexAtPurchase { get; set; }

        /// <summary>The value at the partner's current <paramref name="index"/>, as the game computes it.</summary>
        internal long ValueAt(int index) => this.IndexAtPurchase == 0 ? this.Base : this.Base * index / this.IndexAtPurchase;

        /// <summary>The base that gives <paramref name="value"/> at the partner's current <paramref name="index"/>.</summary>
        internal long BaseFor(long value, int index) => this.IndexAtPurchase == 0 || index == 0 ? value : value * this.IndexAtPurchase / index;
    }
}
