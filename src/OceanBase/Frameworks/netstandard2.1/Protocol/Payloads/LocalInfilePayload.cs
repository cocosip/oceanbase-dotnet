using System;
using System.Text;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

internal readonly struct LocalInfilePayload
{
	public const byte Signature = 251;

	public string FileName { get; }

	public static LocalInfilePayload Create(ReadOnlySpan<byte> span)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(span);
		byteArrayReader.ReadByte(251);
		return new LocalInfilePayload(Encoding.UTF8.GetString(byteArrayReader.ReadByteString(byteArrayReader.BytesRemaining)));
	}

	private LocalInfilePayload(string fileName)
	{
		FileName = fileName;
	}
}
