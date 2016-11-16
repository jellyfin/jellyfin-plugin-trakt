namespace Trakt.Helpers
{
    using System;

    // Can't be generic, because it's impossible to do arithmetics on generics
    public class SplittableProgress : Progress<double>, ISplittableProgress<double>
    {
        public SplittableProgress(Action<double> handler)
            : base(handler)
        {
        }

        public double Progress { get; private set; }

        ISplittableProgress<double> ISplittableProgress<double>.Split(int parts)
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
}