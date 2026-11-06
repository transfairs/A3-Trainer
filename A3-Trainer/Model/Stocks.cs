namespace A3_Trainer.Model
{
    /// <summary>A stock on the game's market: a club that is an AG or a company.</summary>
    internal sealed class StockQuote
    {
        /// <summary>Position in the market's list, which the depots refer to.</summary>
        internal int Index { get; set; }
        /// <summary>Name as the market shows it.</summary>
        internal string Name { get; set; } = "";
        /// <summary>The club whose stock it is, or null for a company.</summary>
        internal ClubKey? Club { get; set; }
        /// <summary>Shares issued.</summary>
        internal int Shares { get; set; }
        /// <summary>Price of the current week.</summary>
        internal float Price { get; set; }

        public override string ToString() => this.Name;
    }

    /// <summary>A position in a human manager's depot.</summary>
    internal sealed class DepotPosition
    {
        /// <summary>Slot in the depot (0-49).</summary>
        internal int Slot { get; set; }
        /// <summary>Index of the stock in the market's list.</summary>
        internal int Stock { get; set; }
        /// <summary>Number of shares held.</summary>
        internal int Shares { get; set; }
        /// <summary>Price paid per share.</summary>
        internal float PurchasePrice { get; set; }
    }
}
