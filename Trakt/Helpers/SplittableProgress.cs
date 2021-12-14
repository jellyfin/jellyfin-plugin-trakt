using System;

namespace Trakt.Helpers;

/// <summary>
/// Similar to <see cref="Progress"/>, but report is relative, not absolute.
/// </summary>
/// Can't be generic, because it's impossible to do arithmetics on generics
public class SplittableProgress : Progress<double>, ISplittableProgress<double>
{
    public SplittableProgress(Action<double> handler)
        : base(handler)
    {
    }

    private double Progress { get; set; }

    public ISplittableProgress<double> Split(int parts)
    {
        var child = new SplittableProgress(
            d =>
            {
                Progress += d / parts;
                OnReport(Progress);
            });
        return child;
    }
}
