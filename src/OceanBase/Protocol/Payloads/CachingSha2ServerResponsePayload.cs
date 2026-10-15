using System;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

internal readonly struct CachingSha2ServerResponsePayload
{
	public const byte Signature = 1;

	public const byte SuccessSignature = 3;

	public const byte FullAuthRequiredSignature = 4;

	public bool Succeeded { get; }

	public bool FullAuthRequired { get; }

	private CachingSha2ServerResponsePayload(bool succeeded, bool fullAuthRequired)
	{
		Succeeded = succeeded;
		FullAuthRequired = fullAuthRequired;
	}

	public static CachingSha2ServerResponsePayload Create(ReadOnlySpan<byte> span)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(span);
		byteArrayReader.ReadByte(1);
		byte b = byteArrayReader.ReadByte();
		return new CachingSha2ServerResponsePayload(b == 3, b == 4);
	}
}
