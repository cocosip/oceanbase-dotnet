using System;
using System.Net.Sockets;

namespace OceanBase.Utilities;

internal static class SocketExtensions
{
	public static int Send(this Socket socket, ReadOnlyMemory<byte> data, SocketFlags flags)
	{
		return socket.Send(data.Span, flags);
	}

	public static void SetKeepAlive(this Socket socket, uint keepAliveTimeSeconds)
	{
		socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, optionValue: true);
		if (keepAliveTimeSeconds != 0)
		{
			socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.BlockSource, 1);
			socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TypeOfService, (int)keepAliveTimeSeconds);
		}
	}
}
