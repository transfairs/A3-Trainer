using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>A club's stadium, laid out like the game's stadium page: places, roof, track, heaters and condition per tribune, comfort.</summary>
    internal sealed class StadiumView : TrainerView
    {
        private readonly A3Game game;

        private readonly TextBox stadiumName = new TextBox { MaxLength = StadiumOffset.NameSize - 1 };
        private readonly TextBox city = new TextBox { MaxLength = StadiumOffset.CitySize - 1 };
        private readonly Label capacity = new Label();
        private readonly NumericUpDown[] vip = Places();
        private readonly NumericUpDown[] seats = Places();
        private readonly NumericUpDown[] standing = Places();
        private readonly CheckBox[] heaters = Checks();
        private readonly CheckBox[] roofs = Checks();
        private readonly CheckBox[] tracks = Checks();
        private readonly ComboBox[] conditions = Enumerable.Range(0, StadiumAddress.Tribunes).Select(_ => DropDown(Tables.StadiumConditions)).ToArray();
        private readonly ComboBox scoreboard = DropDown(Tables.Scoreboards);
        private readonly CheckBox undersoilHeating = new CheckBox { Text = "Rasenheizung", Tag = StadiumComfort.UndersoilHeating };
        private readonly CheckBox heatedSeats = new CheckBox { Text = "beheizbare Sitze", Tag = StadiumComfort.HeatedSeats };
        private readonly CheckBox retractablePitch = new CheckBox { Text = "ausfahrbares Spielfeld", Tag = StadiumComfort.RetractablePitch };
        private readonly CheckBox seatCushions = new CheckBox { Text = "Sitzkissen", Tag = StadiumComfort.SeatCushions };
        private readonly CheckBox luxuryBoxes = new CheckBox { Text = "Luxuskabinen", Tag = StadiumComfort.LuxuryBoxes };
        private readonly CheckBox floodlight = new CheckBox { Text = "Flutlicht", Tag = StadiumComfort.Floodlight };
        private readonly Button save = new Button { Text = "Speichern" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private Stadium shown;

        /// <summary>Creates the stadium page, reading through <paramref name="game"/>.</summary>
        internal StadiumView(ProcessMemory memory, A3Game game) : base(memory)
        {
            this.game = game;
            this.Size = new Size(630, 423);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.BuildLayout();
            A3Theme.Apply(this);
        }

        protected override string GameDisplayName => "Anstoss-3";

        /// <summary>The club whose stadium the page shows.</summary>
        internal ClubKey Club { get; set; }

        private CheckBox[] Comfort => new[] { this.undersoilHeating, this.heatedSeats, this.retractablePitch, this.seatCushions, this.luxuryBoxes, this.floodlight };

        private void BuildLayout()
        {
            var stadium = new GroupBox { Text = "Stadion", Bounds = new Rectangle(8, 8, 614, 76) };
            stadium.Controls.Add(new Label { Text = "Name", Bounds = new Rectangle(12, 23, 60, 18) });
            this.stadiumName.SetBounds(80, 20, 220, 21);
            stadium.Controls.Add(new Label { Text = "Ort", Bounds = new Rectangle(12, 49, 60, 18) });
            this.city.SetBounds(80, 46, 220, 21);
            stadium.Controls.Add(new Label { Text = "Kapazität gesamt", Bounds = new Rectangle(330, 23, 110, 18) });
            this.capacity.SetBounds(445, 23, 150, 18);
            stadium.Controls.AddRange(new Control[] { this.stadiumName, this.city, this.capacity });
            this.Controls.Add(stadium);

            var tribunes = new GroupBox { Text = "Tribünen", Bounds = new Rectangle(8, 90, 614, 160) };
            string[] headers = { "VIP-Plätze", "Sitzplätze", "Stehplätze", "Wärmestr.", "Dach", "Laufbahn", "Zustand" };
            int[] columns = { 110, 185, 260, 340, 405, 455, 515 };
            for (int c = 0; c < headers.Length; c++)
                tribunes.Controls.Add(new Label { Text = headers[c], Bounds = new Rectangle(columns[c], 18, c + 1 < columns.Length ? columns[c + 1] - columns[c] - 2 : 88, 16) });
            for (int t = 0; t < StadiumAddress.Tribunes; t++)
            {
                int y = 38 + t * 28;
                tribunes.Controls.Add(new Label { Text = Tables.Tribunes[t], Bounds = new Rectangle(12, y + 3, 95, 18) });
                this.vip[t].SetBounds(columns[0], y, 66, 21);
                this.seats[t].SetBounds(columns[1], y, 66, 21);
                this.standing[t].SetBounds(columns[2], y, 66, 21);
                this.heaters[t].SetBounds(columns[3] + 18, y + 2, 18, 18);
                this.roofs[t].SetBounds(columns[4] + 10, y + 2, 18, 18);
                this.tracks[t].SetBounds(columns[5] + 18, y + 2, 18, 18);
                this.conditions[t].SetBounds(columns[6], y, 88, 21);
                tribunes.Controls.AddRange(new Control[] { this.vip[t], this.seats[t], this.standing[t], this.heaters[t], this.roofs[t], this.tracks[t], this.conditions[t] });
                foreach (NumericUpDown places in new[] { this.vip[t], this.seats[t], this.standing[t] })
                    places.ValueChanged += (s, e) => this.ShowCapacity();
            }
            this.Controls.Add(tribunes);

            var comfort = new GroupBox { Text = "Stadionkomfort", Bounds = new Rectangle(8, 256, 614, 110) };
            comfort.Controls.Add(new Label { Text = "Anzeigetafel", Bounds = new Rectangle(12, 23, 90, 18) });
            this.scoreboard.SetBounds(110, 20, 220, 21);
            comfort.Controls.Add(this.scoreboard);
            CheckBox[] checks = this.Comfort;
            for (int i = 0; i < checks.Length; i++)
            {
                checks[i].SetBounds(12 + (i % 3) * 200, 52 + (i / 3) * 26, 190, 20);
                comfort.Controls.Add(checks[i]);
            }
            this.Controls.Add(comfort);

            this.save.SetBounds(8, 374, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(124, 374, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(240, 378, 380, 20);
            this.Controls.AddRange(new Control[] { this.save, this.reload, this.status });
        }

        /// <summary>Reads the stadium from the game (or clears the view if there is none).</summary>
        internal void RefreshValues()
        {
            this.shown = this.IsGameRunning(silent: true) ? this.game.ReadStadium(this.Club) : null;
            this.save.Enabled = this.shown != null;
            if (this.shown == null)
            {
                this.ClearAllFields(this);
                this.capacity.Text = "";
                this.status.Text = "Kein Stadion gefunden";
                return;
            }

            this.stadiumName.Text = this.shown.Name;
            this.city.Text = this.shown.City;
            for (int t = 0; t < StadiumAddress.Tribunes; t++)
            {
                SetPlaces(this.vip[t], this.shown.Vip[t]);
                SetPlaces(this.seats[t], this.shown.Seats[t]);
                SetPlaces(this.standing[t], this.shown.Standing[t]);
                this.roofs[t].Checked = (this.shown.TribuneBits & 1 << t) != 0;
                this.tracks[t].Checked = (this.shown.TribuneBits & 0x10 << t) != 0;
                this.heaters[t].Checked = (this.shown.TribuneBits & 0x100 << t) != 0;
                int level = this.shown.Condition[t] / 5;
                this.conditions[t].SelectedIndex = level < Tables.StadiumConditions.Length ? level : -1;
            }
            this.scoreboard.SelectedIndex = this.shown.Scoreboard < Tables.Scoreboards.Length ? this.shown.Scoreboard : -1;
            foreach (CheckBox check in this.Comfort)
                check.Checked = (this.shown.Comfort & (int)check.Tag) != 0;
            this.ShowCapacity();
            this.status.Text = "";
        }

        private void ShowCapacity()
        {
            int sum = this.vip.Concat(this.seats).Concat(this.standing).Sum(n => (int)n.Value);
            this.capacity.Text = sum.ToString("N0");
        }

        private void Save()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;

            Stadium edited = this.shown.Clone();
            edited.Name = this.stadiumName.Text.Trim().Length > 0 ? this.stadiumName.Text.Trim() : this.shown.Name;
            edited.City = this.city.Text.Trim().Length > 0 ? this.city.Text.Trim() : this.shown.City;
            int bits = edited.TribuneBits & ~0xFFF;
            for (int t = 0; t < StadiumAddress.Tribunes; t++)
            {
                edited.Vip[t] = (int)this.vip[t].Value;
                edited.Seats[t] = (int)this.seats[t].Value;
                edited.Standing[t] = (int)this.standing[t].Value;
                bits |= (this.roofs[t].Checked ? 1 << t : 0) | (this.tracks[t].Checked ? 0x10 << t : 0) | (this.heaters[t].Checked ? 0x100 << t : 0);
                // A condition keeps its exact value unless another level is picked.
                if (this.conditions[t].SelectedIndex >= 0 && this.conditions[t].SelectedIndex != this.shown.Condition[t] / 5)
                    edited.Condition[t] = this.conditions[t].SelectedIndex * 5;
            }
            edited.TribuneBits = bits;
            if (this.scoreboard.SelectedIndex >= 0)
                edited.Scoreboard = this.scoreboard.SelectedIndex;
            foreach (CheckBox check in this.Comfort)
                edited.Comfort = check.Checked ? edited.Comfort | (int)check.Tag : edited.Comfort & ~(int)check.Tag;
            try
            {
                this.game.SaveStadium(this.shown, edited);
                Logger.Debug("Stadion gespeichert");
                this.RefreshValues();
                this.status.Text = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Stadion konnte nicht gespeichert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
        }

        // Places are signed words in the stadium.
        private static NumericUpDown[] Places() =>
            Enumerable.Range(0, StadiumAddress.Tribunes).Select(_ => new NumericUpDown { Minimum = 0, Maximum = short.MaxValue, ThousandsSeparator = true }).ToArray();

        private static CheckBox[] Checks() => Enumerable.Range(0, StadiumAddress.Tribunes).Select(_ => new CheckBox()).ToArray();

        private static ComboBox DropDown(string[] items)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(items);
            return combo;
        }

        // Setting the value it already has doesn't redraw the text, which ClearAllFields may have emptied.
        private static void SetPlaces(NumericUpDown places, int value)
        {
            places.Value = Math.Min(Math.Max(value, 0), (int)places.Maximum);
            places.Text = places.Value.ToString("N0");
        }
    }
}
