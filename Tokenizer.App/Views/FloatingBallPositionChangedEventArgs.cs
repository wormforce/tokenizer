using Tokenizer.Core.Models;

namespace Tokenizer.App.Views;

public sealed class FloatingBallPositionChangedEventArgs(FloatingEdge edge, double offsetX, double offsetY) : EventArgs
{
    public FloatingEdge Edge { get; } = edge;

    public double OffsetX { get; } = offsetX;

    public double OffsetY { get; } = offsetY;
}

