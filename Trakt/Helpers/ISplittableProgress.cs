using System;

namespace Trakt.Helpers
{
    /// <summary>
    /// Similar to <see cref="IProgress{T}"/>, but it contains a split method and Report is relative, not absolute.
    /// </summary>
    /// <typeparam name="T">The type of progress update value.</typeparam>
    public interface ISplittableProgress<T> : IProgress<T>
    {
        /// <summary>
        /// Splits the progress into parts.
        /// </summary>
        /// <param name="parts">The amount of parts to split into.</param>
        /// <returns>ISplittableProgress{T}.</returns>
        ISplittableProgress<T> Split(int parts);
    }
}
