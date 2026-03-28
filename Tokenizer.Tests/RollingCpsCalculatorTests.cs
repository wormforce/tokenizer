using Tokenizer.Core.Statistics;

namespace Tokenizer.Tests;

public sealed class RollingCpsCalculatorTests
{
    [Fact]
    public void Register_ComputesFloorCharsPerSecondOverTwoSecondWindow()
    {
        var calculator = new RollingCpsCalculator();
        var now = DateTimeOffset.UtcNow;

        calculator.Register(now);
        calculator.Register(now.AddMilliseconds(200));
        calculator.Register(now.AddMilliseconds(600));
        var cps = calculator.Register(now.AddMilliseconds(1200));

        Assert.Equal(2, cps);
        Assert.Equal(2, calculator.CurrentCps);
    }

    [Fact]
    public void Register_TrimsSamplesOutsideTwoSecondWindow()
    {
        var calculator = new RollingCpsCalculator();
        var now = DateTimeOffset.UtcNow;

        calculator.Register(now);
        calculator.Register(now.AddSeconds(1));
        var cps = calculator.Register(now.AddSeconds(3));

        Assert.Equal(1, cps);
    }

    [Fact]
    public void Refresh_DropsSpeedToZeroAfterSamplesExpire()
    {
        var calculator = new RollingCpsCalculator();
        var now = DateTimeOffset.UtcNow;

        calculator.Register(now);
        calculator.Register(now.AddMilliseconds(400));

        Assert.Equal(1, calculator.CurrentCps);
        Assert.Equal(0, calculator.Refresh(now.AddSeconds(3)));
        Assert.Equal(0, calculator.CurrentCps);
    }
}

