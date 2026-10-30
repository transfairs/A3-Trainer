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
        // The game keeps names in a shared table, so they are shown only.
        private readonly TextBox lastNameValue = new TextBox { ReadOnly = true };
        private readonly TextBox firstNameValue = new TextBox { ReadOnly = true };
        private readonly TextBox artistNameValue = new TextBox { ReadOnly = true, Enabled = false };
        private readonly NumericUpDown birthDay = Number(0, 31);
        private readonly NumericUpDown birthMonth = Number(0, 12);
        private readonly NumericUpDown birthYear = Number(0, 3000);
        private readonly Label ageValue = new Label();
        private readonly Label notLoaded = new Label { Text = "Spieler nicht geladen" };
        private readonly ComboBox nationality = Combo();
        private readonly ComboBox secondNationality = Combo();
        private RadioButton[] skinRadios;
        private RadioButton[] audienceRadios;
        private readonly RadioButton[] positionRadios = new RadioButton[Tables.EditorPositions.Length];
        private readonly CheckBox[] sideChecks = new CheckBox[Tables.EditorPositions.Length - 1];
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
        // "+" and "−" columns of the field player and goalkeeper skills (Skills / Skills2 of the player).
        private readonly CheckBox[] fieldPlus = new CheckBox[Tables.FieldSkills.Length];
        private readonly CheckBox[] fieldMinus = new CheckBox[Tables.FieldSkills.Length];
        private readonly CheckBox[] keeperPlus = new CheckBox[Tables.GoalkeeperSkills.Length];
        private readonly CheckBox[] keeperMinus = new CheckBox[Tables.GoalkeeperSkills.Length];
        private GroupBox fieldGroup;
        private GroupBox keeperGroup;
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

        /// <summary>Creates the squad page, reading through <paramref name="game"/>; it stays empty until <see cref="ShowClub"/>.</summary>
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
            this.list.Columns.Add("Pos", 34);
            this.list.Columns.Add("Spieler", 108);
            this.list.Columns.Add("Stärke", 44);
            this.list.Columns.Add("Alter", 38);
            this.list.SelectedIndexChanged += (s, e) => this.ShowPlayer(this.SelectedPlayer());
            this.Controls.Add(this.list);

            // Tabs as in the game's player editor.
            this.tabs.SetBounds(246, 4, 380, 386);
            this.tabs.ItemSize = new Size(62, 18);
            this.tabs.TabPages.Add(this.Data1Page());
            this.tabs.TabPages.Add(this.Data2Page());
            this.tabs.TabPages.Add(this.PositionsPage());
            this.tabs.TabPages.Add(this.SkillsPage());
            this.tabs.TabPages.Add(this.TraitsPage());
            this.tabs.TabPages.Add(this.HealthPage());
            this.tabs.TabPages.Add(this.ConditionPage());
            this.tabs.TabPages.Add(this.ContractPage());
            this.Controls.Add(this.tabs);

            this.save.SetBounds(246, 394, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(362, 394, 110, 25);
            this.reload.Click += (s, e) => this.RefreshList();
            this.status.SetBounds(478, 398, 148, 20);
            this.Controls.AddRange(new Control[] { this.save, this.reload, this.status });

            this.heal.Click += (s, e) => this.Heal();
        }

        private static GroupBox Group(TabPage page, string title, int top, int height)
        {
            var group = new GroupBox { Text = title, Bounds = new Rectangle(6, top, 358, height) };
            page.Controls.Add(group);
            return group;
        }

        /// <summary>Adds a caption and its input to <paramref name="parent"/> and returns the next row's top.</summary>
        private static int Row(Control parent, int y, string caption, Control input, int width)
        {
            parent.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(10, y + 3, 100, 18) });
            input.SetBounds(115, y, width, 21);
            parent.Controls.Add(input);
            return y + 26;
        }

        private static Label Caption(string text, int x, int y, int width = 100) =>
            new Label { Text = text, Bounds = new Rectangle(x, y, width, 15) };

        private TabPage Data1Page()
        {
            var page = new TabPage("Daten 1");
            GroupBox group = Group(page, "Persönliche Daten 1", 6, 296);

            // Caption above each name field, as in the game's editor.
            int y = 18;
            void NameField(string caption, TextBox box)
            {
                group.Controls.Add(Caption(caption, 10, y));
                box.SetBounds(10, y + 15, 336, 21);
                group.Controls.Add(box);
                y += 42;
            }
            NameField("Name", this.lastNameValue);
            NameField("Künstlername", this.artistNameValue);
            NameField("Vorname", this.firstNameValue);

            group.Controls.Add(Caption("Geburtsdatum", 10, y + 3));
            this.birthDay.SetBounds(110, y, 46, 21);
            this.birthMonth.SetBounds(160, y, 46, 21);
            this.birthYear.SetBounds(210, y, 62, 21);
            group.Controls.AddRange(new Control[] { this.birthDay, this.birthMonth, this.birthYear });
            foreach (NumericUpDown part in new[] { this.birthDay, this.birthMonth, this.birthYear })
                part.ValueChanged += (s, e) => this.ageValue.Text = this.AgeText(this.EnteredBirthDate());
            y += 28;

            y = Row(group, y, "Stärke", this.strength, 60);
            group.Controls.Add(Caption("Alter", 200, y - 23, 40));
            this.ageValue.SetBounds(245, y - 23, 60, 18);
            group.Controls.Add(this.ageValue);
            y = Row(group, y, "Talent", this.talent, 100);
            y = Row(group, y, "Starker Fuß", this.foot, 100);
            Row(group, y, "Rückennummer", this.shirt, 60);

            this.notLoaded.SetBounds(10, 306, 350, 18);
            page.Controls.Add(this.notLoaded);
            return page;
        }

        // Nationality, looks and national team status aren't read from the game yet; the page is
        // laid out as in the game's editor and stays disabled until they are.
        private TabPage Data2Page()
        {
            var page = new TabPage("Daten 2");
            GroupBox group = Group(page, "Persönliche Daten 2", 6, 346);

            group.Controls.Add(Caption("Nationalität", 10, 18, 200));
            this.nationality.SetBounds(10, 33, 336, 21);
            group.Controls.Add(this.nationality);
            group.Controls.Add(Caption("Zweite Nationalität", 10, 60, 200));
            this.secondNationality.SetBounds(10, 75, 336, 21);
            group.Controls.Add(this.secondNationality);

            // Only nationality and skin colour are read from the game so far; the rest of the page
            // is laid out as in the game's editor and stays disabled until it is.
            var notRead = new List<Control>
            {
                new CheckBox { Text = "Fußballinländer", Bounds = new Rectangle(14, 104, 150, 18) },
                new CheckBox { Text = "Nationalspieler", Bounds = new Rectangle(190, 104, 150, 18) },
                new CheckBox { Text = "Aus Nationalmannschaft zurückgetreten", Bounds = new Rectangle(14, 126, 330, 18) },
                new CheckBox { Text = "Als Kapitän zurückgetreten", Bounds = new Rectangle(14, 148, 330, 18) },
                Options(group, "Haartyp", new Rectangle(116, 174, 120, 90), false, 1, "extrem kurz", "kurz", "Wuschelkopf", "lang"),
                Options(group, "Bart", new Rectangle(242, 174, 104, 90), true, 1, "unrasiert", "Bart", "Schnurrbart", "Ziegenbart"),
                Options(group, "Haarfarbe", new Rectangle(10, 268, 336, 70), false, 3, "hellblond", "blond", "braun", "rot", "schwarz", "Glatze", "grau"),
            };
            foreach (Control control in notRead)
            {
                control.Enabled = false;
                if (control is CheckBox)
                    group.Controls.Add(control);
            }

            GroupBox skin = Options(group, "Hautfarbe", new Rectangle(10, 174, 100, 90), false, 1, "hell", "dunkel", "schwarz", "asiatisch");
            this.skinRadios = skin.Controls.Cast<RadioButton>().ToArray();
            return page;
        }

        private static ComboBox Combo() => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

        // The country names come from the running game; the lists are filled once they are readable.
        private void FillCountries()
        {
            if (this.nationality.Items.Count > 0 || !this.IsGameRunning(silent: true))
                return;
            List<string> names = this.game.ReadCountryNames();
            this.nationality.Items.AddRange(names.Cast<object>().ToArray());
            // Value 0 means "none" for the second nationality.
            this.secondNationality.Items.AddRange(names.Select((n, i) => i == 0 ? "Keine" : n).Cast<object>().ToArray());
        }

        /// <summary>Radio buttons (or check boxes) in a group box, filled column by column.</summary>
        private static GroupBox Options(Control parent, string title, Rectangle bounds, bool checks, int columns, params string[] names)
        {
            var box = new GroupBox { Text = title, Bounds = bounds };
            int rows = (names.Length + columns - 1) / columns;
            int width = (bounds.Width - 12) / columns;
            for (int i = 0; i < names.Length; i++)
            {
                var at = new Rectangle(8 + i / rows * width, 14 + i % rows * 18, width, 18);
                box.Controls.Add(checks ? (Control)new CheckBox { Text = names[i], Bounds = at } : new RadioButton { Text = names[i], Bounds = at });
            }
            parent.Controls.Add(box);
            return box;
        }

        private TabPage PositionsPage()
        {
            var page = new TabPage("Positionen");
            var main = new GroupBox { Text = "Hauptposition", Bounds = new Rectangle(6, 6, 180, 250) };
            var side = new GroupBox { Text = "Nebenposition", Bounds = new Rectangle(190, 6, 174, 250) };
            for (int i = 0; i < Tables.EditorPositions.Length; i++)
            {
                var radio = new RadioButton { Text = Tables.EditorPositionNames[i], Bounds = new Rectangle(10, 20 + i * 22, 168, 18), Tag = Tables.EditorPositions[i] };
                radio.CheckedChanged += (s, e) =>
                {
                    if (((RadioButton)s).Checked)
                        this.ShowSkills(this.SelectedPosition(), this.shown?.Skills ?? 0, this.shown?.Skills2 ?? 0);
                };
                this.positionRadios[i] = radio;
                main.Controls.Add(radio);

                if (i == 0)
                    continue; // the goalkeeper has no secondary position
                var check = new CheckBox { Text = Tables.EditorPositionNames[i], Bounds = new Rectangle(10, 20 + (i - 1) * 22, 162, 18), Tag = Tables.EditorPositions[i] };
                check.CheckedChanged += (s, e) =>
                {
                    // The game stores two secondary positions.
                    if (((CheckBox)s).Checked && this.sideChecks.Count(c => c.Checked) > 2)
                        ((CheckBox)s).Checked = false;
                };
                this.sideChecks[i - 1] = check;
                side.Controls.Add(check);
            }
            page.Controls.AddRange(new Control[] { main, side });
            return page;
        }

        private TabPage SkillsPage()
        {
            var page = new TabPage("Fähigkeiten");
            this.fieldGroup = this.SkillGroup("Feldspieler", 6, Tables.FieldSkills, this.fieldPlus, this.fieldMinus);
            this.keeperGroup = this.SkillGroup("Torwart", 187, Tables.GoalkeeperSkills, this.keeperPlus, this.keeperMinus);
            page.Controls.AddRange(new Control[] { this.fieldGroup, this.keeperGroup });
            return page;
        }

        /// <summary>A skill table with a "+" and a "−" check box per skill, as in the game's editor.</summary>
        private GroupBox SkillGroup(string title, int x, string[] names, CheckBox[] plus, CheckBox[] minus)
        {
            var group = new GroupBox { Text = title, Bounds = new Rectangle(x, 6, 177, 344) };
            group.Controls.Add(Caption("+", 10, 17, 14));
            group.Controls.Add(Caption("−", 30, 17, 14));
            for (int i = 0; i < names.Length; i++)
            {
                int y = 34 + i * 18;
                plus[i] = new CheckBox { Bounds = new Rectangle(8, y, 16, 16) };
                minus[i] = new CheckBox { Bounds = new Rectangle(28, y, 16, 16) };
                group.Controls.Add(plus[i]);
                group.Controls.Add(minus[i]);
                group.Controls.Add(Caption(names[i], 50, y + 2, 122));
            }
            return group;
        }

        private TabPage TraitsPage()
        {
            var page = new TabPage("Eig. 1");
            var traitGroup = new GroupBox { Text = "Eigenschaften", Bounds = new Rectangle(6, 6, 177, 344) };
            var characterGroup = new GroupBox { Text = "Charakter", Bounds = new Rectangle(187, 6, 177, 344) };
            this.traits.SetBounds(8, 18, 161, 318);
            this.character.SetBounds(8, 18, 161, 318);
            this.traits.Items.AddRange(Tables.Traits);
            this.character.Items.AddRange(Tables.Character);
            traitGroup.Controls.Add(this.traits);
            characterGroup.Controls.Add(this.character);
            page.Controls.AddRange(new Control[] { traitGroup, characterGroup });
            return page;
        }

        // The audience's view is shown from the record's flags but can't be edited yet (the reputation
        // level in +0x282 belongs to it); the health type isn't read from the game yet.
        private TabPage HealthPage()
        {
            var page = new TabPage("Eig. 2");
            GroupBox health = Options(page, "Gesundheitszustand", new Rectangle(6, 6, 177, 186), false, 1,
                "normal", "robust", "anfällig", "Knieprobleme", "schnell wieder fit", "dauert bis wieder fit", "wehleidig");
            GroupBox audience = Options(page, "Publikum", new Rectangle(187, 6, 177, 90), false, 1, "normal", "Liebling", "Haßfigur");
            health.Enabled = audience.Enabled = false;
            this.audienceRadios = audience.Controls.Cast<RadioButton>().ToArray();
            return page;
        }

        private TabPage ConditionPage()
        {
            var page = new TabPage("Verfassung");
            GroupBox group = Group(page, "Verfassung", 6, 150);
            int y = 20;
            y = Row(group, y, "Kondition", this.condition, 70);
            y = Row(group, y, "Frische", this.freshness, 70);
            y = Row(group, y, "Motivation", this.morale, 70);
            Row(group, y, "Verletzung", this.injuryValue, 140);
            this.heal.SetBounds(270, y, 75, 23);
            group.Controls.Add(this.heal);
            return page;
        }

        private TabPage ContractPage()
        {
            var page = new TabPage("Vertrag");
            GroupBox group = Group(page, "Vertrag", 6, 104);
            int y = 20;
            void Contract(string caption, Control input, string unit)
            {
                group.Controls.Add(new Label { Text = unit, Bounds = new Rectangle(200, y + 3, 150, 18) });
                y = Row(group, y, caption, input, 80);
            }

            Contract("Festgehalt", this.salary, "× 1.000 DM pro Jahr");
            Contract("Auflaufprämie", this.matchBonus, "× 100 DM pro Spiel");
            Contract("Vertrag bis", this.contractEnd, "30.6. des Jahres");
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
                    this.FillCountries();
                    this.today = this.game.Today();
                    this.players = this.game.ReadPlayers(this.club.Value);
                }

                foreach (Player player in this.players)
                {
                    var item = new ListViewItem(new[]
                    {
                        Tables.PositionName(player.Position),
                        player.ListName,
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

        private string AgeText(Player player) => this.AgeText(player.BirthDate);

        private string AgeText(GameDate birth) =>
            this.today.HasValue && birth.IsValid ? birth.AgeOn(this.today.Value).ToString() : "";

        private GameDate EnteredBirthDate() =>
            new GameDate((int)this.birthYear.Value, (int)this.birthMonth.Value, (int)this.birthDay.Value);

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
                this.ageValue.Text = "";
                this.notLoaded.Visible = false;
                this.injuryValue.Text = "";
                this.morale.Text = "";
                this.birthDay.Value = this.birthMonth.Value = this.birthYear.Value = 0;
                return;
            }

            this.lastNameValue.Text = player.LastName;
            this.firstNameValue.Text = player.FirstName;
            this.notLoaded.Visible = !player.HasWorkingCopy;
            this.birthDay.Value = player.BirthDate.IsValid ? player.BirthDate.Day : 0;
            this.birthMonth.Value = player.BirthDate.IsValid ? player.BirthDate.Month : 0;
            SetNumber(this.birthYear, player.BirthDate.IsValid ? player.BirthDate.Year : 0);
            this.ageValue.Text = this.AgeText(player);
            this.nationality.SelectedIndex = Clamp(player.Nationality, this.nationality.Items.Count);
            this.secondNationality.SelectedIndex = Clamp(player.SecondNationality, this.secondNationality.Items.Count);
            for (int i = 0; i < this.skinRadios.Length; i++)
                this.skinRadios[i].Checked = i == player.SkinColor;
            int audience = player.Darling ? 1 : player.Hated ? 2 : 0; // normal, Liebling, Haßfigur
            for (int i = 0; i < this.audienceRadios.Length; i++)
                this.audienceRadios[i].Checked = i == audience;
            foreach (RadioButton radio in this.positionRadios)
                radio.Checked = (int)radio.Tag == player.Position;
            foreach (CheckBox check in this.sideChecks)
                check.Checked = (int)check.Tag == player.SidePosition1 || (int)check.Tag == player.SidePosition2;
            SetNumber(this.strength, player.Strength);
            this.talent.SelectedIndex = Clamp(player.Talent, this.talent.Items.Count);
            SetNumber(this.condition, player.Condition / 100);
            SetNumber(this.freshness, player.Freshness / 100);
            this.morale.Text = player.Morale.ToString();
            this.foot.SelectedIndex = player.Foot >= 1 && player.Foot <= 3 ? player.Foot - 1 : -1;
            SetNumber(this.shirt, player.ShirtNumber);
            this.ShowInjury(player);
            this.ShowSkills(player.Position, player.Skills, player.Skills2);
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

        // As in the game, only the table of the player's kind (field player or goalkeeper) is active.
        private void ShowSkills(int position, int plusBits, int minusBits)
        {
            bool keeper = position == Tables.Goalkeeper;
            this.fieldGroup.Enabled = !keeper;
            this.keeperGroup.Enabled = keeper;
            SetBits(this.fieldPlus, keeper ? 0 : plusBits);
            SetBits(this.fieldMinus, keeper ? 0 : minusBits);
            SetBits(this.keeperPlus, keeper ? plusBits : 0);
            SetBits(this.keeperMinus, keeper ? minusBits : 0);
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
            edited.BirthDate = this.EnteredBirthDate();
            if (!edited.BirthDate.Equals(this.shown.BirthDate) && !edited.BirthDate.Exists)
            {
                this.status.Text = "Ungültiges Geburtsdatum";
                return;
            }
            edited.Position = this.SelectedPosition();
            if (this.nationality.SelectedIndex >= 0)
                edited.Nationality = this.nationality.SelectedIndex;
            if (this.secondNationality.SelectedIndex >= 0)
                edited.SecondNationality = this.secondNationality.SelectedIndex;
            int skin = Array.FindIndex(this.skinRadios, r => r.Checked);
            if (skin >= 0)
                edited.SkinColor = skin;
            int[] sides =this.sideChecks.Where(c => c.Checked).Select(c => (int)c.Tag).ToArray();
            edited.SidePosition1 = sides.Length > 0 ? sides[0] : 0;
            edited.SidePosition2 = sides.Length > 1 ? sides[1] : 0;
            edited.Strength = (int)this.strength.Value;
            edited.Talent = Math.Max(this.talent.SelectedIndex, 0);
            edited.Condition = KeepHundredths(this.shown.Condition, (int)this.condition.Value);
            edited.Freshness = KeepHundredths(this.shown.Freshness, (int)this.freshness.Value);
            if (this.foot.SelectedIndex >= 0)
                edited.Foot = this.foot.SelectedIndex + 1;
            edited.ShirtNumber = (int)this.shirt.Value;
            bool keeper = edited.Position == Tables.Goalkeeper;
            edited.Skills = GetBits(keeper ? this.keeperPlus : this.fieldPlus);
            edited.Skills2 = GetBits(keeper ? this.keeperMinus : this.fieldMinus);
            edited.Traits = GetBits(this.traits);
            edited.Character = GetBits(this.character);
            edited.Salary = (int)this.salary.Value;
            edited.MatchBonus = (int)this.matchBonus.Value;
            edited.ContractEndYear = (int)this.contractEnd.Value;

            string result;
            try
            {
                this.game.SavePlayer(this.shown, edited);
                Logger.Debug($"Spieler {edited.Index} ({edited.FullName}) gespeichert");
                result = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Spieler konnte nicht gespeichert werden", ex);
                result = "Fehler beim Speichern";
            }
            // Reloading shows what the game now holds; the result goes in the status afterwards,
            // which the reload would otherwise overwrite with the player count.
            this.RefreshList();
            this.status.Text = result;
        }

        private int SelectedPosition() =>
            this.positionRadios.Where(r => r.Checked).Select(r => (int)r.Tag).DefaultIfEmpty(this.shown?.Position ?? Tables.Goalkeeper).First();

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

        private static void SetBits(CheckBox[] boxes, int bits)
        {
            for (int i = 0; i < boxes.Length; i++)
                boxes[i].Checked = (bits & (1 << i)) != 0;
        }

        private static int GetBits(CheckBox[] boxes)
        {
            int bits = 0;
            for (int i = 0; i < boxes.Length; i++)
                if (boxes[i].Checked)
                    bits |= 1 << i;
            return bits;
        }
    }
}
