using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>A club's president, opposition leader, founding date and eternal table, like the game's club editor.</summary>
    internal sealed class BoardView : TrainerView
    {
        private readonly A3Game game;

        private readonly MemberInputs president = new MemberInputs();
        private readonly MemberInputs opposition = new MemberInputs();
        private readonly DateInputs founded = new DateInputs();
        private readonly NumericUpDown games = Count();
        private readonly NumericUpDown points = Count();
        private readonly NumericUpDown goals = Count();
        private readonly NumericUpDown conceded = Count();
        private readonly Button save = new Button { Text = "Speichern" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private ClubBoard shown;
        private GameDate? today;

        /// <summary>Creates the board page, reading through <paramref name="game"/>.</summary>
        internal BoardView(ProcessMemory memory, A3Game game) : base(memory)
        {
            this.game = game;
            this.Size = new Size(614, 389);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.BuildLayout();
            A3Theme.Apply(this);
        }

        protected override string GameDisplayName => "Anstoss-3";

        /// <summary>The club the page shows.</summary>
        internal ClubKey Club { get; set; }

        private void BuildLayout()
        {
            this.Controls.Add(this.president.Group("Präsident", new Rectangle(8, 6, 295, 124), this.Age));
            this.Controls.Add(this.opposition.Group("Oppositionsführer", new Rectangle(311, 6, 295, 124), this.Age));

            var history = new GroupBox { Text = "Geschichte", Bounds = new Rectangle(8, 136, 598, 134) };
            history.Controls.Add(new Label { Text = "Gegründet am", Bounds = new Rectangle(12, 25, 120, 18) });
            this.founded.Place(history, 140, 22);
            history.Controls.Add(new Label { Text = "Ewige Tabelle 1. Liga", Bounds = new Rectangle(12, 55, 160, 18) });
            void Cell(string caption, NumericUpDown input, int x, int y)
            {
                history.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(x, y + 3, 120, 18) });
                input.SetBounds(x + 128, y, 100, 21);
                history.Controls.Add(input);
            }
            Cell("Spiele", this.games, 12, 76);
            Cell("Punkte", this.points, 300, 76);
            Cell("Tore", this.goals, 12, 104);
            Cell("Gegentore", this.conceded, 300, 104);
            this.Controls.Add(history);

            this.save.SetBounds(8, 278, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(124, 278, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(240, 282, 366, 20);
            this.Controls.AddRange(new Control[] { this.save, this.reload, this.status });
        }

        /// <summary>Reads the club's board from the game (or clears the view if there is none).</summary>
        internal void RefreshValues()
        {
            this.shown = this.IsGameRunning(silent: true) ? this.game.ReadBoard(this.Club) : null;
            this.save.Enabled = this.shown != null;
            if (this.shown == null)
            {
                this.ClearAllFields(this);
                this.president.ShowAge("");
                this.opposition.ShowAge("");
                this.status.Text = "Kein Verein gefunden";
                return;
            }

            this.today = this.game.Today();
            this.president.Show(this.shown.President);
            this.opposition.Show(this.shown.Opposition);
            this.founded.Show(this.shown.Founded);
            SetNumber(this.games, this.shown.Games);
            SetNumber(this.points, this.shown.Points);
            SetNumber(this.goals, this.shown.Goals);
            SetNumber(this.conceded, this.shown.Conceded);
            this.status.Text = "";
        }

        // The age the game shows for a birthday on the current game day.
        private string Age(GameDate birthday) =>
            this.today.HasValue && birthday.Exists ? birthday.AgeOn(this.today.Value).ToString() : "";

        private void Save()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;

            ClubBoard edited = this.shown.Clone();
            edited.President = this.president.Entered(this.shown.President);
            edited.Opposition = this.opposition.Entered(this.shown.Opposition);
            edited.Founded = this.founded.Entered;
            edited.Games = (int)this.games.Value;
            edited.Points = (int)this.points.Value;
            edited.Goals = (int)this.goals.Value;
            edited.Conceded = (int)this.conceded.Value;
            // A date the calendar doesn't have is refused; an unchanged one (e.g. empty in the record) stays.
            if (Invalid(edited.President.Birthday, this.shown.President.Birthday) ||
                Invalid(edited.Opposition.Birthday, this.shown.Opposition.Birthday) ||
                Invalid(edited.Founded, this.shown.Founded))
            {
                this.status.Text = "Ungültiges Datum";
                return;
            }
            try
            {
                this.game.SaveBoard(this.shown, edited);
                Logger.Debug("Präsidium gespeichert");
                this.RefreshValues();
                this.status.Text = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Präsidium konnte nicht gespeichert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
        }

        private static bool Invalid(GameDate now, GameDate was) => !now.Equals(was) && !now.Exists;

        private static NumericUpDown Count() => new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, ThousandsSeparator = true };

        private static void SetNumber(NumericUpDown input, int value) =>
            input.Value = Math.Max(input.Minimum, Math.Min(input.Maximum, value));

        /// <summary>Day, month and year inputs of a date.</summary>
        private sealed class DateInputs
        {
            internal readonly NumericUpDown Day = new NumericUpDown { Minimum = 0, Maximum = 31 };
            internal readonly NumericUpDown Month = new NumericUpDown { Minimum = 0, Maximum = 12 };
            internal readonly NumericUpDown Year = new NumericUpDown { Minimum = 0, Maximum = 3000 };

            internal void Place(Control parent, int x, int y)
            {
                this.Day.SetBounds(x, y, 46, 21);
                this.Month.SetBounds(x + 50, y, 46, 21);
                this.Year.SetBounds(x + 100, y, 62, 21);
                parent.Controls.AddRange(new Control[] { this.Day, this.Month, this.Year });
            }

            internal void Show(GameDate date)
            {
                SetNumber(this.Day, date.Day);
                SetNumber(this.Month, date.Month);
                SetNumber(this.Year, date.Year);
            }

            internal GameDate Entered => new GameDate((int)this.Year.Value, (int)this.Month.Value, (int)this.Day.Value);
        }

        /// <summary>Name and birthday inputs of a board member.</summary>
        private sealed class MemberInputs
        {
            private readonly TextBox firstName = new TextBox { MaxLength = A3Addresses.MaxNameLength };
            private readonly TextBox lastName = new TextBox { MaxLength = A3Addresses.MaxNameLength };
            private readonly DateInputs birthday = new DateInputs();
            private readonly Label age = new Label();

            internal GroupBox Group(string title, Rectangle bounds, Func<GameDate, string> ageOf)
            {
                var group = new GroupBox { Text = title, Bounds = bounds };
                int y = 22;
                void Row(string caption)
                {
                    group.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(12, y + 3, 76, 18) });
                }
                Row("Vorname");
                this.firstName.SetBounds(90, y, 190, 21);
                y += 30;
                Row("Nachname");
                this.lastName.SetBounds(90, y, 190, 21);
                y += 30;
                Row("Geburtstag");
                this.birthday.Place(group, 90, y);
                this.age.SetBounds(256, y + 3, 36, 18);
                group.Controls.AddRange(new Control[] { this.firstName, this.lastName, this.age });
                foreach (NumericUpDown part in new[] { this.birthday.Day, this.birthday.Month, this.birthday.Year })
                    part.ValueChanged += (s, e) => this.age.Text = ageOf(this.birthday.Entered);
                return group;
            }

            internal void Show(BoardMember member)
            {
                this.firstName.Text = member.FirstName;
                this.lastName.Text = member.LastName;
                this.birthday.Show(member.Birthday);
            }

            internal void ShowAge(string text) => this.age.Text = text;

            // An emptied name stays as it was.
            internal BoardMember Entered(BoardMember was) => new BoardMember
            {
                FirstName = this.firstName.Text.Trim().Length > 0 ? this.firstName.Text.Trim() : was.FirstName,
                LastName = this.lastName.Text.Trim().Length > 0 ? this.lastName.Text.Trim() : was.LastName,
                Birthday = this.birthday.Entered,
            };
        }
    }
}
