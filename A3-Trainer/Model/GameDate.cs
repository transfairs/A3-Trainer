using System;

namespace A3_Trainer.Model
{
    /// <summary>A calendar date of the game world (no time of day).</summary>
    internal struct GameDate
    {
        /// <summary>Year.</summary>
        internal int Year { get; }
        /// <summary>Month (1-12).</summary>
        internal int Month { get; }
        /// <summary>Day of the month.</summary>
        internal int Day { get; }

        /// <summary>A date from its parts, unchecked (see <see cref="IsValid"/> and <see cref="Exists"/>).</summary>
        internal GameDate(int year, int month, int day)
        {
            this.Year = year;
            this.Month = month;
            this.Day = day;
        }

        /// <summary>From the game calendar's (year, zero-based day of year) pair.</summary>
        internal static GameDate FromDayOfYear(int year, int dayOfYear)
        {
            DateTime date = new DateTime(year, 1, 1).AddDays(dayOfYear);
            return new GameDate(date.Year, date.Month, date.Day);
        }

        /// <summary>Whether the date is a real calendar date (records of empty slots hold zeros).</summary>
        internal bool IsValid => this.Year > 1800 && this.Month >= 1 && this.Month <= 12 && this.Day >= 1 && this.Day <= 31;

        /// <summary>Completed years between this (birth) date and <paramref name="today"/>.</summary>
        internal int AgeOn(GameDate today)
        {
            int age = today.Year - this.Year;
            if (today.Month < this.Month || (today.Month == this.Month && today.Day < this.Day))
                age--;
            return age;
        }

        /// <summary>Whether year, month and day form a date that exists in the calendar.</summary>
        internal bool Exists => this.Year >= 1 && this.Year <= 9999 && this.Month >= 1 && this.Month <= 12
            && this.Day >= 1 && this.Day <= DateTime.DaysInMonth(this.Year, this.Month);

        public override bool Equals(object obj) =>
            obj is GameDate other && other.Year == this.Year && other.Month == this.Month && other.Day == this.Day;

        public override int GetHashCode() => (this.Year << 9) ^ (this.Month << 5) ^ this.Day;

        public override string ToString() => $"{this.Day}.{this.Month}.{this.Year}";
    }
}
