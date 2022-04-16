using System;

namespace Trakt.Helpers
{
    /// <summary>
    /// Similar to <see cref="Progress"/>, but report is relative, not absolute.
    /// </summary>
    /// Can't be generic, because it's impossible to do arithmetics on generics.
    public class SplittableProgress : Progress<double>, ISplittableProgress<double>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SplittableProgress"/> class.
        /// </summary>
        /// <param name="handler">Instance of the <see cref="Action"/> interface.</param>
        public SplittableProgress(Action<double> handler)
            : base(handler)
        {
        }

        private double Progress { get; set; }

        /// <inheritdoc />
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
}
