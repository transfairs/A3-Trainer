namespace A3_Trainer.Model
{
    /// <summary>A club's fans, board, fan friendship and arch rival, as the game's club editor sets them.</summary>
    internal sealed class ClubIdentity
    {
        /// <summary>The club.</summary>
        internal ClubKey Key { get; set; }
        /// <summary>What the fans are called.</summary>
        internal string FanName { get; set; }
        /// <summary>Index into <see cref="Tables.FanCrowds"/>.</summary>
        internal int FanCrowd { get; set; }
        /// <summary>Index into <see cref="Tables.FanCrowds"/>: the best fan crowd the club can reach.</summary>
        internal int MaxFanCrowd { get; set; }
        /// <summary>Index into <see cref="Tables.Hooligans"/>.</summary>
        internal int Hooligans { get; set; }
        /// <summary>Index into <see cref="Tables.FinancialPowers"/>.</summary>
        internal int FinancialPower { get; set; }
        /// <summary>Whether the club's city is a media city.</summary>
        internal bool MediaCity { get; set; }
        /// <summary>Index into <see cref="Tables.Regions"/>.</summary>
        internal int Region { get; set; }
        /// <summary>Index into <see cref="Tables.FanTypes"/>.</summary>
        internal int FanType { get; set; }
        /// <summary>Index into <see cref="Tables.Boards"/>.</summary>
        internal int Board { get; set; }
        /// <summary>Number in the club's country of the club with a fan friendship, -1 = none.</summary>
        internal int Friend { get; set; }
        /// <summary>Number in the club's country of the arch rival, -1 = none.</summary>
        internal int Rival { get; set; }

        /// <summary>A copy to edit.</summary>
        internal ClubIdentity Clone() => (ClubIdentity)this.MemberwiseClone();
    }
}
