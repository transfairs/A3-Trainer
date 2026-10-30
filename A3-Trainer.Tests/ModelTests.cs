using A3_Trainer.Model;
using Xunit;

namespace A3_Trainer.Tests
{
    public class ModelTests
    {
        [Theory]
        [InlineData(0x0u, false, false)]
        [InlineData(0x10000u, true, false)]
        [InlineData(0x400u, false, true)]
        public void Player_Flags_TellDarlingFromHated(uint flags, bool darling, bool hated)
        {
            var player = new Player { Flags = flags };
            Assert.Equal(darling, player.Darling);
            Assert.Equal(hated, player.Hated);
        }

        [Theory]
        [InlineData("Oliver", "Huhn", "Huhn, Oliver")]
        [InlineData("", "Huhn", "Huhn")]
        [InlineData("Oliver", "", "Oliver")]
        public void Player_ListName_IsLastNameCommaFirstName(string first, string last, string expected)
        {
            Assert.Equal(expected, new Player { FirstName = first, LastName = last }.ListName);
        }

        [Theory]
        [InlineData(184, 7, 4)]  // the test career's start date
        [InlineData(0, 1, 1)]
        [InlineData(365, 12, 31)] // 2000 is a leap year
        public void GameDate_FromDayOfYear_IsZeroBased(int dayOfYear, int month, int day)
        {
            GameDate date = GameDate.FromDayOfYear(dayOfYear == 365 ? 2000 : 1999, dayOfYear);

            Assert.Equal(month, date.Month);
            Assert.Equal(day, date.Day);
        }

        [Theory]
        [InlineData(1969, 6, 15, 30)] // Huhn on 4.7.1999
        [InlineData(1961, 3, 21, 38)] // Butthäus
        [InlineData(1972, 9, 8, 26)]  // Gubbel, birthday still ahead
        public void GameDate_AgeOn_CountsCompletedYears(int year, int month, int day, int age)
        {
            Assert.Equal(age, new GameDate(year, month, day).AgeOn(new GameDate(1999, 7, 4)));
        }

        [Fact]
        public void GameDate_AgeOn_TheDayBeforeTheBirthday()
        {
            Assert.Equal(29, new GameDate(1969, 7, 5).AgeOn(new GameDate(1999, 7, 4)));
            Assert.Equal(30, new GameDate(1969, 7, 4).AgeOn(new GameDate(1999, 7, 4)));
        }

        [Fact]
        public void GameDate_EqualityAndHash()
        {
            var date = new GameDate(1969, 6, 15);
            Assert.True(date.Equals(new GameDate(1969, 6, 15)));
            Assert.False(date.Equals(new GameDate(1969, 6, 16)));
            Assert.False(date.Equals("15.6.1969"));
            Assert.Equal(new GameDate(1969, 6, 15).GetHashCode(), date.GetHashCode());
            Assert.NotEqual(new GameDate(1969, 6, 16).GetHashCode(), date.GetHashCode());
        }

        [Fact]
        public void ClubKey_EqualityHashAndText()
        {
            var key = new ClubKey(14, 1);
            Assert.True(key.Equals(new ClubKey(14, 1)));
            Assert.False(key.Equals(new ClubKey(14, 2)));
            Assert.False(key.Equals(new ClubKey(3, 1)));
            Assert.False(key.Equals("14/1"));
            Assert.Equal(new ClubKey(14, 1).GetHashCode(), key.GetHashCode());
            Assert.Equal("14/1", key.ToString());
        }

        [Fact]
        public void ClubInfo_Clone_IsIndependent()
        {
            var info = new ClubInfo { Name = "1.FV München", SeasonStartMoney = 5 };
            ClubInfo copy = info.Clone();
            copy.SeasonStartMoney = 6;

            Assert.Equal("1.FV München", copy.Name);
            Assert.Equal(5, info.SeasonStartMoney);
        }

        [Fact]
        public void GameDate_EmptySlotsAreInvalid()
        {
            Assert.False(new GameDate(0, 0, 0).IsValid);
            Assert.True(new GameDate(1969, 6, 15).IsValid);
        }

        [Fact]
        public void Tables_GoalkeepersHaveTheirOwnSkills()
        {
            Assert.Equal(9, Tables.SkillsFor(Tables.Goalkeeper).Length);
            Assert.Equal("starke Reflexe", Tables.SkillsFor(Tables.Goalkeeper)[1]);
            Assert.Equal(16, Tables.SkillsFor(10).Length);
            Assert.Equal("Kämpfernatur", Tables.Traits[0]);
            Assert.Equal("Hitzkopf", Tables.Character[1]);
        }

        [Fact]
        public void Tables_PositionName_HandlesUnknownValues()
        {
            Assert.Equal("TO", Tables.PositionName(1));
            Assert.Equal("ST", Tables.PositionName(10));
            Assert.Equal("?", Tables.PositionName(11));
        }
    }
}
