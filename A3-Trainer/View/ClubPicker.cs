using A3_Trainer.Controller;
using A3_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>
    /// The country and club choice of the player and club pages: fills both combo boxes from the
    /// clubs that have players, selects the club shown and reports a club the user picks.
    /// </summary>
    internal sealed class ClubPicker
    {
        private readonly ComboBox countryBox;
        private readonly ComboBox clubBox;
        private ClubKey? shown;
        private bool filling;
        private Func<ClubKey, string> clubName = key => key.ToString();

        /// <summary>Raised when the user picks a club other than the one shown.</summary>
        internal event Action<ClubKey> Picked;

        /// <summary>Wires up the two combo boxes (DropDownList).</summary>
        internal ClubPicker(ComboBox countryBox, ComboBox clubBox)
        {
            this.countryBox = countryBox;
            this.clubBox = clubBox;
            this.countryBox.SelectedIndexChanged += (s, e) => this.CountryPicked();
            this.clubBox.SelectedIndexChanged += (s, e) => this.ClubPicked();
        }

        /// <summary>The clubs offered, in the game's club order.</summary>
        internal List<ClubKey> Clubs { get; private set; } = new List<ClubKey>();

        /// <summary>The countries offered, sorted by name (each with club number 0).</summary>
        internal IEnumerable<ClubChoice> Countries => this.countryBox.Items.Cast<ClubChoice>();

        /// <summary>
        /// Offers the game's clubs, the countries named after <paramref name="countryNames"/> (by
        /// country number), and selects <paramref name="shown"/>; empties both lists if shown is null.
        /// </summary>
        internal void Fill(A3Game game, IList<string> countryNames, ClubKey? shown)
        {
            this.shown = shown;
            this.filling = true;
            try
            {
                this.countryBox.Items.Clear();
                this.clubBox.Items.Clear();
                this.Clubs = new List<ClubKey>();
                if (!shown.HasValue)
                    return;
                this.clubName = game.ClubName;
                this.Clubs = game.ReadClubs();
                this.countryBox.Items.AddRange(this.Clubs.Select(c => c.Country).Distinct()
                    .Select(country => new ClubChoice(country < countryNames.Count ? countryNames[country] : country.ToString(), new ClubKey(country, 0)))
                    .OrderBy(c => c.Text, StringComparer.CurrentCulture).Cast<object>().ToArray());
                this.countryBox.SelectedItem = this.Countries.FirstOrDefault(c => c.Club.Country == shown.Value.Country);
                this.FillClubBox(shown.Value.Country);
            }
            finally
            {
                this.filling = false;
            }
        }

        /// <summary>The clubs of <paramref name="country"/> as choices, in the game's order.</summary>
        internal IEnumerable<ClubChoice> ClubsOf(int country) =>
            this.Clubs.Where(c => c.Country == country).Select(key => new ClubChoice(this.clubName(key), key));

        private void FillClubBox(int country)
        {
            this.clubBox.Items.Clear();
            this.clubBox.Items.AddRange(this.ClubsOf(country).Cast<object>().ToArray());
            this.clubBox.SelectedItem = this.clubBox.Items.Cast<ClubChoice>().FirstOrDefault(c => c.Club.Equals(this.shown));
        }

        // Another country shows its first club.
        private void CountryPicked()
        {
            if (this.filling || !(this.countryBox.SelectedItem is ClubChoice country))
                return;
            this.filling = true;
            try
            {
                this.FillClubBox(country.Club.Country);
                if (this.clubBox.SelectedIndex < 0 && this.clubBox.Items.Count > 0)
                    this.clubBox.SelectedIndex = 0;
            }
            finally
            {
                this.filling = false;
            }
            this.ClubPicked();
        }

        private void ClubPicked()
        {
            if (this.filling || !(this.clubBox.SelectedItem is ClubChoice choice) || choice.Club.Equals(this.shown))
                return;
            this.shown = choice.Club;
            this.Picked?.Invoke(choice.Club);
        }
    }

    /// <summary>An entry of a country or club selection.</summary>
    internal sealed class ClubChoice
    {
        /// <summary>A choice showing <paramref name="text"/> for <paramref name="club"/>.</summary>
        internal ClubChoice(string text, ClubKey club)
        {
            this.Text = text;
            this.Club = club;
        }

        /// <summary>The name shown.</summary>
        internal string Text { get; }
        /// <summary>The club (for a country: club number 0).</summary>
        internal ClubKey Club { get; }

        public override string ToString() => this.Text;
    }
}
