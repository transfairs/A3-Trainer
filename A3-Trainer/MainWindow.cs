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
        private readonly IdentityView identityView;
        private readonly StadiumView stadiumView;
        private readonly BoardView boardView;
        private readonly FinancesView financesView;
        private readonly InvestmentsView investmentsView;
        private readonly PagesView clubPages;
        private readonly ManagerView managerView;
        private readonly PersonView personView;
        private readonly StocksView stocksView;
        private readonly CareerView careerView;
        private readonly PagesView managerPages;
        private readonly HelpView helpView = new HelpView();
        private readonly InterviewEffects interviewEffects;
        private readonly ToolStripMenuItem interviewItem = new ToolStripMenuItem("Interviews: Wirkung der Antworten zeigen") { CheckOnClick = true };
        private readonly Timer mechanicsTimer = new Timer { Interval = 250 };
        private readonly Label waiting;

        private enum Page { Squad, Opponent, Amateurs, Other, Club, OtherClub, Manager }
        private Page page = Page.Squad;
        // The club choice above the tabs of the club page.
        private readonly ComboBox clubCountryBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox clubClubBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ClubPicker clubPicker;
        // The club last picked in the club selection of the player or the club page.
        private ClubKey? otherClub;
        // The human manager whose squad, club and data the pages show; follows the turn.
        private int manager = 1;
        private readonly ToolStripMenuItem managerMenu = new ToolStripMenuItem("Trainer");

        /// <summary>Builds the menu, the views and the waiting screen and starts watching for the game.</summary>
        internal MainWindow()
        {
            this.Text = "A3-Trainer";
            this.ShowDialogBox = dialog => dialog.ShowDialog(this);
            this.Font = new Font("Microsoft Sans Serif", 8.25f);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.ClientSize = new Size(630, 447);
            this.BackColor = A3Theme.Panel;
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            var players = new ToolStripMenuItem("Spieler");
            players.DropDownItems.Add("Eigener Kader", null, (s, e) => this.Go(Page.Squad));
            this.opponentItem = new ToolStripMenuItem("Nächster Gegner", null, (s, e) => this.Go(Page.Opponent));
            players.DropDownItems.Add(this.opponentItem);
            players.DropDownItems.Add("Amateure", null, (s, e) => this.Go(Page.Amateurs));
            players.DropDownItems.Add("Anderer Verein", null, (s, e) => this.Go(Page.Other));
            var club = new ToolStripMenuItem("Verein");
            club.DropDownItems.Add("Eigener Verein", null, (s, e) => this.Go(Page.Club));
            club.DropDownItems.Add("Anderer Verein", null, (s, e) => this.Go(Page.OtherClub));
            this.managerMenu.DropDownItems.Add("Manager", null, (s, e) => this.Go(Page.Manager));
            this.managerMenu.DropDownOpening += (s, e) => this.ListManagers();
            var mechanics = new ToolStripMenuItem("Spielmechaniken");
            mechanics.DropDownItems.Add(this.interviewItem);
            var help = new ToolStripMenuItem("?");
            help.DropDownItems.Add("Hilfe", null, (s, e) => this.ShowHelp());
            help.DropDownItems.Add("Onlinehilfe", null, (s, e) => this.OpenHelp());
            help.DropDownItems.Add("Über", null, (s, e) => this.ShowAbout());
            this.menu.Items.AddRange(new ToolStripItem[] { club, players, this.managerMenu, mechanics, help });
            this.AddNavigationButtons(this.menu);
            A3Theme.ApplyMenu(this.menu);

            this.Controls.Add(this.content);
            this.Controls.Add(this.menu);
            this.MainMenuStrip = this.menu;

            var live = new LiveGameMemory(this.memory);
            this.game = new A3Game(live);
            this.playerView = new PlayerView(this.memory, this.game) { Transfers = new TransferInjector(live, live) };
            this.interviewEffects = new InterviewEffects(live, live);
            this.interviewItem.CheckedChanged += (s, e) => this.SwitchInterviewEffects(this.interviewItem.Checked);
            this.mechanicsTimer.Tick += (s, e) => this.TickMechanics();
            this.Disposed += (s, e) => this.mechanicsTimer.Dispose();
            this.playerView.ClubChosen += this.OnClubChosen;
            this.clubView = new ClubView(this.memory, this.game);
            this.identityView = new IdentityView(this.memory, this.game);
            this.stadiumView = new StadiumView(this.memory, this.game);
            this.boardView = new BoardView(this.memory, this.game);
            this.financesView = new FinancesView(this.memory, this.game);
            this.investmentsView = new InvestmentsView(this.memory, this.game);
            this.clubPicker = new ClubPicker(this.clubCountryBox, this.clubClubBox);
            this.clubPicker.Picked += this.OnClubPagePicked;
            this.clubPages = new PagesView().Add("Verein", this.clubView).Add("Identität", this.identityView).Add("Stadion", this.stadiumView).Add("Finanzen", this.financesView).Add("Anlagen", this.investmentsView).Add("Präsidium", this.boardView)
                .AddClubChoice(this.clubPicker);
            this.managerView = new ManagerView(this.memory, this.game);
            this.personView = new PersonView(this.memory, this.game);
            this.stocksView = new StocksView(this.memory, this.game);
            this.careerView = new CareerView(this.memory, this.game);
            this.managerPages = new PagesView().Add("Manager", this.managerView).Add("Person", this.personView).Add("Karriere", this.careerView).Add("Aktien", this.stocksView);
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
            this.RecordNavigation(this.HistoryKey(this.page), this.Restorer(this.page));
        }

        /// <summary>Called by the observer when the game is gone.</summary>
        internal void OnGameLost()
        {
            this.playerView.StopFreezing();
            this.interviewEffects.Reset();
            this.ShowWaiting();
        }

        /// <summary>Shows a message on the waiting screen (e.g. unsupported game version).</summary>
        internal void ShowStatus(string message)
        {
            this.waiting.Text = message;
        }

        // Shows a page from the menu and notes it in the back/forward history, together with the
        // manager and the picked club it shows, so going back brings exactly that screen again.
        private void Go(Page target) => this.Navigate(this.HistoryKey(target), this.Restorer(target));

        private string HistoryKey(Page target) =>
            IsOther(target) ? $"{target}/{this.manager}/{this.otherClub}" : $"{target}/{this.manager}";

        private static bool IsOther(Page target) => target == Page.Other || target == Page.OtherClub;

        private Action Restorer(Page target)
        {
            int shownManager = this.manager;
            ClubKey? shownClub = this.otherClub;
            return () =>
            {
                this.manager = shownManager;
                if (IsOther(target))
                    this.otherClub = shownClub;
                this.Show(target);
            };
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
                    this.ShowClubPage(own.Value);
                    this.clubPicker.Fill(this.game, this.game.ReadCountryNames(), own.Value);
                    break;
                case Page.OtherClub:
                    ClubKey picked = this.OtherOrOwn(own.Value);
                    this.ShowClubPage(picked);
                    this.clubPicker.Fill(this.game, this.game.ReadCountryNames(), picked);
                    break;
                case Page.Manager:
                    this.ShowScreen(this.managerPages, $"Manager (Spieler {this.manager})");
                    this.managerView.Manager = this.manager;
                    this.managerView.RefreshValues();
                    this.personView.Manager = this.manager;
                    this.personView.RefreshValues();
                    this.careerView.Manager = this.manager;
                    this.careerView.RefreshValues();
                    this.stocksView.Manager = this.manager;
                    this.stocksView.RefreshValues();
                    break;
                case Page.Opponent:
                    ClubKey? opponent = this.game.NextOpponent(own.Value);
                    this.ShowScreen(this.playerView, opponent.HasValue ? "Nächster Gegner: " + this.game.ClubName(opponent.Value) : "Nächster Gegner (keiner geladen)");
                    this.playerView.ShowClub(opponent);
                    break;
                case Page.Amateurs:
                    ClubKey? amateurs = this.game.AmateurClub(own.Value);
                    this.ShowScreen(this.playerView, amateurs.HasValue ? "Amateure: " + this.game.ClubName(amateurs.Value) : "Amateure (keine Amateurmannschaft)");
                    this.playerView.ShowClub(amateurs);
                    break;
                case Page.Other:
                    ClubKey other = this.OtherOrOwn(own.Value);
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
                    this.Go(this.page == Page.Club || this.page == Page.Squad ? this.page : Page.Manager);
                }) { Checked = number == this.manager });
            }
        }

        // The club last picked, while the game still has it, else the own club.
        private ClubKey OtherOrOwn(ClubKey own) =>
            this.otherClub.HasValue && this.game.ClubAddress(this.otherClub.Value).HasValue ? this.otherClub.Value : own;

        // The club page's tabs all show the same club.
        private void ShowClubPage(ClubKey club)
        {
            this.ShowScreen(this.clubPages, "Verein: " + this.game.ClubName(club));
            this.clubView.Club = club;
            this.clubView.RefreshValues();
            this.identityView.Club = club;
            this.identityView.RefreshValues();
            this.stadiumView.Club = club;
            this.stadiumView.RefreshValues();
            this.financesView.Club = club;
            this.financesView.RefreshValues();
            this.investmentsView.Club = club;
            this.investmentsView.RefreshValues();
            this.boardView.Club = club;
            this.boardView.RefreshValues();
        }

        // A club picked on the club page: its tabs follow, the picker already shows it.
        private void OnClubPagePicked(ClubKey club)
        {
            this.page = Page.OtherClub;
            this.otherClub = club;
            this.ShowClubPage(club);
            this.RecordNavigation(this.HistoryKey(Page.OtherClub), this.Restorer(Page.OtherClub));
        }

        // A club picked in the player view: the view already shows it, only page and title follow.
        private void OnClubChosen(ClubKey club)
        {
            this.page = Page.Other;
            this.otherClub = club;
            this.ShowScreen(this.playerView, "Verein: " + this.game.ClubName(club));
            this.RecordNavigation(this.HistoryKey(Page.Other), this.Restorer(Page.Other));
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

        // The help needs no running game either; it is built from the online help when first shown.
        private void ShowHelp() => this.Navigate("Help", () =>
        {
            this.helpView.EnsureHelpLoaded();
            this.ShowScreen(this.helpView, "Hilfe");
        });

        // Game mechanics work only while the game runs; nothing of it goes into the save game.
        private void SwitchInterviewEffects(bool on)
        {
            if (this.memory.mProc.Process != null)
                this.interviewEffects.SetEnabled(on);
            else if (!on)
                this.interviewEffects.Reset();
            this.mechanicsTimer.Enabled = on;
        }

        private void TickMechanics()
        {
            if (this.memory.mProc.Process == null)
                return;
            if (!this.interviewEffects.Enabled)
                this.interviewEffects.SetEnabled(true);
            try
            {
                this.interviewEffects.Tick();
            }
            catch (Exception ex)
            {
                Logger.Error("Interview-Wirkungen konnten nicht angezeigt werden", ex);
            }
        }

        /// <summary>Shows a dialog modally over the window; tests replace it so no dialog blocks them.</summary>
        internal Func<Form, DialogResult> ShowDialogBox;

        // A dialog, so the page shown stays and nothing goes into the history.
        private void ShowAbout()
        {
            using (var about = new AboutDialog())
                this.ShowDialogBox(about);
        }
    }
}
