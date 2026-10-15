using System;

namespace OceanBase.Protocol.Serialization;

internal readonly struct Packet(ArraySegment<byte> contents)
{
	public ArraySegment<byte> Contents { get; } = contents;
}
