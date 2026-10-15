using System;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

internal readonly struct EofPayload
{
	public const byte Signature = 254;

	public int WarningCount { get; }

	public ServerStatus ServerStatus { get; }

	public static EofPayload Create(ReadOnlySpan<byte> span)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(span);
		byteArrayReader.ReadByte(254);
		if (span.Length > 5)
		{
			throw new FormatException("Not an EOF packet");
		}
		ushort warningCount = byteArrayReader.ReadUInt16();
		ServerStatus status = (ServerStatus)byteArrayReader.ReadUInt16();
		if (byteArrayReader.BytesRemaining != 0)
		{
			throw new FormatException("Extra bytes at end of payload.");
		}
		return new EofPayload(warningCount, status);
	}

	/// <summary>
	/// Returns <c>true</c> if <paramref name="payload" /> contains an <a href="https://dev.mysql.com/doc/internals/en/packet-EOF_Packet.html">EOF packet</a>.
	/// Note that EOF packets can appear in places where a length-encoded integer (which starts with the same signature byte) may appear, so the length
	/// has to be checked to verify that it is an EOF packet.
	/// </summary>
	/// <param name="payload">The payload to examine.</param>
	/// <returns><c>true</c> if this is an EOF packet; otherwise, <c>false</c>.</returns>
	public static bool IsEof(PayloadData payload)
	{
		int length = payload.Span.Length;
		if (length > 0 && length < 9)
		{
			return payload.HeaderByte == 254;
		}
		return false;
	}

	private EofPayload(int warningCount, ServerStatus status)
	{
		WarningCount = warningCount;
		ServerStatus = status;
	}
}
