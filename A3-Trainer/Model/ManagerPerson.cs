using A3_Trainer.Controller;
using System;
using System.Linq;

namespace A3_Trainer.Model
{
    /// <summary>A human manager as a person: birthday, manager level and competencies.</summary>
    internal sealed class ManagerPerson
    {
        /// <summary>The manager's number, 1 to 4.</summary>
        internal int Manager { get; set; } = 1;
        /// <summary>Date of birth.</summary>
        internal GameDate Birthday { get; set; }
        /// <summary>The stored manager level; see <see cref="LevelInForce"/>.</summary>
        internal int Level { get; set; }
        /// <summary>Flags that raise or lower the level in force (game state, not edited).</summary>
        internal int LevelFlags { get; set; }
        /// <summary>The competency table: a row of Tables.Competencies values for each level.</summary>
        internal int[][] Competencies { get; set; } = Enumerable.Range(0, A3Addresses.ManagerLevels).Select(_ => new int[Tables.Competencies.Length]).ToArray();

        /// <summary>The level the game works with for a stored <paramref name="level"/>.</summary>
        internal int LevelInForce(int level) => InForce(level, this.LevelFlags);

        /// <summary>The level the game works with (FUN_006A61E5): the stored one plus bonuses for the flags, 0 to 15.</summary>
        internal static int InForce(int level, int flags)
        {
            if (level < 12)
            {
                int[] bonus = { 0, -1, 3, 3, 1, 2, 1, 2 };   // flags 0x01 .. 0x80
                for (int bit = 1; bit < 8; bit++)
                    if ((flags & (1 << bit)) != 0)
                        level += bonus[bit];
                level = Math.Min(level, 12);
            }
            else if ((flags & 2) != 0)
                level--;
            return Math.Min(Math.Max(level, 0), A3Addresses.ManagerLevels - 1);
        }

        /// <summary>A copy to edit without changing this one.</summary>
        internal ManagerPerson Clone()
        {
            var copy = (ManagerPerson)this.MemberwiseClone();
            copy.Competencies = this.Competencies.Select(row => (int[])row.Clone()).ToArray();
            return copy;
        }
    }
}
