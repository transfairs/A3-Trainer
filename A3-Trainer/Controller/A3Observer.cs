using A3_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace A3_Trainer.Controller
{
    /// <summary>
    /// Anstoss 3's game observer (polling loop in the shared GameObserver): attaches to the game
    /// once a career is loaded and tells the window when the game appears, changes to another
    /// career (different own club) or goes away.
    /// </summary>
    internal sealed class A3Observer : GameObserver
    {
        private readonly MainWindow window;
        private readonly ProcessMemory memory;
        private readonly A3Game game;

        private int shownPid;
        private ClubKey? shownClub;
        private bool reportedUnsupported;

        /// <summary>Creates the observer for <paramref name="window"/>, attaching <paramref name="memory"/> and reading through <paramref name="game"/>.</summary>
        internal A3Observer(MainWindow window, ProcessMemory memory, A3Game game)
        {
            this.window = window;
            this.memory = memory;
            this.game = game;
        }

        protected override bool ShutDownRequested => this.window.ShutDown;

        // The game's main window is titled exactly "ANSTOSS 3"; TryAttach confirms the build.
        // Under Wine/Proton the title can't be relied on: Wine Mono reports no window titles at all
        // (MainWindowTitle is null), and a game in another Wine desktop has no main window from the
        // trainer's side. So the game's process name counts as well.
        internal override IEnumerable<Process> GetGameCandidates() =>
            Process.GetProcesses().Where(p => IsGameCandidate(SafeGet(() => p.ProcessName), SafeGet(() => p.MainWindowTitle)));

        /// <summary>Process name of the game (without ".exe").</summary>
        internal const string GameProcessName = "anstoss3";

        /// <summary>Whether a process with this name and main window title (either may be null) may be the game.</summary>
        internal static bool IsGameCandidate(string processName, string windowTitle) =>
            (windowTitle != null && windowTitle.StartsWith("ANSTOSS 3")) ||
            string.Equals(processName, GameProcessName, StringComparison.OrdinalIgnoreCase);

        // Process properties throw for processes that exit meanwhile or can't be queried.
        internal static string SafeGet(Func<string> get)
        {
            try
            {
                return get();
            }
            catch (Exception)
            {
                return null;
            }
        }

        protected override bool TryAttach(Process candidate)
        {
            if (!this.memory.OpenProcess(candidate.Id))
                return false;

            if (!this.game.IsSupportedBuild())
            {
                if (!this.reportedUnsupported)
                {
                    Logger.Warn($"Prozess {candidate.Id} ist kein unterstütztes Anstoss 3 (benötigt Version 1.40)");
                    this.reportedUnsupported = true;
                    this.Post(() => this.window.ShowStatus("Nicht unterstützte Anstoss-3-Version (benötigt Version 1.40)."));
                }
                return false;
            }

            // In the menus before a career is loaded there is no own club yet - keep trying.
            if (!this.game.OwnClub().HasValue)
                return false;

            Logger.Debug($"Mit Anstoss 3 verbunden (Prozess {candidate.Id})");
            return true;
        }

        protected override void UpdateGameProcess()
        {
            if (this.gameProcess.HasExited)
            {
                this.gameProcess = null;
                this.ClearGameProcess();
                return;
            }

            ClubKey? club;
            try
            {
                club = this.game.OwnClub();
            }
            catch (Exception ex)
            {
                Logger.Warn("Eigener Verein nicht lesbar", ex);
                return;
            }

            // Loading another career (or going back to the menus) changes or clears the own club.
            if (!club.HasValue)
                return;
            if (this.shownPid == this.gameProcess.Id && this.shownClub.Equals(club))
                return;

            this.shownPid = this.gameProcess.Id;
            this.shownClub = club;
            this.Post(() => this.RefreshView("Anstoss 3", this.window.OnGameAttached));
        }

        protected override void ClearGameProcess()
        {
            if (this.shownPid == 0)
                return;
            this.shownPid = 0;
            this.shownClub = null;
            this.Post(this.window.OnGameLost);
        }

        private void Post(Action action)
        {
            if (this.window.IsHandleCreated && !this.window.IsDisposed)
                this.window.BeginInvoke((MethodInvoker)(() => action()));
        }
    }
}
