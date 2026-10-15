using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace OceanBase.Utilities;

internal static class SocketExtensions
{
	public static SocketAwaitable ReceiveAsync(this Socket socket, SocketAwaitable awaitable)
	{
		awaitable.Reset();
		if (!socket.ReceiveAsync(awaitable.EventArgs))
		{
			awaitable.WasCompleted = true;
		}
		return awaitable;
	}

	public static SocketAwaitable SendAsync(this Socket socket, SocketAwaitable awaitable)
	{
		awaitable.Reset();
		if (!socket.SendAsync(awaitable.EventArgs))
		{
			awaitable.WasCompleted = true;
		}
		return awaitable;
	}

	public static void SetBuffer(this SocketAsyncEventArgs args, Memory<byte> buffer)
	{
		MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out ArraySegment<byte> segment);
		args.SetBuffer(segment.Array, segment.Offset, segment.Count);
	}

	public static int Send(this Socket socket, ReadOnlyMemory<byte> data, SocketFlags flags)
	{
		MemoryMarshal.TryGetArray(data, out var segment);
		return socket.Send(segment.Array, segment.Offset, segment.Count, flags);
	}

	public static void SetKeepAlive(this Socket socket, uint keepAliveTimeSeconds)
	{
		socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, optionValue: true);
		if (keepAliveTimeSeconds != 0 && Utility.IsWindows())
		{
			uint num = ((keepAliveTimeSeconds > 4294967) ? uint.MaxValue : (keepAliveTimeSeconds * 1000));
			socket.IOControl(IOControlCode.KeepAliveValues, new byte[12]
			{
				1,
				0,
				0,
				0,
				(byte)(num & 0xFF),
				(byte)((num >> 8) & 0xFF),
				(byte)((num >> 16) & 0xFF),
				(byte)((num >> 24) & 0xFF),
				232,
				3,
				0,
				0
			}, null);
		}
	}
}
