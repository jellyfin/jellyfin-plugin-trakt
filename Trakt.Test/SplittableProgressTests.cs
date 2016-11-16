namespace Trakt.Test
{
    using System;

    using Trakt.Helpers;

    using Xunit;

    public class SplittableProgressTests
    {
        [Fact]
        public void AsIProgress()
        {
            IProgress<double> mainProg = new SplittableProgress(d => Assert.Equal(100, d));
            mainProg.Report(100);
        }

        [Fact]
        public void ExtensionConversion()
        {
            IProgress<double> mainProg = new Progress<double>(d => Assert.Equal(100, d));
            mainProg.ToSplittableProgress().Report(100);
        }

        [Fact]
        public void ExtensionSplit()
        {
            IProgress<double> mainProg = new Progress<double>(d => Assert.Equal(25, d));
            mainProg.Split(4).Report(100);
        }

        [Fact]
        public void Split()
        {
            var firstSplit = 3;
            var expected = 100d / firstSplit;

            IProgress<double> mainProg = new Progress<double>(d => Assert.Equal(expected, d));
            ISplittableProgress<double> mainProgS = new SplittableProgress(mainProg.Report);
            var childProgS = mainProgS.Split(firstSplit);

            childProgS.Report(100);
        }

        [Fact]
        public void SplitTwice()
        {
            var firstSplit = 3;
            var secondSplit = 5;
            var expected = 100d / firstSplit / secondSplit;

            IProgress<double> mainProg = new Progress<double>(d => Assert.Equal(expected, d));
            ISplittableProgress<double> mainProgS = new SplittableProgress(mainProg.Report);
            var grandchildProgS = mainProgS.Split(firstSplit).Split(secondSplit);

            grandchildProgS.Report(100);
        }
    }
}