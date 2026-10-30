using A3_Trainer.Model;
using Xunit;

namespace A3_Trainer.Tests
{
    public class ModelTests
    {
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
        public void GameDate_EmptySlotsAreInvalid()
        {
            Assert.False(new GameDate(0, 0, 0).IsValid);
            Assert.True(new GameDate(1969, 6, 15).IsValid);
        }

        [Fact]
        public void Tables_GoalkeepersHaveTheirOwnSkills()
        {
            Assert.Equal(9, Tables.SkillsFor(Tables.Goalkeeper).Length);
            Assert.Equal("Reflexe", Tables.SkillsFor(Tables.Goalkeeper)[1]);
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
