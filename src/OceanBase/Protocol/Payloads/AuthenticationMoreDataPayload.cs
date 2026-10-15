using System;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

internal readonly struct AuthenticationMoreDataPayload
{
	public const byte Signature = 1;

	public byte[] Data { get; }

	public static AuthenticationMoreDataPayload Create(ReadOnlySpan<byte> span)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(span);
		byteArrayReader.ReadByte(1);
		return new AuthenticationMoreDataPayload(byteArrayReader.ReadByteString(byteArrayReader.BytesRemaining).ToArray());
	}

	private AuthenticationMoreDataPayload(byte[] data)
	{
		Data = data;
	}
}
