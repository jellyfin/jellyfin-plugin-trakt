namespace Trakt.Helpers
{
    using System;

    public interface ISplittableProgress<T> : IProgress<T>
    {
        ISplittableProgress<T> Split(int parts);
    }
}