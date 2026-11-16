using A3_Trainer.Controller;
using A3_Trainer.Model;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>Reading and editing a club's fans, board, fan friendship and arch rival.</summary>
    public class IdentityTests
    {
        private const uint Copy = 0x00A6BFB8;

        private static uint Club(int index) => A3Addresses.ClubArray + (uint)(index * A3Addresses.ClubSize);

        // Germany (14) from club 0: München (1) is friends with Frankfurt (2), Dortmund (3) with Hamburg (10);
        // club 20 belongs to the next country. München is loaded, its working copy is what counts.
        internal static FakeGameMemory WithIdentities()
        {
            FakeGameMemory m = A3GameTests.Career();
            A3GameTests.PutClub(m, 2, "Fortuna Frankfurt");
            A3GameTests.PutClub(m, 3, "Preußen Dortmund");
            A3GameTests.PutClub(m, 20, "Anderes Land");
            foreach (int index in new[] { 1, 2, 3, 10 })
            {
                m.Put(Club(index) + A3Addresses.ClubOwnCountry, 14);
                m.Put(Club(index) + A3Addresses.ClubFriend, 0xFF, 0xFF);
            }
            m.Put(Club(20) + A3Addresses.ClubOwnCountry, 15);
            m.Put(Club(1) + A3Addresses.ClubFanName, Encoding.ASCII.GetBytes("Bayern\0"));
            m.Put(Club(1) + A3Addresses.ClubFanCrowd, 3, 0);      // stale record
            m.Put(Club(1) + A3Addresses.ClubFinancialPower, 5, 0, 2, 1);   // reich, max. wahre Horden, stale hooligans, media city
            m.Put(Club(1) + A3Addresses.ClubPublic, 3, 0, 0, 0);           // AG and south
            m.Put(Club(2) + A3Addresses.ClubFriend, 1);
            m.Put(Club(3) + A3Addresses.ClubFriend, 10);
            m.Put(Club(10) + A3Addresses.ClubFriend, 3);

            m.Map(A3Addresses.ClubCache, A3Addresses.ClubCacheSlots * A3Addresses.ClubCacheEntrySize);
            m.Put(A3Addresses.ClubCache, 1, 14, 1);
            m.PutUInt32(A3Addresses.ClubCache + A3Addresses.ClubCacheObject, Copy);
            m.Map(Copy, 0x1390);
            m.Put(Copy + A3Addresses.ClubCopyFanCrowd, 0, 2);
            m.Put(Copy + A3Addresses.ClubCopyFriend, 2, 3, 2);
            m.Put(Copy + A3Addresses.ClubCopyHooligans, 3, 1);
            return m;
        }

        private static ProcessMemory Attached()
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            return memory;
        }

        [Fact]
        public void ReadIdentity_PrefersTheWorkingCopy()
        {
            var game = new A3Game(WithIdentities());

            ClubIdentity munich = game.ReadIdentity(new ClubKey(14, 1));
            Assert.Equal("Bayern", munich.FanName);
            Assert.Equal(0, munich.FanCrowd);
            Assert.Equal(2, munich.FanType);
            Assert.Equal(2, munich.Board);
            Assert.Equal(2, munich.Friend);
            Assert.Equal(3, munich.Rival);
            Assert.Equal(5, munich.FinancialPower);
            Assert.Equal(0, munich.MaxFanCrowd);
            Assert.Equal(3, munich.Hooligans);
            Assert.True(munich.MediaCity);
            Assert.Equal(1, munich.Region);

            ClubIdentity hamburg = game.ReadIdentity(new ClubKey(14, 10));
            Assert.Equal(3, hamburg.Friend);
            Assert.Equal(-1, hamburg.Rival);
            Assert.False(hamburg.MediaCity);
            Assert.Equal(0, hamburg.Region);
            Assert.Null(game.ReadIdentity(new ClubKey(14, 5)));
        }

        [Fact]
        public void CountryClubs_StopAtTheNextCountry()
        {
            var game = new A3Game(WithIdentities());
            Assert.Equal(new[] { 1, 2, 3, 10 }, game.CountryClubs(14).Select(c => c.Key));
            Assert.Equal("Preußen Dortmund", game.CountryClubs(14)[2].Value);
        }

        [Fact]
        public void SaveIdentity_MovesTheFriendshipOnBothSides()
        {
            FakeGameMemory m = WithIdentities();
            var game = new A3Game(m);
            ClubIdentity original = game.ReadIdentity(new ClubKey(14, 1));
            ClubIdentity edited = original.Clone();
            edited.Friend = 3;
            edited.FanType = 4;
            edited.FanName = "Münchner";

            game.SaveIdentity(original, edited);

            Assert.Equal(3, m.Get(Club(1) + A3Addresses.ClubFriend));
            Assert.Equal(3, m.Get(Copy + A3Addresses.ClubCopyFriend));
            Assert.Equal(4, m.Get(Club(1) + A3Addresses.ClubFanType));
            Assert.Equal(4, m.Get(Copy + A3Addresses.ClubCopyFanType));
            Assert.Equal(0xFF, m.Get(Club(2) + A3Addresses.ClubFriend));    // the old friend
            Assert.Equal(1, m.Get(Club(3) + A3Addresses.ClubFriend));       // the new one
            Assert.Equal(0xFF, m.Get(Club(10) + A3Addresses.ClubFriend));   // the new one's old friend
            Assert.Equal("Münchner", game.ReadIdentity(new ClubKey(14, 1)).FanName);
            Assert.Null(m.WrittenAt(Club(1) + A3Addresses.ClubRival));
            Assert.Null(m.WrittenAt(Club(1) + A3Addresses.ClubFanCrowd));
            Assert.Null(m.WrittenAt(Club(1) + A3Addresses.ClubPublic));
            Assert.Null(m.WrittenAt(Club(1) + A3Addresses.ClubHooligans));

            // Ending it clears both sides.
            original = game.ReadIdentity(new ClubKey(14, 1));
            edited = original.Clone();
            edited.Friend = -1;
            game.SaveIdentity(original, edited);
            Assert.Equal(0xFF, m.Get(Club(1) + A3Addresses.ClubFriend));
            Assert.Equal(0xFF, m.Get(Club(3) + A3Addresses.ClubFriend));
        }

        [Fact]
        public void SaveIdentity_WritesTheClubEditorValues()
        {
            FakeGameMemory m = WithIdentities();
            var game = new A3Game(m);
            ClubIdentity original = game.ReadIdentity(new ClubKey(14, 1));
            ClubIdentity edited = original.Clone();
            edited.FinancialPower = 1;
            edited.MaxFanCrowd = 2;
            edited.Hooligans = 0;
            edited.MediaCity = false;
            edited.Region = 0;

            game.SaveIdentity(original, edited);

            Assert.Equal(1, m.Get(Club(1) + A3Addresses.ClubFinancialPower));
            Assert.Equal(2, m.Get(Club(1) + A3Addresses.ClubMaxFanCrowd));
            Assert.Equal(0, m.Get(Club(1) + A3Addresses.ClubHooligans));
            Assert.Equal(0, m.Get(Copy + A3Addresses.ClubCopyHooligans));
            Assert.Equal(0, m.Get(Copy + A3Addresses.ClubCopyMediaCity));
            Assert.Equal(1, m.Get(Club(1) + A3Addresses.ClubPublic));      // the AG stays

            original = game.ReadIdentity(new ClubKey(14, 1));
            edited = original.Clone();
            edited.Region = 1;
            game.SaveIdentity(original, edited);
            Assert.Equal(3, m.Get(Club(1) + A3Addresses.ClubPublic));
        }

        [Fact]
        public void View_ShowsAndSavesTheIdentity() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithIdentities();
            using (var view = new IdentityView(Attached(), new A3Game(m)) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Assert.Equal("Bayern", TestHelpers.Get<TextBox>(view, "fanName").Text);
                Assert.Equal("wahre Horden", TestHelpers.Get<ComboBox>(view, "fanCrowd").Text);
                Assert.Equal("verwöhnt", TestHelpers.Get<ComboBox>(view, "fanType").Text);
                Assert.Equal("nervös", TestHelpers.Get<ComboBox>(view, "board").Text);
                ComboBox friend = TestHelpers.Get<ComboBox>(view, "friend");
                ComboBox rival = TestHelpers.Get<ComboBox>(view, "rival");
                Assert.Equal(new[] { "–", "Fortuna Frankfurt", "Preußen Dortmund", "Hamburger FC" }, friend.Items.Cast<string>());
                Assert.Equal("Fortuna Frankfurt", friend.Text);
                Assert.Equal("Preußen Dortmund", rival.Text);
                Assert.Equal("reich", TestHelpers.Get<ComboBox>(view, "financialPower").Text);
                Assert.Equal("viele und schlimme", TestHelpers.Get<ComboBox>(view, "hooligans").Text);
                Assert.Equal("wahre Horden", TestHelpers.Get<ComboBox>(view, "maxFanCrowd").Text);
                Assert.Equal("Süd", TestHelpers.Get<ComboBox>(view, "region").Text);
                Assert.True(TestHelpers.Get<CheckBox>(view, "mediaCity").Checked);

                rival.SelectedIndex = 3;
                TestHelpers.Get<ComboBox>(view, "board").SelectedIndex = 5;
                TestHelpers.Get<TextBox>(view, "fanName").Text = " ";
                TestHelpers.Get<ComboBox>(view, "maxFanCrowd").SelectedIndex = 3;   // below the current crowd
                TestHelpers.Get<ComboBox>(view, "financialPower").SelectedIndex = 6;
                TestHelpers.Get<ComboBox>(view, "hooligans").SelectedIndex = 1;
                TestHelpers.Get<ComboBox>(view, "region").SelectedIndex = 0;
                TestHelpers.Get<CheckBox>(view, "mediaCity").Checked = false;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));

                Assert.Equal("Gespeichert", TestHelpers.Get<Label>(view, "status").Text);
                Assert.Equal(10, m.Get(Copy + A3Addresses.ClubCopyRival));
                Assert.Equal(5, m.Get(Club(1) + A3Addresses.ClubBoard));
                Assert.Equal("Bayern", TestHelpers.Get<TextBox>(view, "fanName").Text);   // an emptied name stays
                Assert.Null(m.WrittenAt(Copy + A3Addresses.ClubCopyFriend));   // friendship untouched
                Assert.Equal(3, m.Get(Club(1) + A3Addresses.ClubMaxFanCrowd));
                Assert.Equal(3, m.Get(Copy + A3Addresses.ClubCopyFanCrowd));      // lowered to the maximum
                Assert.Equal(6, m.Get(Club(1) + A3Addresses.ClubFinancialPower));
                Assert.Equal(1, m.Get(Copy + A3Addresses.ClubCopyHooligans));
                Assert.Equal(0, m.Get(Copy + A3Addresses.ClubCopyMediaCity));
                Assert.Equal(1, m.Get(Club(1) + A3Addresses.ClubPublic));
                Assert.Equal("Durchschnitt", TestHelpers.Get<ComboBox>(view, "fanCrowd").Text);
            }
        });

        [Fact]
        public void View_KeepsAnUnlistedFriend_AndReportsErrors() => StaThread.Run(() =>
        {
            FakeGameMemory m = WithIdentities();
            m.Put(Copy + A3Addresses.ClubCopyFriend, 40);       // no club with a name there
            m.Put(Copy + A3Addresses.ClubCopyFanCrowd, 99);
            using (var view = new IdentityView(Attached(), new A3Game(m)) { Club = new ClubKey(14, 1) })
            {
                view.RefreshValues();
                Assert.Equal(-1, TestHelpers.Get<ComboBox>(view, "friend").SelectedIndex);
                Assert.Equal(-1, TestHelpers.Get<ComboBox>(view, "fanCrowd").SelectedIndex);
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));
                Assert.Null(m.WrittenAt(Copy + A3Addresses.ClubCopyFriend));
                Assert.Null(m.WrittenAt(Copy + A3Addresses.ClubCopyFanCrowd));

                TestHelpers.Set(view, "game", new A3Game(new ReadOnlyGameMemory(m)));
                TestHelpers.Get<ComboBox>(view, "fanType").SelectedIndex = 0;
                TestHelpers.Click(TestHelpers.Get<Button>(view, "save"));
                Assert.Equal("Fehler beim Speichern", TestHelpers.Get<Label>(view, "status").Text);

                view.Club = new ClubKey(14, 5);
                view.RefreshValues();
                Assert.Equal("Kein Verein gefunden", TestHelpers.Get<Label>(view, "status").Text);
                Assert.False(TestHelpers.Get<Button>(view, "save").Enabled);
            }
        });
    }
}
