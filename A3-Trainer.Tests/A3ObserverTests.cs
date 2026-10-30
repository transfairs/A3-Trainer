using A3_Trainer.Controller;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>
    /// The A3 game observer: which processes it attaches to, what it tells the window, and how it
    /// notices a new career or the game going away. The test process stands in for the game.
    /// </summary>
    public class A3ObserverTests
    {
        private static bool TryAttach(A3Observer observer, Process candidate) =>
            (bool)TestHelpers.Invoke(observer, "TryAttach", candidate);

        private static void Update(A3Observer observer) => TestHelpers.Invoke(observer, "UpdateGameProcess");

        private static void Clear(A3Observer observer) => TestHelpers.Invoke(observer, "ClearGameProcess");

        private static Label Waiting(MainWindow window) => TestHelpers.Get<Label>(window, "waiting");

        [Fact]
        public void Candidates_AndShutDown_ComeFromTheSystemAndTheWindow() => StaThread.Run(() =>
        {
            using (var window = new MainWindow())
            {
                var observer = new A3Observer(window, new ProcessMemory(), new A3Game(A3GameTests.Career()));

                Assert.DoesNotContain(observer.GetGameCandidates().ToList(), p => p.Id == Process.GetCurrentProcess().Id);
                Assert.False((bool)typeof(A3Observer).GetProperty("ShutDownRequested", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(observer));
            }
        });

        [Fact]
        public void TryAttach_FailsWhenTheProcessCantBeOpened() => StaThread.Run(() =>
        {
            using (var window = new MainWindow())
            {
                var observer = new A3Observer(window, new ProcessMemory(), new A3Game(A3GameTests.Career()));

                // The System process (PID 4) can't be opened without elevation.
                Assert.False(TryAttach(observer, Process.GetProcessById(4)));
            }
        });

        [Fact]
        public void TryAttach_ToAnUnsupportedBuild_BeforeTheWindowExists_PostsNothing() => StaThread.Run(() =>
        {
            using (var window = new MainWindow())
            {
                var observer = new A3Observer(window, new ProcessMemory(), new A3Game(new FakeGameMemory()));

                Assert.False(TryAttach(observer, Process.GetCurrentProcess()));
                Assert.False(window.IsHandleCreated);
                Assert.StartsWith("Warte auf Anstoss 3", Waiting(window).Text);
            }
        });

        [Fact]
        public void TryAttach_ToAnUnsupportedBuild_SaysSoOnce() => StaThread.Run(() =>
        {
            using (var window = new MainWindow())
            {
                _ = window.Handle;
                var observer = new A3Observer(window, new ProcessMemory(), new A3Game(new FakeGameMemory()));

                Assert.False(TryAttach(observer, Process.GetCurrentProcess()));
                Application.DoEvents();
                Assert.StartsWith("Nicht unterstützte Anstoss-3-Version", Waiting(window).Text);

                window.ShowStatus("");
                Assert.False(TryAttach(observer, Process.GetCurrentProcess()));
                Application.DoEvents();
                Assert.Equal("", Waiting(window).Text);
            }
        });

        [Fact]
        public void TryAttach_WaitsForACareer() => StaThread.Run(() =>
        {
            using (var window = new MainWindow())
            {
                FakeGameMemory m = A3GameTests.Career();
                m.PutUInt16(A3Addresses.ManagerClubNumber, 2);
                var observer = new A3Observer(window, new ProcessMemory(), new A3Game(m));
                Assert.False(TryAttach(observer, Process.GetCurrentProcess()));

                m.PutUInt16(A3Addresses.ManagerClubNumber, 1);
                Assert.True(TryAttach(observer, Process.GetCurrentProcess()));
            }
        });

        [Fact]
        public void Update_RefreshesTheWindowOnceForEachCareer_AndClearsWhenTheGameEnds() => StaThread.Run(() =>
        {
            using (var window = new MainWindow())
            {
                _ = window.Handle;
                FakeGameMemory m = A3GameTests.Career();
                var observer = new A3Observer(window, new ProcessMemory(), new A3Game(m));
                TestHelpers.Set(observer, "gameProcess", Process.GetCurrentProcess());

                Clear(observer);                                   // nothing shown yet: nothing to clear
                Update(observer);
                Assert.Equal(Process.GetCurrentProcess().Id, TestHelpers.Get<int>(observer, "shownPid"));
                Application.DoEvents();                            // runs OnGameAttached

                Update(observer);                                  // same career: no new refresh
                m.PutUInt16(A3Addresses.ManagerClubNumber, 2);
                Update(observer);                                  // back in the menus: keep what's shown
                Assert.Equal(Process.GetCurrentProcess().Id, TestHelpers.Get<int>(observer, "shownPid"));

                using (Process ended = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { CreateNoWindow = true, UseShellExecute = false }))
                {
                    ended.WaitForExit();
                    TestHelpers.Set(observer, "gameProcess", ended);
                    Update(observer);
                }
                Assert.Null(TestHelpers.Get<Process>(observer, "gameProcess"));
                Assert.Equal(0, TestHelpers.Get<int>(observer, "shownPid"));
                Application.DoEvents();                            // runs OnGameLost
                Assert.Same(Waiting(window), TestHelpers.Get<Panel>(window, "content").Controls[0]);
            }
        });

        [Fact]
        public void Update_WhenTheGameCantBeRead_KeepsGoing() => StaThread.Run(() =>
        {
            using (var window = new MainWindow())
            {
                var observer = new A3Observer(window, new ProcessMemory(), new A3Game(new BrokenGameMemory()));
                TestHelpers.Set(observer, "gameProcess", Process.GetCurrentProcess());

                Update(observer);

                Assert.Equal(0, TestHelpers.Get<int>(observer, "shownPid"));
            }
        });
    }
}
