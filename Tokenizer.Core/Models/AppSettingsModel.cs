namespace Tokenizer.Core.Models;

public sealed class AppSettingsModel
{
    public const int DefaultFloatingBallOpacityPercent = 84;
    public const int DefaultFloatingBallSize = 98;

    public bool AutostartEnabled { get; set; }

    public bool FloatingBallEnabled { get; set; } = true;

    public FloatingEdge FloatingEdge { get; set; } = FloatingEdge.Right;

    public bool LaunchMinimized { get; set; }

    public bool Paused { get; set; }

    public double FloatingOffsetX { get; set; } = 180;

    public double FloatingOffsetY { get; set; } = 180;

    public int FloatingBallOpacityPercent { get; set; } = DefaultFloatingBallOpacityPercent;

    public int FloatingBallSize { get; set; } = DefaultFloatingBallSize;
}

