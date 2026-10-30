using AnstossTrainer.Shared.Tests;
using A3_Trainer.Controller;
using A3_Trainer.Model;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    public class PlayerViewTests
    {
        private static ListView List(PlayerView view) =>
            (ListView)typeof(PlayerView).GetField("list", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        // The view only reads when a game process is attached; the test process stands in for it,
        // the data itself comes from the fake memory.
        private static PlayerView View(FakeGameMemory m)
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            var view = new PlayerView(memory, new A3Game(m));
            _ = List(view).Handle; // a ListView only tracks its selection once it has a window
            return view;
        }

        private static FakeGameMemory TwoClubs()
        {
            FakeGameMemory m = A3GameTests.Career();
            A3GameTests.PutPlayer(m, 1039, 14, 1, 1, 11);
            A3GameTests.PutPlayer(m, 1041, 14, 1, 2, 12);
            A3GameTests.PutPlayer(m, 1255, 14, 10, 3, 10);
            return m;
        }

        [Fact]
        public void ShowClub_PreselectsTheFirstPlayer()
        {
            StaThread.Run(() =>
            {
                using (PlayerView view = View(TwoClubs()))
                {
                    view.ShowClub(new ClubKey(14, 1));

                    ListView list = List(view);
                    Assert.Equal(2, list.Items.Count);
                    Assert.Single(list.SelectedItems);
                    Assert.Equal(1039, ((Player)list.SelectedItems[0].Tag).Index);
                }
            });
        }

        [Fact]
        public void ShowClub_KeepsTheShownPlayer_AndSelectsTheFirstOneOfAnotherClub()
        {
            StaThread.Run(() =>
            {
                using (PlayerView view = View(TwoClubs()))
                {
                    view.ShowClub(new ClubKey(14, 1));
                    ListView list = List(view);
                    list.Items[1].Selected = true;

                    view.ShowClub(new ClubKey(14, 1)); // reload
                    Assert.Equal(1041, ((Player)list.SelectedItems[0].Tag).Index);

                    view.ShowClub(new ClubKey(14, 10));
                    Assert.Equal(1255, ((Player)list.SelectedItems[0].Tag).Index);
                }
            });
        }

        [Fact]
        public void ShowClub_WithoutAClub_SelectsNothing()
        {
            StaThread.Run(() =>
            {
                using (PlayerView view = View(TwoClubs()))
                {
                    view.ShowClub(null);
                    Assert.Empty(List(view).Items);
                }
            });
        }
    }
}
