namespace A3_Trainer.Model
{
    /// <summary>A club's president, opposition leader, founding date and eternal table, as the game's club editor sets them.</summary>
    internal sealed class ClubBoard
    {
        /// <summary>The club.</summary>
        internal ClubKey Key { get; set; }
        /// <summary>The president.</summary>
        internal BoardMember President { get; set; } = new BoardMember();
        /// <summary>The leader of the opposition, who becomes president if it takes over.</summary>
        internal BoardMember Opposition { get; set; } = new BoardMember();
        /// <summary>Date the club was founded.</summary>
        internal GameDate Founded { get; set; }
        /// <summary>Eternal table of the first division: goals scored.</summary>
        internal int Goals { get; set; }
        /// <summary>Eternal table: goals conceded.</summary>
        internal int Conceded { get; set; }
        /// <summary>Eternal table: games.</summary>
        internal int Games { get; set; }
        /// <summary>Eternal table: points.</summary>
        internal int Points { get; set; }

        /// <summary>A copy to edit.</summary>
        internal ClubBoard Clone()
        {
            var copy = (ClubBoard)this.MemberwiseClone();
            copy.President = this.President.Clone();
            copy.Opposition = this.Opposition.Clone();
            return copy;
        }
    }

    /// <summary>A person of a club's board: name and birthday.</summary>
    internal sealed class BoardMember
    {
        /// <summary>First name.</summary>
        internal string FirstName { get; set; } = "";
        /// <summary>Last name.</summary>
        internal string LastName { get; set; } = "";
        /// <summary>Birthday.</summary>
        internal GameDate Birthday { get; set; }

        /// <summary>A copy to edit.</summary>
        internal BoardMember Clone() => (BoardMember)this.MemberwiseClone();
    }
}
