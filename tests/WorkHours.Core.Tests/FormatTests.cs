using WorkHours.Core.Services;

namespace WorkHours.Core.Tests;

public class FormatTests
{
    [Theory]
    [InlineData(4, 0, "4h")]
    [InlineData(4, 30, "4h 30")]
    [InlineData(0, 5, "0h 05")]
    [InlineData(0, 0, "0h")]
    [InlineData(-1, -30, "−1h 30")]
    public void Ore(int h, int m, string expected) =>
        Assert.Equal(expected, Format.Hours(new TimeSpan(h, m, 0)));

    [Theory]
    [InlineData(2, 0, "+2h")]
    [InlineData(-2, 0, "−2h")]
    [InlineData(0, 0, "0h")]
    public void Saldo(int h, int m, string expected) =>
        Assert.Equal(expected, Format.Balance(new TimeSpan(h, m, 0)));

    [Fact]
    public void DataLunga() => Assert.Equal("martedì 20 ottobre", Format.LongDate(new DateOnly(2026, 10, 20)));
}
