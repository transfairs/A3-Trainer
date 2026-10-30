using A3_Trainer.Controller;
using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A3_Trainer.View
{
    /// <summary>Squad list of a club plus an editor for the selected player.</summary>
    internal sealed class PlayerView : TrainerView
    {
        private readonly A3Game game;

        private readonly ListView list = new ListView();
        private readonly TabControl tabs = new TabControl();
        private readonly Label nameValue = new Label();
        private readonly Label birthValue = new Label();
        private readonly ComboBox position = DropDown(Tables.Positions.Skip(1));
        private readonly ComboBox side1 = DropDown(Tables.Positions);
        private readonly ComboBox side2 = DropDown(Tables.Positions);
        private readonly NumericUpDown strength = Number(1, Tables.MaxStrength);
        private readonly ComboBox talent = DropDown(Tables.Talents);
        private readonly NumericUpDown condition = Number(0, 100);
        private readonly NumericUpDown freshness = Number(0, 100);
        // Motivation is recomputed by the game from seven factors whenever it shows a player
        // (FUN_0070147E), so a written value wouldn't stick - shown only.
        private readonly Label morale = new Label();
        private readonly ComboBox foot = DropDown(Tables.Feet.Skip(1));
        private readonly NumericUpDown shirt = Number(0, 99);
        private readonly Label injuryValue = new Label();
        private readonly Button heal = new Button { Text = "Heilen" };
        private readonly CheckedListBox skills = new CheckedListBox { CheckOnClick = true };
        private readonly CheckedListBox traits = new CheckedListBox { CheckOnClick = true };
        private readonly CheckedListBox character = new CheckedListBox { CheckOnClick = true };
        private readonly NumericUpDown salary = Number(0, 65535);
        private readonly NumericUpDown matchBonus = Number(0, 65535);
        private readonly NumericUpDown contractEnd = Number(1990, 2100);
        private readonly Button save = new Button { Text = "Speichern" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private ClubKey? club;
        private List<Player> players = new List<Player>();
        private Player shown;
        private Player healed;
        private GameDate? today;

        internal PlayerView(ProcessMemory memory, A3Game game) : base(memory)
        {
            this.game = game;
            this.Size = new Size(630, 423);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.BuildLayout();
            A3Theme.Apply(this);
            this.ShowPlayer(null);
        }

        protected override string GameDisplayName => "Anstoss-3";

        /// <summary>Shows the squad of <paramref name="club"/>, or clears the view if null.</summary>
        internal void ShowClub(ClubKey? club)
        {
            this.club = club;
            this.RefreshList();
        }

        private void BuildLayout()
        {
            this.list.SetBounds(4, 4, 236, 415);
            this.list.View = System.Windows.Forms.View.Details;
            this.list.FullRowSelect = true;
            this.list.HideSelection = false;
            this.list.MultiSelect = false;
            this.list.Columns.Add("Name", 104);
            this.list.Columns.Add("Pos", 34);
            this.list.Columns.Add("St", 28);
            this.list.Columns.Add("Alter", 40);
            this.list.SelectedIndexChanged += (s, e) => this.ShowPlayer(this.SelectedPlayer());
            this.Controls.Add(this.list);

            this.tabs.SetBounds(246, 4, 380, 386);
            this.tabs.TabPages.Add(this.GeneralPage());
            this.tabs.TabPages.Add(this.SkillsPage());
            this.tabs.TabPages.Add(this.TraitsPage());
            this.tabs.TabPages.Add(this.ContractPage());
            this.Controls.Add(this.tabs);

            this.save.SetBounds(246, 394, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(362, 394, 110, 25);
            this.reload.Click += (s, e) => this.RefreshList();
            this.status.SetBounds(478, 398, 148, 20);
            this.Controls.AddRange(new Control[] { this.save, this.reload, this.status });

            this.heal.Click += (s, e) => this.Heal();
            this.position.SelectedIndexChanged += (s, e) => this.FillSkillList(this.SelectedPosition(), this.shown?.Skills ?? 0);
        }

        private TabPage GeneralPage()
        {
            var page = new TabPage("Spieler");
            int y = 10;
            void Row(string caption, Control input, int width = 150)
            {
                page.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(10, y + 3, 130, 18) });
                input.SetBounds(145, y, width, 21);
                page.Controls.Add(input);
                y += 26;
            }

            Row("Name", this.nameValue, 220);
            Row("Geboren (Alter)", this.birthValue, 220);
            Row("Hauptposition", this.position, 70);
            Row("Nebenposition 1", this.side1, 70);
            Row("Nebenposition 2", this.side2, 70);
            Row("Stärke (1–13)", this.strength, 70);
            Row("Talent", this.talent, 100);
            Row("Kondition", this.condition, 70);
            Row("Frische", this.freshness, 70);
            Row("Motivation", this.morale, 220);
            Row("Starker Fuß", this.foot, 100);
            Row("Rückennummer", this.shirt, 70);
            Row("Verletzung", this.injuryValue, 140);
            this.heal.SetBounds(290, y - 27, 75, 23);
            page.Controls.Add(this.heal);
            return page;
        }

        private TabPage SkillsPage()
        {
            var page = new TabPage("Fähigkeiten");
            this.skills.SetBounds(10, 10, 220, 330);
            page.Controls.Add(this.skills);
            return page;
        }

        private TabPage TraitsPage()
        {
            var page = new TabPage("Eigenschaften");
            page.Controls.Add(new Label { Text = "Eigenschaften", Bounds = new Rectangle(10, 8, 170, 16) });
            page.Controls.Add(new Label { Text = "Charakter", Bounds = new Rectangle(190, 8, 170, 16) });
            this.traits.SetBounds(10, 26, 170, 310);
            this.character.SetBounds(190, 26, 170, 310);
            this.traits.Items.AddRange(Tables.Traits);
            this.character.Items.AddRange(Tables.Character);
            page.Controls.Add(this.traits);
            page.Controls.Add(this.character);
            return page;
        }

        private TabPage ContractPage()
        {
            var page = new TabPage("Vertrag");
            int y = 10;
            void Row(string caption, Control input, string unit)
            {
                page.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(10, y + 3, 130, 18) });
                input.SetBounds(145, y, 80, 21);
                page.Controls.Add(input);
                page.Controls.Add(new Label { Text = unit, Bounds = new Rectangle(230, y + 3, 130, 18) });
                y += 26;
            }

            Row("Festgehalt", this.salary, "× 1.000 DM pro Jahr");
            Row("Auflaufprämie", this.matchBonus, "× 100 DM pro Spiel");
            Row("Vertrag bis", this.contractEnd, "30.6. des Jahres");
            return page;
        }

        private void RefreshList()
        {
            this.list.BeginUpdate();
            try
            {
                int? selectedIndex = this.SelectedPlayer()?.Index;
                this.list.Items.Clear();
                this.players = new List<Player>();
                this.today = null;

                if (this.club.HasValue && this.IsGameRunning(silent: true))
                {
                    this.today = this.game.Today();
                    this.players = this.game.ReadPlayers(this.club.Value);
                }

                foreach (Player player in this.players)
                {
                    var item = new ListViewItem(new[]
                    {
                        player.FullName,
                        Tables.PositionName(player.Position),
                        player.Strength.ToString(),
                        this.AgeText(player),
                    }) { Tag = player };
                    if (player.IsInjured)
                        item.ForeColor = Color.DarkRed;
                    this.list.Items.Add(item);
                }

                // Keep the player shown before, otherwise preselect the first one (as the A2G trainer does).
                ListViewItem again = this.list.Items.Cast<ListViewItem>().FirstOrDefault(i => ((Player)i.Tag).Index == selectedIndex)
                    ?? (this.list.Items.Count > 0 ? this.list.Items[0] : null);
                if (again != null)
                {
                    again.Selected = true;
                    again.Focused = true;
                    again.EnsureVisible();
                }
                else
                    this.ShowPlayer(null);

                this.status.Text = this.club.HasValue ? $"{this.players.Count} Spieler" : "Kein Spiel verbunden";
            }
            finally
            {
                this.list.EndUpdate();
            }
        }

        private string AgeText(Player player) =>
            this.today.HasValue && player.BirthDate.IsValid ? player.BirthDate.AgeOn(this.today.Value).ToString() : "";

        private Player SelectedPlayer() =>
            this.list.SelectedItems.Count > 0 ? (Player)this.list.SelectedItems[0].Tag : null;

        private void ShowPlayer(Player player)
        {
            this.shown = player;
            this.healed = null;
            bool has = player != null;
            this.tabs.Enabled = has;
            this.save.Enabled = has;
            if (!has)
            {
                this.ClearAllFields(this.tabs);
                this.nameValue.Text = "";
                this.birthValue.Text = "";
                this.injuryValue.Text = "";
                this.morale.Text = "";
                this.skills.Items.Clear();
                return;
            }

            this.nameValue.Text = player.FullName + (player.HasWorkingCopy ? "" : "  (nicht geladen)");
            this.birthValue.Text = player.BirthDate.IsValid ? $"{player.BirthDate} ({this.AgeText(player)})" : "";
            this.position.SelectedIndex = Clamp(player.Position - 1, this.position.Items.Count);
            this.side1.SelectedIndex = Clamp(player.SidePosition1, this.side1.Items.Count);
            this.side2.SelectedIndex = Clamp(player.SidePosition2, this.side2.Items.Count);
            SetNumber(this.strength, player.Strength);
            this.talent.SelectedIndex = Clamp(player.Talent, this.talent.Items.Count);
            SetNumber(this.condition, player.Condition / 100);
            SetNumber(this.freshness, player.Freshness / 100);
            this.morale.Text = player.Morale.ToString();
            this.foot.SelectedIndex = player.Foot >= 1 && player.Foot <= 3 ? player.Foot - 1 : -1;
            SetNumber(this.shirt, player.ShirtNumber);
            this.ShowInjury(player);
            this.FillSkillList(player.Position, player.Skills);
            SetBits(this.traits, player.Traits);
            SetBits(this.character, player.Character);
            SetNumber(this.salary, player.Salary);
            SetNumber(this.matchBonus, player.MatchBonus);
            SetNumber(this.contractEnd, player.ContractEndYear);
        }

        private void ShowInjury(Player player)
        {
            if (player.HasWorkingCopy && player.InjuryDays > 0)
                this.injuryValue.Text = $"noch {player.InjuryDays} Tage (Art {player.InjuryType})";
            else if (player.IsInjured)
                this.injuryValue.Text = "verletzt";
            else if (player.Groggy)
                this.injuryValue.Text = "angeschlagen";
            else
                this.injuryValue.Text = "gesund";
            this.heal.Enabled = player.IsInjured || player.Groggy;
        }

        private void FillSkillList(int position, int bits)
        {
            this.skills.Items.Clear();
            this.skills.Items.AddRange(Tables.SkillsFor(position));
            SetBits(this.skills, bits);
        }

        private void Heal()
        {
            if (this.shown == null)
                return;
            this.healed = this.shown.Clone();
            this.healed.InjuryDays = 0;
            this.healed.RecordInjured = false;
            this.healed.Groggy = false;
            this.ShowInjury(this.healed);
            this.injuryValue.Text = "gesund (nach Speichern)";
        }

        private void Save()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;

            Player edited = (this.healed ?? this.shown).Clone();
            edited.Position = this.SelectedPosition();
            edited.SidePosition1 = Math.Max(this.side1.SelectedIndex, 0);
            edited.SidePosition2 = Math.Max(this.side2.SelectedIndex, 0);
            edited.Strength = (int)this.strength.Value;
            edited.Talent = Math.Max(this.talent.SelectedIndex, 0);
            edited.Condition = KeepHundredths(this.shown.Condition, (int)this.condition.Value);
            edited.Freshness = KeepHundredths(this.shown.Freshness, (int)this.freshness.Value);
            if (this.foot.SelectedIndex >= 0)
                edited.Foot = this.foot.SelectedIndex + 1;
            edited.ShirtNumber = (int)this.shirt.Value;
            edited.Skills = GetBits(this.skills);
            edited.Traits = GetBits(this.traits);
            edited.Character = GetBits(this.character);
            edited.Salary = (int)this.salary.Value;
            edited.MatchBonus = (int)this.matchBonus.Value;
            edited.ContractEndYear = (int)this.contractEnd.Value;

            try
            {
                this.game.SavePlayer(this.shown, edited);
                Logger.Debug($"Spieler {edited.Index} ({edited.FullName}) gespeichert");
                this.status.Text = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Spieler konnte nicht gespeichert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
            this.RefreshList();
        }

        private int SelectedPosition() => Math.Max(this.position.SelectedIndex, 0) + 1;

        // Kondition/Frische are kept in hundredths; an untouched value keeps its fraction.
        private static int KeepHundredths(int original, int shownValue) =>
            shownValue == original / 100 ? original : shownValue * 100;

        private static ComboBox DropDown(IEnumerable<string> items)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(items.Cast<object>().ToArray());
            return combo;
        }

        private static NumericUpDown Number(int min, int max) => new NumericUpDown { Minimum = min, Maximum = max };

        private static void SetNumber(NumericUpDown number, int value) =>
            number.Value = Math.Min(Math.Max(value, (int)number.Minimum), (int)number.Maximum);

        private static int Clamp(int index, int count) => index >= 0 && index < count ? index : -1;

        private static void SetBits(CheckedListBox box, int bits)
        {
            for (int i = 0; i < box.Items.Count; i++)
                box.SetItemChecked(i, (bits & (1 << i)) != 0);
        }

        private static int GetBits(CheckedListBox box)
        {
            int bits = 0;
            foreach (int i in box.CheckedIndices)
                bits |= 1 << i;
            return bits;
        }
    }
}
