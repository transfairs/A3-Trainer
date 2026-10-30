using A3_Trainer.Controller;
using A3_Trainer.Model;
using A3_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Xunit;

namespace A3_Trainer.Tests
{
    /// <summary>The player editor: what it shows for different players, and saving, healing and their checks.</summary>
    public class PlayerViewEditTests
    {
        private const int Keeper = 1039;     // TO, darling, unknown nationality and skin
        private const int Defender = 1041;   // IV, hated, side positions LV/RV, injured, no birth date or foot
        private const int Striker = 1042;    // ST with a working copy, injured for 12 more days
        private const int Midfielder = 1043; // OM, groggy

        private static uint Record(int index) => A3Addresses.PlayerArray + (uint)(index * A3Addresses.PlayerSize);

        private static FakeGameMemory Squad()
        {
            FakeGameMemory m = A3GameTests.Career();
            m.Map(A3Addresses.CountryNames, A3Addresses.CountrySlots * 4);
            string[] countries = { "Albanien", "Deutschland", "England" };
            for (int i = 0; i < countries.Length; i++)
            {
                uint text = 0x01000000 + (uint)(i * 0x40);
                m.Put(text, Encoding.GetEncoding(1252).GetBytes(countries[i] + "\0"));
                m.PutUInt32(A3Addresses.CountryNames + (uint)(i * 4), text);
            }

            A3GameTests.PutPlayer(m, Keeper, 14, 1, 1, 11);
            m.PutUInt32(Record(Keeper) + RecordOffset.Flags, 0x10000);
            m.Put(Record(Keeper) + RecordOffset.Appearance, 5);           // no skin colour of the editor
            m.Put(Record(Keeper) + RecordOffset.SecondNationality, 9);    // beyond the country list

            A3GameTests.PutPlayer(m, Defender, 14, 1, 3, 10);
            m.PutUInt32(Record(Defender) + RecordOffset.Flags, 0x400);
            m.Put(Record(Defender) + RecordOffset.SidePosition1, 4, 5);
            m.PutUInt16(Record(Defender) + RecordOffset.InjuryTo, 300);
            m.Put(Record(Defender) + RecordOffset.BirthDay, 0, 0, 0, 0);
            m.Put(Record(Defender) + RecordOffset.FootAndReputation, 0x80);
            m.Put(Record(Defender) + RecordOffset.Nationality, 1, 2);

            A3GameTests.PutPlayer(m, Striker, 14, 1, 10, 12);
            A3GameTests.PutPlayer(m, Midfielder, 14, 1, 9, 9);
            m.PutUInt16(Record(Midfielder) + RecordOffset.Groggy, 1);

            A3GameTests.PutCache(m, Striker);
            m.PutUInt16(0x06000000 + 8 + CompactOffset.InjuryDays, 12);
            return m;
        }

        private static ProcessMemory Attached()
        {
            var memory = new ProcessMemory();
            memory.mProc.Process = Process.GetCurrentProcess();
            return memory;
        }

        private static PlayerView View(IGameMemory m, ProcessMemory memory = null)
        {
            var view = new PlayerView(memory ?? Attached(), new A3Game(m));
            _ = TestHelpers.Get<ListView>(view, "list").Handle;
            view.ShowClub(new ClubKey(14, 1));
            return view;
        }

        private static void Select(PlayerView view, int index)
        {
            ListView list = TestHelpers.Get<ListView>(view, "list");
            list.Items.Cast<ListViewItem>().Single(i => ((Player)i.Tag).Index == index).Selected = true;
        }

        private static T Field<T>(PlayerView view, string name) => TestHelpers.Get<T>(view, name);

        private static string Status(PlayerView view) => Field<Label>(view, "status").Text;

        [Fact]
        public void ShowPlayer_KeeperWithUnknownValues() => StaThread.Run(() =>
        {
            using (PlayerView view = View(Squad()))
            {
                Select(view, Keeper);

                Assert.Equal("15", Field<NumericUpDown>(view, "birthDay").Value.ToString());
                Assert.Equal("30", Field<Label>(view, "ageValue").Text);
                Assert.Equal(-1, Field<ComboBox>(view, "nationality").SelectedIndex);
                Assert.Equal(-1, Field<ComboBox>(view, "secondNationality").SelectedIndex);
                Assert.DoesNotContain(Field<RadioButton[]>(view, "skinRadios"), r => r.Checked);
                Assert.True(Field<RadioButton[]>(view, "audienceRadios")[1].Checked);   // Liebling
                Assert.True(Field<GroupBox>(view, "keeperGroup").Enabled);
                Assert.False(Field<GroupBox>(view, "fieldGroup").Enabled);
                Assert.Equal("gesund", Field<Label>(view, "injuryValue").Text);
                Assert.False(Field<Button>(view, "heal").Enabled);
                Assert.Equal("Keine", Field<ComboBox>(view, "secondNationality").Items[0]);
                Assert.Equal("Albanien", Field<ComboBox>(view, "nationality").Items[0]);
            }
        });

        [Fact]
        public void ShowPlayer_InjuredHatedDefenderWithoutBirthDateOrFoot() => StaThread.Run(() =>
        {
            using (PlayerView view = View(Squad()))
            {
                ListViewItem item = Field<ListView>(view, "list").Items.Cast<ListViewItem>().Single(i => ((Player)i.Tag).Index == Defender);
                Assert.Equal(Color.DarkRed, item.ForeColor);

                Select(view, Defender);

                Assert.Equal(0, Field<NumericUpDown>(view, "birthYear").Value);
                Assert.Equal("", Field<Label>(view, "ageValue").Text);
                Assert.Equal(-1, Field<ComboBox>(view, "foot").SelectedIndex);
                Assert.Equal(1, Field<ComboBox>(view, "nationality").SelectedIndex);
                Assert.Equal(2, Field<ComboBox>(view, "secondNationality").SelectedIndex);
                Assert.True(Field<RadioButton[]>(view, "audienceRadios")[2].Checked);   // Haßfigur
                Assert.Equal(new[] { 4, 5 }, Field<CheckBox[]>(view, "sideChecks").Where(c => c.Checked).Select(c => (int)c.Tag).OrderBy(t => t));
                Assert.Equal("verletzt", Field<Label>(view, "injuryValue").Text);
                Assert.True(Field<Button>(view, "heal").Enabled);
            }
        });

        [Fact]
        public void ShowPlayer_InjuryOfALoadedPlayerAndAGroggyOne() => StaThread.Run(() =>
        {
            using (PlayerView view = View(Squad()))
            {
                Select(view, Striker);
                Assert.Equal("noch 12 Tage (Art 0)", Field<Label>(view, "injuryValue").Text);
                Assert.False(Field<Label>(view, "notLoaded").Visible);

                Select(view, Midfielder);
                Assert.Equal("angeschlagen", Field<Label>(view, "injuryValue").Text);
                Assert.True(Field<Button>(view, "heal").Enabled);
            }
        });

        [Fact]
        public void Save_WritesTheEditedValues() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            using (PlayerView view = View(m))
            {
                Select(view, Defender);
                uint r = Record(Defender);

                Field<NumericUpDown>(view, "birthDay").Value = 1;
                Field<NumericUpDown>(view, "birthMonth").Value = 2;
                Field<NumericUpDown>(view, "birthYear").Value = 1980;
                Assert.Equal("19", Field<Label>(view, "ageValue").Text);
                Field<RadioButton[]>(view, "positionRadios").Single(p => (int)p.Tag == 10).Checked = true;
                Field<CheckBox[]>(view, "sideChecks").Single(c => (int)c.Tag == 5).Checked = false;
                Field<ComboBox>(view, "nationality").SelectedIndex = 2;
                Field<ComboBox>(view, "secondNationality").SelectedIndex = 0;
                Field<RadioButton[]>(view, "skinRadios")[2].Checked = true;
                Field<NumericUpDown>(view, "strength").Value = 12;
                Field<ComboBox>(view, "talent").SelectedIndex = 1;
                Field<NumericUpDown>(view, "freshness").Value = 80;
                Field<ComboBox>(view, "foot").SelectedIndex = 1;
                Field<NumericUpDown>(view, "shirt").Value = 7;
                Field<CheckBox[]>(view, "fieldPlus")[0].Checked = true;
                Field<CheckBox[]>(view, "fieldMinus")[2].Checked = true;
                Field<CheckedListBox>(view, "traits").SetItemChecked(3, true);
                Field<CheckedListBox>(view, "character").SetItemChecked(0, true);
                Field<NumericUpDown>(view, "salary").Value = 2000;
                Field<NumericUpDown>(view, "matchBonus").Value = 300;
                Field<NumericUpDown>(view, "contractEnd").Value = 2005;
                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal("Gespeichert", Status(view));
                Assert.Equal(new byte[] { 1, 2, 0xBC, 0x07 }, m.WrittenAt(r + RecordOffset.BirthDay));
                Assert.Equal(new byte[] { 10 }, m.WrittenAt(r + RecordOffset.Position));
                Assert.Null(m.WrittenAt(r + RecordOffset.SidePosition1));   // still LV
                Assert.Equal(new byte[] { 0 }, m.WrittenAt(r + RecordOffset.SidePosition2));
                Assert.Equal(new byte[] { 2 }, m.WrittenAt(r + RecordOffset.Nationality));
                Assert.Equal(new byte[] { 12 }, m.WrittenAt(r + RecordOffset.Strength));
                Assert.Equal(BitConverter.GetBytes((ushort)8000), m.WrittenAt(r + RecordOffset.Freshness));
                Assert.Null(m.WrittenAt(r + RecordOffset.Condition));   // untouched: keeps its hundredths
                Assert.Equal(new byte[] { 0x80 | 2 << 2 }, m.WrittenAt(r + RecordOffset.FootAndReputation));
                Assert.Equal(BitConverter.GetBytes((ushort)(1 | 6)), m.WrittenAt(r + RecordOffset.Skills));
                Assert.Equal(BitConverter.GetBytes((ushort)4), m.WrittenAt(r + RecordOffset.Skills2));
                Assert.Equal(BitConverter.GetBytes((ushort)(1 | 8)), m.WrittenAt(r + RecordOffset.Traits));
                Assert.Equal(BitConverter.GetBytes((ushort)2005), m.WrittenAt(r + RecordOffset.ContractEnd));
            }
        });

        [Fact]
        public void Save_OfAnUnchangedKeeper_WritesNothing() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            using (PlayerView view = View(m))
            {
                Select(view, Keeper);
                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal("Gespeichert", Status(view));
                Assert.Empty(m.Writes);
            }
        });

        [Fact]
        public void Save_RejectsADateThatDoesntExist() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            using (PlayerView view = View(m))
            {
                Select(view, Keeper);
                Field<NumericUpDown>(view, "birthDay").Value = 31;
                Field<NumericUpDown>(view, "birthMonth").Value = 2;
                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal("Ungültiges Geburtsdatum", Status(view));
                Assert.Empty(m.Writes);
            }
        });

        [Fact]
        public void Heal_ThenSave_ClearsTheInjury() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            using (PlayerView view = View(m))
            {
                Select(view, Defender);
                TestHelpers.Click(Field<Button>(view, "heal"));
                Assert.Equal("gesund (nach Speichern)", Field<Label>(view, "injuryValue").Text);

                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal(new byte[] { 0, 0 }, m.WrittenAt(Record(Defender) + RecordOffset.InjuryTo));
            }
        });

        [Fact]
        public void HealAndSave_WithoutAPlayer_DoNothing() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            using (PlayerView view = View(m))
            {
                view.ShowClub(null);
                TestHelpers.Invoke(view, "Heal");
                TestHelpers.Invoke(view, "Save");

                Assert.Empty(m.Writes);
                Assert.Equal(Tables.Goalkeeper, TestHelpers.Invoke(view, "SelectedPosition"));
            }
        });

        [Fact]
        public void Save_WhenWritingFails_SaysSo() => StaThread.Run(() =>
        {
            using (PlayerView view = View(new ReadOnlyGameMemory(Squad())))
            {
                Select(view, Striker);
                Field<NumericUpDown>(view, "strength").Value = 5;
                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal("Fehler beim Speichern", Status(view));
            }
        });

        [Fact]
        public void Save_WhenTheGameIsGone_ReportsIt() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            ProcessMemory memory = Attached();
            using (PlayerView view = View(m, memory))
            {
                Select(view, Keeper);
                memory.mProc.Process = null;

                TestHelpers.ExpectDialog("Error", () => TestHelpers.Click(Field<Button>(view, "save")));
                Assert.Empty(m.Writes);
            }
        });

        [Fact]
        public void SidePositions_AtMostTwo() => StaThread.Run(() =>
        {
            using (PlayerView view = View(Squad()))
            {
                Select(view, Defender);
                CheckBox third = Field<CheckBox[]>(view, "sideChecks").First(c => !c.Checked);

                third.Checked = true;

                Assert.False(third.Checked);
                Assert.Equal(2, Field<CheckBox[]>(view, "sideChecks").Count(c => c.Checked));
            }
        });

        [Fact]
        public void Reload_KeepsTheCountryLists() => StaThread.Run(() =>
        {
            using (PlayerView view = View(Squad()))
            {
                TestHelpers.Click(Field<Button>(view, "reload"));

                Assert.Equal(3, Field<ComboBox>(view, "nationality").Items.Count);
                Assert.Equal("4 Spieler", Status(view));
            }
        });

        [Fact]
        public void SelectedPosition_OfAPlayerWithoutAnEditorPosition_IsHisOwn() => StaThread.Run(() =>
        {
            FakeGameMemory m = A3GameTests.Career();
            A3GameTests.PutPlayer(m, 1039, 14, 1, 0, 5);
            using (PlayerView view = View(m))
            {
                Select(view, 1039);

                Assert.Equal(0, TestHelpers.Invoke(view, "SelectedPosition"));
            }
        });
    }
}
