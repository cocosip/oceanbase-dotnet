using System;

namespace OceanBase.Utilities;

/// <summary>
/// A wrapper around a resizable array. This type is intended to be used with <see cref="T:OceanBase.Utilities.ResizableArraySegment`1" />.
/// </summary>
internal sealed class ResizableArray<T> where T : notnull
{
	private T[]? m_array;

	public T[]? Array => m_array;

	public int Count
	{
		get
		{
			T[]? array = m_array;
			if (array == null)
			{
				return 0;
			}
			return array.Length;
		}
	}

	public Span<T> AsSpan(int start)
	{
		return m_array.AsSpan(start);
	}

	/// <summary>
	/// Do not call this method directly; use <see cref="M:OceanBase.Utilities.Utility.Resize``1(OceanBase.Utilities.ResizableArray{``0}@,System.Int32)" />.
	/// </summary>
	internal void DoResize(int length)
	{
		if (m_array == null || length > m_array.Length)
		{
			System.Array.Resize(ref m_array, Math.Max(length, Count * 2));
		}
	}
}
