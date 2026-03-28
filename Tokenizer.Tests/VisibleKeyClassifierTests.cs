using Tokenizer.Core.Statistics;

namespace Tokenizer.Tests;

public sealed class VisibleKeyClassifierTests
{
    [Theory]
    [InlineData(0x41)]
    [InlineData(0x39)]
    [InlineData(0x20)]
    [InlineData(0x6B)]
    [InlineData(0xBA)]
    public void ReturnsTrueForCountableKeys(int virtualKey)
    {
        Assert.True(VisibleKeyClassifier.IsCountable(virtualKey));
    }

    [Theory]
    [InlineData(0x08)]
    [InlineData(0x09)]
    [InlineData(0x0D)]
    [InlineData(0x11)]
    [InlineData(0x25)]
    [InlineData(0x70)]
    public void ReturnsFalseForControlAndNavigationKeys(int virtualKey)
    {
        Assert.False(VisibleKeyClassifier.IsCountable(virtualKey));
    }
}

