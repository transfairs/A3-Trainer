namespace A3_Trainer.Model
{
    /// <summary>A human manager's career as the game's "Trainererfolge" page shows it.</summary>
    internal sealed class ManagerCareer
    {
        /// <summary>The human manager (1-4).</summary>
        internal int Manager { get; set; } = 1;
        /// <summary>Title counters in the order of <see cref="Controller.A3Addresses.CareerTitles"/>.</summary>
        internal int[] Titles { get; set; } = new int[Controller.A3Addresses.CareerTitles.Length];
        /// <summary>Times dismissed.</summary>
        internal int Dismissed { get; set; }
        /// <summary>Times the manager left on his own.</summary>
        internal int Resigned { get; set; }
        /// <summary>Number of stations (shown only).</summary>
        internal int Stations { get; set; }
        /// <summary>Years as a manager.</summary>
        internal int Years { get; set; }
        /// <summary>Games as trainer: league, cup, European cup, internationals.</summary>
        internal int[] Games { get; set; } = new int[4];
        /// <summary>Wins in the same order.</summary>
        internal int[] Wins { get; set; } = new int[4];

        /// <summary>A copy to edit.</summary>
        internal ManagerCareer Clone()
        {
            var copy = (ManagerCareer)this.MemberwiseClone();
            copy.Titles = (int[])this.Titles.Clone();
            copy.Games = (int[])this.Games.Clone();
            copy.Wins = (int[])this.Wins.Clone();
            return copy;
        }
    }
}
