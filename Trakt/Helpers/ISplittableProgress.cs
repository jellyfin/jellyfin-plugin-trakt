namespace Trakt.Helpers;

using System;

/// <summary>
/// Similar to <see cref="IProgress{T}"/>, but it contains a split method and Report is relative, not absolute.
/// </summary>
/// <typeparam name="T">The type of progress update value</typeparam>
public interface ISplittableProgress<T> : IProgress<T>
{
    ISplittableProgress<T> Split(int parts);
}
