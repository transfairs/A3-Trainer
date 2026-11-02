using A3_Trainer.Controller;
using A3_Trainer.Model;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace A3_Trainer
{
    /// <summary>Main window of the Anstoss 3 trainer, on the shared TrainerShell.</summary>
    internal sealed class MainWindow : TrainerShell
    {
        private readonly MenuStrip menu = new MenuStrip();
        private readonly Panel content = new Panel { Dock = DockStyle.Fill };
        private readonly ToolStripMenuItem opponentItem;

        private readonly ProcessMemory memory = new ProcessMemory();
        private readonly A3Game game;
        private readonly PlayerView playerView;
        private readonly ClubView clubView;
        private readonly StadiumView stadiumView;
        private readonly ManagerView managerView;
        private readonly AboutView aboutView = new AboutView();
        private readonly Label waiting;

        private enum Page { Squad, Opponent, Other, Club, Stadium, Manager }
        private Page page = Page.Squad;
        // The club last picked in the player view's club selection.
        private ClubKey? otherClub;
        // The human manager whose squad, club and data the pages show; follows the turn.
        private int manager = 1;
        private readonly ToolStripMenuItem managerMenu = new ToolStripMenuItem("Trainer");

        /// <summary>Builds the menu, the views and the waiting screen and starts watching for the game.</summary>
        internal MainWindow()
        {
            this.Text = "A3-Trainer";
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.ClientSize = new Size(630, 447);
            this.BackColor = A3Theme.Panel;
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            var players = new ToolStripMenuItem("Spieler");
            players.DropDownItems.Add("Eigener Kader", null, (s, e) => this.Show(Page.Squad));
            this.opponentItem = new ToolStripMenuItem("Nächster Gegner", null, (s, e) => this.Show(Page.Opponent));
            players.DropDownItems.Add(this.opponentItem);
            players.DropDownItems.Add("Anderer Verein", null, (s, e) => this.Show(Page.Other));
            var club = new ToolStripMenuItem("Verein");
            club.DropDownItems.Add("Eigener Verein", null, (s, e) => this.Show(Page.Club));
            club.DropDownItems.Add("Stadion", null, (s, e) => this.Show(Page.Stadium));
            this.managerMenu.DropDownItems.Add("Manager", null, (s, e) => this.Show(Page.Manager));
            this.managerMenu.DropDownOpening += (s, e) => this.ListManagers();
            var help = new ToolStripMenuItem("?");
            help.DropDownItems.Add("Onlinehilfe", null, (s, e) => this.OpenHelp());
            help.DropDownItems.Add("Über", null, (s, e) => this.ShowAbout());
            this.menu.Items.AddRange(new ToolStripItem[] { club, players, this.managerMenu, help });
            A3Theme.ApplyMenu(this.menu);

            this.Controls.Add(this.content);
            this.Controls.Add(this.menu);
            this.MainMenuStrip = this.menu;

            this.game = new A3Game(new LiveGameMemory(this.memory));
            this.playerView = new PlayerView(this.memory, this.game);
            this.playerView.ClubChosen += this.OnClubChosen;
            this.clubView = new ClubView(this.memory, this.game);
            this.stadiumView = new StadiumView(this.memory, this.game);
            this.managerView = new ManagerView(this.memory, this.game);
            this.waiting = new Label
            {
                Text = "Warte auf Anstoss 3 …\n\nBitte das Spiel starten und einen Spielstand laden.\nUnterstützt wird Version 1.40.",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = A3Theme.Panel,
                ForeColor = A3Theme.Text,
            };

            var observer = new A3Observer(this, this.memory, this.game);
            this.InitShell(this.content, observer.Observe);
            this.ShowWaiting();
        }

        /// <summary>Called by the observer once a career is loaded (also after loading another one).</summary>
        internal void OnGameAttached()
        {
            this.manager = this.game.ActiveManager();
            this.Show(this.page);
        }

        /// <summary>Called by the observer when the game is gone.</summary>
        internal void OnGameLost()
        {
            this.playerView.StopFreezing();
            this.ShowWaiting();
        }

        /// <summary>Shows a message on the waiting screen (e.g. unsupported game version).</summary>
        internal void ShowStatus(string message)
        {
            this.waiting.Text = message;
        }

        private void Show(Page target)
        {
            this.page = target;
            ClubKey? own = this.memory.mProc.Process != null ? this.game.OwnClub(this.manager) : null;
            if (!own.HasValue)
            {
                this.ShowWaiting();
                return;
            }

            switch (target)
            {
                case Page.Club:
                    this.ShowScreen(this.clubView, "Verein: " + this.game.ClubName(own.Value));
                    this.clubView.Manager = this.manager;
                    this.clubView.RefreshValues();
                    break;
                case Page.Stadium:
                    this.ShowScreen(this.stadiumView, "Stadion: " + this.game.ClubName(own.Value));
                    this.stadiumView.Club = own.Value;
                    this.stadiumView.RefreshValues();
                    break;
                case Page.Manager:
                    this.ShowScreen(this.managerView, $"Manager (Spieler {this.manager})");
                    this.managerView.Manager = this.manager;
                    this.managerView.RefreshValues();
                    break;
                case Page.Opponent:
                    ClubKey? opponent = this.game.NextOpponent(own.Value);
                    this.ShowScreen(this.playerView, opponent.HasValue ? "Nächster Gegner: " + this.game.ClubName(opponent.Value) : "Nächster Gegner (keiner geladen)");
                    this.playerView.ShowClub(opponent);
                    break;
                case Page.Other:
                    ClubKey other = this.otherClub.HasValue && this.game.ClubAddress(this.otherClub.Value).HasValue ? this.otherClub.Value : own.Value;
                    this.ShowScreen(this.playerView, "Verein: " + this.game.ClubName(other));
                    this.playerView.ShowClub(other);
                    break;
                default:
                    this.ShowScreen(this.playerView, "Eigener Kader: " + this.game.ClubName(own.Value));
                    this.playerView.ShowClub(own);
                    break;
            }
        }

        // With several human managers (hot seat) the menu lists them; picking one shows his data on the
        // squad, club and manager pages until the turn passes.
        private void ListManagers()
        {
            while (this.managerMenu.DropDownItems.Count > 1)
                this.managerMenu.DropDownItems.RemoveAt(1);
            List<int> managers = this.memory.mProc.Process != null ? this.game.Managers() : new List<int>();
            if (managers.Count < 2)
                return;
            this.managerMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (int number in managers)
            {
                ClubInfo info = this.game.ReadClubInfo(number);
                string name = info == null ? "" : $": {(info.ManagerFirstName + " " + info.ManagerLastName).Trim()} ({info.Name})";
                int picked = number;
                this.managerMenu.DropDownItems.Add(new ToolStripMenuItem($"Spieler {number}{name}", null, (s, e) =>
                {
                    this.manager = picked;
                    this.Show(this.page == Page.Club || this.page == Page.Stadium || this.page == Page.Squad ? this.page : Page.Manager);
                }) { Checked = number == this.manager });
            }
        }

        // A club picked in the player view: the view already shows it, only page and title follow.
        private void OnClubChosen(ClubKey club)
        {
            this.page = Page.Other;
            this.otherClub = club;
            this.ShowScreen(this.playerView, "Verein: " + this.game.ClubName(club));
        }

        private void ShowWaiting() => this.ShowPlaceholder(this.waiting);

        /// <summary>The A3 page of the project's online help (deployed from help/ by pages.yml).</summary>
        internal const string HelpUrl = "https://transfairs.github.io/A3-Trainer/";

        /// <summary>Opens the URL in the browser; tests replace it to avoid launching one.</summary>
        internal Action<string> StartProcess = url => Process.Start(url);

        private void OpenHelp()
        {
            try
            {
                this.StartProcess(HelpUrl);
            }
            catch (Exception ex)
            {
                Logger.Error("Onlinehilfe konnte nicht geöffnet werden", ex);
                MessageBox.Show(this, "Die Onlinehilfe konnte nicht geöffnet werden:\n" + HelpUrl, "Hilfe",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Needs no running game, so it bypasses Show(Page) and leaves the remembered page alone.
        private void ShowAbout() => this.ShowScreen(this.aboutView, "Über");
    }
}
