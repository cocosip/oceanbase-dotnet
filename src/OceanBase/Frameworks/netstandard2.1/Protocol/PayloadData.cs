using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace OceanBase.Protocol;

internal readonly struct PayloadData : IDisposable
{
	private readonly bool m_isPooled;

	public ReadOnlyMemory<byte> Memory { get; }

	public ReadOnlySpan<byte> Span => Memory.Span;

	public byte HeaderByte => Span[0];

	public PayloadData(byte[] data)
	{
		m_isPooled = false;
		Memory = data;
	}

	public PayloadData(ReadOnlyMemory<byte> data, bool isPooled = false)
	{
		Memory = data;
		m_isPooled = isPooled;
	}

	public void Dispose()
	{
		if (m_isPooled && MemoryMarshal.TryGetArray(Memory, out var segment))
		{
			ArrayPool<byte>.Shared.Return(segment.Array);
		}
	}
}
