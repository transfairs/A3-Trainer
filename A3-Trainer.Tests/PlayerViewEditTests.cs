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
        public void ShowAndSave_ContractHealthAndForm() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            uint r = Record(Defender);
            m.PutUInt16(r + RecordOffset.GoalBonus, 25);
            m.PutUInt16(r + RecordOffset.ReleaseClause, 300);
            m.PutUInt16(r + RecordOffset.ContractFlags, 0x2042);   // mind. 2. Liga, Option Verein, unknown bit 0x40
            m.PutUInt16(r + RecordOffset.Health, 0x1004);          // anfällig plus a state bit
            m.Put(r + RecordOffset.FormBase, 12);
            using (PlayerView view = View(m))
            {
                Select(view, Defender);

                Assert.Equal(25, Field<NumericUpDown>(view, "goalBonus").Value);
                Assert.Equal(300, Field<NumericUpDown>(view, "releaseClause").Value);
                Assert.Equal(2, Field<ComboBox>(view, "validity").SelectedIndex);
                Assert.True(Field<CheckBox>(view, "clubOption").Checked);
                Assert.False(Field<CheckBox>(view, "playerOption").Checked);
                Assert.False(Field<CheckBox>(view, "regularPlace").Checked);
                Assert.True(Field<RadioButton[]>(view, "healthRadios")[2].Checked);
                Assert.Equal(12, Field<NumericUpDown>(view, "formBase").Value);

                Field<NumericUpDown>(view, "goalBonus").Value = 40;
                Field<NumericUpDown>(view, "releaseClause").Value = 0;
                Field<ComboBox>(view, "validity").SelectedIndex = 0;
                Field<CheckBox>(view, "clubOption").Checked = false;
                Field<CheckBox>(view, "playerOption").Checked = true;
                Field<CheckBox>(view, "regularPlace").Checked = true;
                Field<RadioButton[]>(view, "healthRadios")[1].Checked = true;   // robust
                Field<NumericUpDown>(view, "formBase").Value = 20;
                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal("Gespeichert", Status(view));
                Assert.Equal(BitConverter.GetBytes((ushort)40), m.WrittenAt(r + RecordOffset.GoalBonus));
                Assert.Equal(new byte[] { 0, 0 }, m.WrittenAt(r + RecordOffset.ReleaseClause));
                Assert.Equal(BitConverter.GetBytes((ushort)0x5040), m.WrittenAt(r + RecordOffset.ContractFlags));
                Assert.Equal(BitConverter.GetBytes((ushort)0x1002), m.WrittenAt(r + RecordOffset.Health));
                Assert.Equal(new byte[] { 20 }, m.WrittenAt(r + RecordOffset.FormBase));
            }
        });

        [Fact]
        public void ShowAndSave_LooksAndNationalTeamStatus() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            uint r = Record(Striker);
            uint copy = 0x06000000 + 8;
            m.PutUInt16(r + RecordOffset.ArtistName, 500);
            A3GameTests.PutName(m, 500, "Zé", 0x02000100);
            m.Put(r + RecordOffset.Appearance, 0x21);      // Bart, skin dunkel
            m.Put(r + RecordOffset.Hair, 0x12);            // kurz, braun
            m.Put(copy + CompactOffset.NationalTeam, 0x08); // a game state bit
            using (PlayerView view = View(m))
            {
                Select(view, Striker);

                Assert.Equal("Zé", Field<TextBox>(view, "artistNameValue").Text);
                Assert.True(Field<RadioButton[]>(view, "skinRadios")[1].Checked);
                Assert.True(Field<RadioButton[]>(view, "hairColorRadios")[2].Checked);
                Assert.True(Field<RadioButton[]>(view, "hairTypeRadios")[1].Checked);
                Assert.Equal(new[] { false, true, false, false }, Field<CheckBox[]>(view, "beardChecks").Select(c => c.Checked));
                Assert.True(Field<CheckBox>(view, "nationalPlayer").Checked);
                Assert.False(Field<CheckBox>(view, "homegrownForeigner").Checked);
                Assert.False(Field<CheckBox>(view, "nationalTeamRetired").Checked);
                Assert.False(Field<CheckBox>(view, "captainRetired").Checked);

                Field<RadioButton[]>(view, "hairColorRadios")[5].Checked = true;   // Glatze
                Field<RadioButton[]>(view, "hairTypeRadios")[3].Checked = true;    // lang
                Field<CheckBox[]>(view, "beardChecks")[1].Checked = false;
                Field<CheckBox[]>(view, "beardChecks")[3].Checked = true;          // Ziegenbart
                Field<CheckBox>(view, "homegrownForeigner").Checked = true;
                Field<CheckBox>(view, "captainRetired").Checked = true;
                Field<CheckBox>(view, "nationalTeamRetired").Checked = true;
                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal("Gespeichert", Status(view));
                Assert.Equal(new byte[] { 0x81 }, m.WrittenAt(r + RecordOffset.Appearance));
                Assert.Equal(new byte[] { 0x35 }, m.WrittenAt(r + RecordOffset.Hair));
                Assert.Equal(new byte[] { 0x87 | 0x10 | 0x40 }, m.WrittenAt(r + RecordOffset.FootAndReputation));
                Assert.Equal(new byte[] { 0x0A }, m.WrittenAt(r + RecordOffset.NationalTeam));
                Assert.Equal(new byte[] { 0x0A }, m.WrittenAt(copy + CompactOffset.NationalTeam));
            }
        });

        [Fact]
        public void ShowAndSave_Audience_SetsReputationAndCurrentFlag() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            uint copy = 0x06000000 + 8;
            m.Put(Record(Striker) + RecordOffset.FootAndReputation, 0x86);   // national player, right foot, Liebling
            m.PutUInt32(copy + CompactOffset.Flags, CrowdFlag.Darling | 1);
            m.Put(Record(Midfielder) + RecordOffset.FootAndReputation, 0x07); // no current flag, verhaßt
            using (PlayerView view = View(m))
            {
                RadioButton[] audience = Field<RadioButton[]>(view, "audienceRadios");
                Select(view, Keeper);
                Assert.True(audience[1].Enabled);
                Assert.True(audience[1].Checked);                              // darling flag
                Select(view, Defender);
                Assert.True(audience[2].Checked);                              // hated flag

                Select(view, Midfielder);
                Assert.True(audience[2].Checked);                              // lasting reputation
                audience[0].Checked = true;
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal(new byte[] { 0x05 }, m.WrittenAt(Record(Midfielder) + RecordOffset.FootAndReputation));
                Assert.Null(m.WrittenAt(Record(Midfielder) + RecordOffset.Flags));

                Select(view, Striker);
                Assert.True(audience[1].Checked);
                audience[2].Checked = true;
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal(new byte[] { 0x87 }, m.WrittenAt(Record(Striker) + RecordOffset.FootAndReputation));
                Assert.Equal(BitConverter.GetBytes(CrowdFlag.Hated | 1), m.WrittenAt(Record(Striker) + RecordOffset.Flags));
                Assert.Equal(BitConverter.GetBytes(CrowdFlag.Hated | 1), m.WrittenAt(copy + CompactOffset.Flags));

                // An untouched audience writes nothing.
                Select(view, Keeper);
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Null(m.WrittenAt(Record(Keeper) + RecordOffset.Flags));
            }
        });

        [Fact]
        public void ShowAndSave_PrivateLife() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            uint r = Record(Striker);
            m.Map(A3Addresses.FirstNameLists, 3 * A3Addresses.FirstNameListSize * 2);
            uint list = A3Addresses.FirstNameLists + 2 * A3Addresses.FirstNameListSize * 2; // name country 2
            string[] names = { "Brigitte", "Tatjana", "Jürgen", "Sabine" };
            for (int i = 0; i < names.Length; i++)
            {
                A3GameTests.PutName(m, 600 + i, names[i], 0x02000200 + (uint)(i * 0x20));
                m.PutUInt16(list + (uint)((7 + i) * 2), (ushort)(600 + i));
            }
            m.Put(r + RecordOffset.NameCountry, 2);
            m.Put(r + RecordOffset.PartnerName, 7);
            m.Put(r + RecordOffset.ChildNames, 8, 9, 0, 0, 0);
            m.Put(r + RecordOffset.ChildBirthDates, 3, 5, 0xCB, 0x07, 20, 11, 0xCD, 0x07);   // 3.5.1995, 20.11.1997
            m.Put(r + RecordOffset.ChildBirthDates + 8, new byte[12]);
            m.PutUInt16(r + RecordOffset.ChildCount, 2);
            m.Put(r + RecordOffset.Relationship, 0x83);   // verheiratet, zusammen, a game state bit
            m.Put(r + RecordOffset.Hobby, 19);
            using (PlayerView view = View(m))
            {
                Select(view, Striker);
                ComboBox[] children = Field<ComboBox[]>(view, "childNames");
                NumericUpDown[] days = Field<NumericUpDown[]>(view, "childDays");
                NumericUpDown[] years = Field<NumericUpDown[]>(view, "childYears");

                Assert.Equal("verheiratet", Field<ComboBox>(view, "relationship").Text);
                Assert.Equal("zusammen", Field<ComboBox>(view, "relationshipState").Text);
                Assert.Equal("Brigitte", Field<ComboBox>(view, "partner").Text);
                Assert.Equal("Gut essen gehen", Field<ComboBox>(view, "hobby").Text);
                Assert.Equal(2, Field<NumericUpDown>(view, "childCount").Value);
                Assert.Equal(new[] { "Tatjana", "Jürgen" }, children.Take(2).Select(c => c.Text));
                Assert.Equal(new decimal[] { 3, 20 }, days.Take(2).Select(d => d.Value));
                Assert.Equal(1997, years[1].Value);
                Assert.Equal(new[] { true, true, false, false, false }, children.Select(c => c.Enabled));

                Field<ComboBox>(view, "relationship").SelectedIndex = 1;      // Freundin
                Field<ComboBox>(view, "relationshipState").SelectedIndex = 1; // getrennt
                Field<ComboBox>(view, "hobby").SelectedIndex = 2;             // Angeln
                Field<ComboBox>(view, "partner").SelectedIndex = 10;          // Sabine
                Field<NumericUpDown>(view, "childCount").Value = 3;           // a new child: free name, today
                Assert.True(children[2].Enabled);
                Assert.Equal(0, children[2].SelectedIndex);
                Assert.Equal(1999, years[2].Value);
                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal("Gespeichert", Status(view));
                Assert.Equal(new byte[] { 0x85 }, m.WrittenAt(r + RecordOffset.Relationship));
                Assert.Equal(new byte[] { 2 }, m.WrittenAt(r + RecordOffset.Hobby));
                Assert.Equal(new byte[] { 10 }, m.WrittenAt(r + RecordOffset.PartnerName));
                Assert.Equal(new byte[] { 3, 0 }, m.WrittenAt(r + RecordOffset.ChildCount));
                Assert.Equal(new byte[] { 8, 9, 0, 0, 0 }, m.WrittenAt(r + RecordOffset.ChildNames));
                Assert.Equal(new byte[] { 3, 5, 0xCB, 0x07, 20, 11, 0xCD, 0x07, 4, 7, 0xCF, 0x07 },
                    m.WrittenAt(r + RecordOffset.ChildBirthDates).Take(12));
            }
        });

        [Fact]
        public void PrivateLife_ChecksChangedChildren_AndKeepsAnUnnamedState() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            uint r = Record(Defender);
            m.Put(r + RecordOffset.Relationship, 0x1D);   // Freundin, state 7
            m.Put(r + RecordOffset.PartnerName, 150);     // beyond the 100 names of a list
            m.Put(r + RecordOffset.ChildNames, 1, 2, 0, 0, 0);
            m.Put(r + RecordOffset.ChildBirthDates, new byte[20]);   // no dates: left alone while unchanged
            m.PutUInt16(r + RecordOffset.ChildCount, 2);
            using (PlayerView view = View(m))
            {
                Select(view, Defender);
                Assert.Equal(-1, Field<ComboBox>(view, "partner").SelectedIndex);
                Assert.Equal(-1, Field<ComboBox>(view, "relationshipState").SelectedIndex);

                Field<ComboBox>(view, "relationship").SelectedIndex = 2;   // verlobt
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal(new byte[] { 0x1E }, m.WrittenAt(r + RecordOffset.Relationship));
                Assert.Null(m.WrittenAt(r + RecordOffset.PartnerName));
                Assert.Null(m.WrittenAt(r + RecordOffset.ChildCount));

                Select(view, Defender);
                ComboBox[] children = Field<ComboBox[]>(view, "childNames");
                Field<NumericUpDown[]>(view, "childDays")[1].Value = 3;   // 3.0.0
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal("Ungültiges Geburtsdatum eines Kindes", Status(view));

                foreach (int i in new[] { 0, 1 })
                {
                    Field<NumericUpDown[]>(view, "childDays")[i].Value = 1;
                    Field<NumericUpDown[]>(view, "childMonths")[i].Value = 1;
                    Field<NumericUpDown[]>(view, "childYears")[i].Value = 1998;
                }
                children[1].SelectedIndex = 1;
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal("Kinder brauchen verschiedene Namen", Status(view));
                Assert.Null(m.WrittenAt(r + RecordOffset.ChildCount));

                Field<NumericUpDown>(view, "childCount").Value = 1;
                Assert.False(children[1].Enabled);
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal(new byte[] { 1, 0 }, m.WrittenAt(r + RecordOffset.ChildCount));
            }
        });

        [Fact]
        public void Save_KeepsAHairTypeWithoutAButton() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            m.Put(Record(Defender) + RecordOffset.Hair, 0x52);
            using (PlayerView view = View(m))
            {
                Select(view, Defender);
                Assert.DoesNotContain(Field<RadioButton[]>(view, "hairTypeRadios"), h => h.Checked);

                Field<RadioButton[]>(view, "hairColorRadios")[4].Checked = true;
                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Equal(new byte[] { 0x54 }, m.WrittenAt(Record(Defender) + RecordOffset.Hair));
            }
        });

        [Fact]
        public void ShowAndSave_TheHairTypeOnlyTheGameSets() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            m.Put(Record(Defender) + RecordOffset.Hair, 0x42);
            using (PlayerView view = View(m))
            {
                Select(view, Defender);
                RadioButton[] types = Field<RadioButton[]>(view, "hairTypeRadios");
                Assert.Equal("Sonderfrisur", types[4].Text);
                Assert.True(types[4].Checked);

                types[0].Checked = true;
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal(new byte[] { 0x02 }, m.WrittenAt(Record(Defender) + RecordOffset.Hair));
            }

            m = Squad();
            m.Put(Record(Defender) + RecordOffset.Hair, 0x12);
            using (PlayerView view = View(m))
            {
                Select(view, Defender);
                RadioButton special = Field<RadioButton[]>(view, "hairTypeRadios")[4];
                special.Checked = true;
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal(new byte[] { 0x42 }, m.WrittenAt(Record(Defender) + RecordOffset.Hair));

                // No portrait row after "asiatisch": the special hair type is switched off.
                RadioButton[] skins = Field<RadioButton[]>(view, "skinRadios");
                skins[3].Checked = true;
                Assert.False(special.Enabled);
                Assert.False(special.Checked);
                skins[0].Checked = true;
                Assert.True(special.Enabled);
            }
        });

        [Fact]
        public void Save_KeepsAFormOutsideTheRangeAndAHealthWordWithoutType() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            m.Put(Record(Keeper) + RecordOffset.FormBase, 30);
            using (PlayerView view = View(m))
            {
                Select(view, Keeper);
                Assert.Equal(20, Field<NumericUpDown>(view, "formBase").Value);
                Assert.DoesNotContain(Field<RadioButton[]>(view, "healthRadios"), h => h.Checked);

                TestHelpers.Click(Field<Button>(view, "save"));

                Assert.Empty(m.Writes);
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

        [Fact]
        public void SeasonStats_ShowTheValueBits_AndSaveKeepsTheGameState() => StaThread.Run(() =>
        {
            FakeGameMemory m = Squad();
            int goals = Array.FindIndex(SeasonStat.All, s => s.Caption == "Tore");
            int ban = Array.FindIndex(SeasonStat.All, s => s.Caption == "Sperre EP (Spiele)");
            int red = Array.FindIndex(SeasonStat.All, s => s.Caption == "Rot Pokal");           // record only
            uint compact = 0x06000000 + 8;
            m.Put(Record(Keeper) + (uint)SeasonStat.All[goals].Record, 7);
            m.Put(Record(Keeper) + (uint)SeasonStat.All[ban].Record, 0x83);      // 3 games, a state bit
            m.Put(compact + (uint)SeasonStat.All[goals].Compact, 4);             // the striker's working copy
            m.Put(Record(Striker) + (uint)SeasonStat.All[red].Record, 2);
            using (PlayerView view = View(m))
            {
                NumericUpDown[] stats = Field<NumericUpDown[]>(view, "seasonStats");
                Select(view, Keeper);
                Assert.Equal(7, stats[goals].Value);
                Assert.Equal(3, stats[ban].Value);
                Assert.Equal(31, stats[ban].Maximum);
                Assert.Equal("0", stats[red].Text); // a zero shows as 0, not empty

                stats[goals].Value = 9;
                stats[ban].Value = 0;
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal("Gespeichert", Status(view));
                Assert.Equal(9, m.Get(Record(Keeper) + (uint)SeasonStat.All[goals].Record));
                Assert.Equal(0x80, m.Get(Record(Keeper) + (uint)SeasonStat.All[ban].Record));

                Select(view, Striker);
                Assert.Equal(4, stats[goals].Value);
                Assert.Equal(2, stats[red].Value);
                stats[red].Value = 0;
                stats[goals].Value = 5;
                TestHelpers.Click(Field<Button>(view, "save"));
                Assert.Equal(5, m.Get(compact + (uint)SeasonStat.All[goals].Compact));
                Assert.Equal(5, m.Get(Record(Striker) + (uint)SeasonStat.All[goals].Record));
                Assert.Equal(0, m.Get(Record(Striker) + (uint)SeasonStat.All[red].Record));
            }
        });
    }
}
