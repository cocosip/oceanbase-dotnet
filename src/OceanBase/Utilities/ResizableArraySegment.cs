using System;

namespace OceanBase.Utilities;

/// <summary>
/// An <see cref="T:System.ArraySegment`1" /> that supports having its underlying array reallocated and resized.
/// </summary>
internal readonly struct ResizableArraySegment<T>(ResizableArray<T> array, int offset, int count) where T : notnull
{
	public ResizableArray<T> Array { get; } = array;

	public int Offset { get; } = offset;

	public int Count { get; } = count;

	public static implicit operator ReadOnlySpan<T>(ResizableArraySegment<T> segment)
	{
		return new ReadOnlySpan<T>(segment.Array.Array, segment.Offset, segment.Count);
	}
}
