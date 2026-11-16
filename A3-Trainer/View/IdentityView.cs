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
    /// <summary>A club's fans, board, fan friendship and arch rival, like the game's club editor.</summary>
    internal sealed class IdentityView : TrainerView
    {
        private const string None = "–";

        private readonly A3Game game;

        private readonly TextBox fanName = new TextBox { MaxLength = A3Addresses.ClubFanNameSize - 1 };
        private readonly ComboBox fanCrowd = DropDown();
        private readonly ComboBox maxFanCrowd = DropDown();
        private readonly ComboBox fanType = DropDown();
        private readonly ComboBox hooligans = DropDown();
        private readonly ComboBox financialPower = DropDown();
        private readonly ComboBox region = DropDown();
        private readonly CheckBox mediaCity = new CheckBox { Text = "Medienstadt" };
        private readonly ComboBox board = DropDown();
        private readonly ComboBox friend = DropDown();
        private readonly ComboBox rival = DropDown();
        private readonly Button save = new Button { Text = "Speichern" };
        private readonly Button reload = new Button { Text = "Neu laden" };
        private readonly Label status = new Label();

        private ClubIdentity shown;
        // Club numbers of the friend and rival lists after their "none" entry.
        private List<int> numbers = new List<int>();

        /// <summary>Creates the identity page, reading through <paramref name="game"/>.</summary>
        internal IdentityView(ProcessMemory memory, A3Game game) : base(memory)
        {
            this.game = game;
            this.Size = new Size(614, 389);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.fanCrowd.Items.AddRange(Tables.FanCrowds);
            this.maxFanCrowd.Items.AddRange(Tables.FanCrowds);
            this.fanType.Items.AddRange(Tables.FanTypes);
            this.hooligans.Items.AddRange(Tables.Hooligans);
            this.financialPower.Items.AddRange(Tables.FinancialPowers);
            this.region.Items.AddRange(Tables.Regions);
            this.board.Items.AddRange(Tables.Boards);
            this.BuildLayout();
            A3Theme.Apply(this);
        }

        protected override string GameDisplayName => "Anstoss-3";

        /// <summary>The club the page shows.</summary>
        internal ClubKey Club { get; set; }

        private void BuildLayout()
        {
            var fans = new GroupBox { Text = "Fans", Bounds = new Rectangle(8, 6, 598, 160) };
            int y = 22;
            void Row(GroupBox group, string caption, Control input, int width)
            {
                group.Controls.Add(new Label { Text = caption, Bounds = new Rectangle(12, y + 3, 170, 18) });
                input.SetBounds(190, y, width, 21);
                group.Controls.Add(input);
                y += 27;
            }
            Row(fans, "Fanbezeichnung", this.fanName, 160);
            Row(fans, "Fanaufkommen", this.fanCrowd, 160);
            Row(fans, "Max. Fanaufkommen", this.maxFanCrowd, 160);
            Row(fans, "Art der Fans", this.fanType, 160);
            Row(fans, "Hooligans", this.hooligans, 160);
            this.Controls.Add(fans);

            var club = new GroupBox { Text = "Verein", Bounds = new Rectangle(8, 172, 598, 160) };
            y = 22;
            Row(club, "Vorstand", this.board, 160);
            Row(club, "Finanzkraft", this.financialPower, 160);
            Row(club, "Fanfreundschaft mit", this.friend, 220);
            Row(club, "Erzrivale", this.rival, 220);
            this.mediaCity.SetBounds(360, y, 120, 21);
            club.Controls.Add(this.mediaCity);
            Row(club, "Region", this.region, 160);
            this.Controls.Add(club);

            this.save.SetBounds(8, 340, 110, 25);
            this.save.Click += (s, e) => this.Save();
            this.reload.SetBounds(124, 340, 110, 25);
            this.reload.Click += (s, e) => this.RefreshValues();
            this.status.SetBounds(240, 344, 366, 20);
            this.Controls.AddRange(new Control[] { this.save, this.reload, this.status });
        }

        /// <summary>Reads the club's identity from the game (or clears the view if there is none).</summary>
        internal void RefreshValues()
        {
            this.shown = this.IsGameRunning(silent: true) ? this.game.ReadIdentity(this.Club) : null;
            this.save.Enabled = this.shown != null;
            if (this.shown == null)
            {
                this.ClearAllFields(this);
                this.status.Text = "Kein Verein gefunden";
                return;
            }

            // A club can't be its own friend or rival.
            List<KeyValuePair<int, string>> clubs = this.game.CountryClubs(this.Club.Country).Where(c => c.Key != this.Club.Number).ToList();
            this.numbers = clubs.Select(c => c.Key).ToList();
            foreach (ComboBox list in new[] { this.friend, this.rival })
            {
                list.Items.Clear();
                list.Items.Add(None);
                list.Items.AddRange(clubs.Select(c => (object)c.Value).ToArray());
            }

            this.fanName.Text = this.shown.FanName;
            Select(this.fanCrowd, this.shown.FanCrowd);
            Select(this.maxFanCrowd, this.shown.MaxFanCrowd);
            Select(this.fanType, this.shown.FanType);
            Select(this.hooligans, this.shown.Hooligans);
            Select(this.financialPower, this.shown.FinancialPower);
            Select(this.region, this.shown.Region);
            this.mediaCity.Checked = this.shown.MediaCity;
            Select(this.board, this.shown.Board);
            this.friend.SelectedIndex = this.ListIndex(this.shown.Friend);
            this.rival.SelectedIndex = this.ListIndex(this.shown.Rival);
            this.status.Text = "";
        }

        // A club the list doesn't have shows nothing and is kept on saving.
        private int ListIndex(int number) => number < 0 ? 0 : this.numbers.Contains(number) ? this.numbers.IndexOf(number) + 1 : -1;

        private int Number(ComboBox list, int old) =>
            list.SelectedIndex < 0 ? old : list.SelectedIndex == 0 ? -1 : this.numbers[list.SelectedIndex - 1];

        private void Save()
        {
            if (this.shown == null || !this.IsGameRunning())
                return;

            ClubIdentity edited = this.shown.Clone();
            // An emptied fan name stays as it was.
            edited.FanName = this.fanName.Text.Trim().Length > 0 ? this.fanName.Text.Trim() : this.shown.FanName;
            edited.FanCrowd = Picked(this.fanCrowd, this.shown.FanCrowd);
            edited.MaxFanCrowd = Picked(this.maxFanCrowd, this.shown.MaxFanCrowd);
            // As in the game's club editor, the fan crowd is never better than the maximum.
            edited.FanCrowd = Math.Max(edited.FanCrowd, edited.MaxFanCrowd);
            edited.FanType = Picked(this.fanType, this.shown.FanType);
            edited.Hooligans = Picked(this.hooligans, this.shown.Hooligans);
            edited.FinancialPower = Picked(this.financialPower, this.shown.FinancialPower);
            edited.Region = Picked(this.region, this.shown.Region);
            edited.MediaCity = this.mediaCity.Checked;
            edited.Board = Picked(this.board, this.shown.Board);
            edited.Friend = this.Number(this.friend, this.shown.Friend);
            edited.Rival = this.Number(this.rival, this.shown.Rival);
            try
            {
                this.game.SaveIdentity(this.shown, edited);
                Logger.Debug("Vereinsidentität gespeichert");
                this.RefreshValues();
                this.status.Text = "Gespeichert";
            }
            catch (Exception ex)
            {
                Logger.Error("Vereinsidentität konnte nicht gespeichert werden", ex);
                this.status.Text = "Fehler beim Speichern";
            }
        }

        private static ComboBox DropDown() => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

        private static void Select(ComboBox list, int value) => list.SelectedIndex = value >= 0 && value < list.Items.Count ? value : -1;

        private static int Picked(ComboBox list, int old) => list.SelectedIndex >= 0 ? list.SelectedIndex : old;
    }
}
