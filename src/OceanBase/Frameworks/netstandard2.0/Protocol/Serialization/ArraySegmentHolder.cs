using System;

namespace OceanBase.Protocol.Serialization;

/// <summary>
/// <see cref="T:OceanBase.Protocol.Serialization.ArraySegmentHolder`1" /> is a class that holds an instance of <see cref="T:System.ArraySegment`1" />.
/// Its primary difference from <see cref="T:System.ArraySegment`1" /> is that it's a reference type, so mutations
/// to this object are visible to other objects that hold a reference to it.
/// </summary>
internal sealed class ArraySegmentHolder<T>
{
	public ArraySegment<T> ArraySegment { get; set; }

	public T[]? Array => ArraySegment.Array;

	public int Offset => ArraySegment.Offset;

	public int Count => ArraySegment.Count;

	public void Clear()
	{
		if (ArraySegment.Count > 0)
		{
			ArraySegment = new ArraySegment<T>(ArraySegment.Array, 0, 0);
		}
	}
}
