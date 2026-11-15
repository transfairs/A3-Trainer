using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>A human manager's career as on the game's "Trainererfolge" page: titles, stations, years and games.</summary>
    internal sealed class CareerView : TrainerView
    {
        private static readonly string[] Competitions = { "Liga", "Pokal", "Europapokal", "Länderspiele" };

        private readonly A3Game game;

        private readonly NumericUpDown[] titles = A3Addresses.CareerTitles.Select(_ => Count(short.MaxValue)).ToArray();
        private readonly NumericUpDown years = Count(short.MaxValue);
        private readonly NumericUpDown dismissed = Count(short.MaxValue);
        private readonly NumericUpDown resigned = Count(short.MaxValue);
        private readonly Label stations = new Label();
        private readonly NumericUpDown[] games = Competitions.Select((_, i) => Count(i == 0 ? int.MaxValue : short.MaxValue)).ToArray();
        private readonly NumericUpDown[] wins = Competitions.Select((_, i) => Count(i == 0 ? int.MaxValue : short.MaxValue)).ToArray();
        private readonly Button save = new Button { Text = "Speichern" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private ManagerCareer shown;

        /// <summary>Creates the career page, reading through <paramref name="game"/>.</summary>
        internal CareerView(ProcessMemory memory, A3Game game) : base(memory)
        {
            this.game = game;
            this.Size = new Size(614, 389);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.BuildLayout();
            A3Theme.Apply(this);
        }

        protected override string GameDisplayName => "Anstoss-3";

        /// <summary>The human manager (1-4) whose career the page shows.</summary>
        internal int Manager { get; set; } = 1;

        private void BuildLayout()
        {
            var honours = new GroupBox { Text = "Titel und Ehrungen", Bounds = new Rectangle(8, 6, 598, 206) };
            for (int i = 0; i < this.titles.Length; i++)
            {
                int x = i < 8 ? 12 : 306, y = 20 + (i % 8) * 23;
                honours.Controls.Add(new Label { Text = A3Addresses.CareerTitles[i].Value, Bounds = new Rectangle(x, y + 3, 170, 18) });
                this.titles[i].SetBounds(x + 176, y, 60, 21);
                honours.Controls.Add(this.titles[i]);
            }
            this.Controls.Add(honours);

            var career = new GroupBox { Text = "Laufbahn", Bounds = new Rectangle(8, 216, 260, 124) };
            int row = 20;
            void Row(string caption, Control input)
            {
                career.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(12, row + 3, 120, 18) });
                input.SetBounds(140, row, 80, 21);
                career.Controls.Add(input);
                row += 25;
            }
            Row("Managerjahre", this.years);
            Row("Entlassen", this.dismissed);
            Row("Selbst gegangen", this.resigned);
            Row("Stationen", this.stations);
            this.Controls.Add(career);

            var played = new GroupBox { Text = "Spiele als Trainer", Bounds = new Rectangle(276, 216, 330, 124) };
            played.Controls.Add(new Label { Text = "Spiele", Bounds = new Rectangle(130, 16, 80, 16) });
            played.Controls.Add(new Label { Text = "Siege", Bounds = new Rectangle(220, 16, 80, 16) });
            for (int i = 0; i < Competitions.Length; i++)
            {
                int y = 34 + i * 22;
                played.Controls.Add(new Label { Text = Competitions[i], Bounds = new Rectangle(12, y + 3, 110, 18) });
                this.games[i].SetBounds(130, y, 80, 21);
                this.wins[i].SetBounds(220, y, 80, 21);
                played.Controls.AddRange(new Control[] { this.games[i], this.wins[i] });
            }
            this.Controls.Add(played);

            this.save.SetBounds(8, 350, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(124, 350, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(240, 354, 366, 20);
            this.Controls.AddRange(new Control[] { this.save, this.reload, this.status });
        }

        /// <summary>Reads the career from the game (or clears the view if none is attached).</summary>
        internal void RefreshValues()
        {
            this.shown = this.IsGameRunning(silent: true) ? this.game.ReadCareer(this.Manager) : null;
            this.save.Enabled = this.shown != null;
            if (this.shown == null)
            {
                this.ClearAllFields(this);
                this.stations.Text = "";
                this.status.Text = "Kein Spiel verbunden";
                return;
            }

            for (int i = 0; i < this.titles.Length; i++)
                SetValue(this.titles[i], this.shown.Titles[i]);
            SetValue(this.years, this.shown.Years);
            SetValue(this.dismissed, this.shown.Dismissed);
            SetValue(this.resigned, this.shown.Resigned);
            this.stations.Text = this.shown.Stations.ToString();
            for (int i = 0; i < Competitions.Length; i++)
            {
                SetValue(this.games[i], this.shown.Games[i]);
                SetValue(this.wins[i], this.shown.Wins[i]);
            }
            this.status.Text = "";
        }

        private void Save()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;

            ManagerCareer edited = this.shown.Clone();
            edited.Titles = this.titles.Select(t => (int)t.Value).ToArray();
            edited.Years = (int)this.years.Value;
            edited.Dismissed = (int)this.dismissed.Value;
            edited.Resigned = (int)this.resigned.Value;
            edited.Games = this.games.Select(g => (int)g.Value).ToArray();
            edited.Wins = this.wins.Select(w => (int)w.Value).ToArray();
            if (edited.Wins.Where((w, i) => w > edited.Games[i]).Any())
            {
                this.status.Text = "Mehr Siege als Spiele";
                return;
            }
            try
            {
                this.game.SaveCareer(this.shown, edited);
                Logger.Debug($"Karriere von Manager {this.Manager} gespeichert");
                this.RefreshValues();
                this.status.Text = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Karriere konnte nicht gespeichert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
        }

        private static NumericUpDown Count(int maximum) => new NumericUpDown { Minimum = 0, Maximum = maximum, ThousandsSeparator = true };

        private static void SetValue(NumericUpDown input, int value) =>
            input.Value = Math.Max(input.Minimum, Math.Min(input.Maximum, value));
    }
}
