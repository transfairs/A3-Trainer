using System.Linq;

namespace A3_Trainer.Model
{
    /// <summary>A club's stadium as the stadium page shows it; tribunes in the order of Tables.Tribunes.</summary>
    internal sealed class Stadium
    {
        /// <summary>Address of the stadium data in the game.</summary>
        internal uint Address { get; set; }
        /// <summary>Stadium name.</summary>
        internal string Name { get; set; } = "";
        /// <summary>City the stadium is in.</summary>
        internal string City { get; set; } = "";
        /// <summary>VIP places per tribune.</summary>
        internal int[] Vip { get; set; } = new int[4];
        /// <summary>Seats per tribune.</summary>
        internal int[] Seats { get; set; } = new int[4];
        /// <summary>Standing places per tribune.</summary>
        internal int[] Standing { get; set; } = new int[4];
        /// <summary>Condition byte per tribune (Tables.StadiumConditions level * 5).</summary>
        internal int[] Condition { get; set; } = new int[4];
        /// <summary>Roof, running track and heater bits per tribune (Controller.StadiumOffset.TribuneBits).</summary>
        internal int TribuneBits { get; set; }
        /// <summary>Comfort bits (Controller.StadiumComfort), the others are kept.</summary>
        internal int Comfort { get; set; }
        /// <summary>Scoreboard (Tables.Scoreboards).</summary>
        internal int Scoreboard { get; set; }

        /// <summary>All places, as the stadium page adds them up.</summary>
        internal int Capacity => this.Vip.Sum() + this.Seats.Sum() + this.Standing.Sum();

        /// <summary>A copy to edit without changing this one.</summary>
        internal Stadium Clone()
        {
            var copy = (Stadium)this.MemberwiseClone();
            copy.Vip = (int[])this.Vip.Clone();
            copy.Seats = (int[])this.Seats.Clone();
            copy.Standing = (int[])this.Standing.Clone();
            copy.Condition = (int[])this.Condition.Clone();
            return copy;
        }
    }
}
