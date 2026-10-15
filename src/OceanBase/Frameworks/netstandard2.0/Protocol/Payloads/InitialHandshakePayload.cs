using System;
using System.Text;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase.Protocol.Payloads;

internal sealed class InitialHandshakePayload
{
	private const byte c_protocolVersion = 10;

	public ProtocolCapabilities ProtocolCapabilities { get; }

	public byte[] ServerVersion { get; }

	public int ConnectionId { get; }

	public byte[] AuthPluginData { get; }

	public string? AuthPluginName { get; }

	public static InitialHandshakePayload Create(ReadOnlySpan<byte> span)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(span);
		byteArrayReader.ReadByte(10);
		ReadOnlySpan<byte> readOnlySpan = byteArrayReader.ReadNullTerminatedByteString();
		int connectionId = byteArrayReader.ReadInt32();
		byte[] array = null;
		ReadOnlySpan<byte> readOnlySpan2 = byteArrayReader.ReadByteString(8);
		string authPluginName = null;
		byteArrayReader.ReadByte(0);
		ProtocolCapabilities protocolCapabilities = (ProtocolCapabilities)byteArrayReader.ReadUInt16();
		if (byteArrayReader.BytesRemaining > 0)
		{
			byteArrayReader.ReadByte();
			_ = (ushort)byteArrayReader.ReadInt16();
			ushort num = byteArrayReader.ReadUInt16();
			protocolCapabilities = (ProtocolCapabilities)((ulong)protocolCapabilities | ((ulong)num << 16));
			byte b = byteArrayReader.ReadByte();
			byteArrayReader.Offset += 6;
			long num2 = byteArrayReader.ReadInt32();
			if ((protocolCapabilities & ProtocolCapabilities.LongPassword) == ProtocolCapabilities.None)
			{
				protocolCapabilities = (ProtocolCapabilities)((ulong)protocolCapabilities | (ulong)(num2 << 32));
			}
			if ((protocolCapabilities & ProtocolCapabilities.SecureConnection) != ProtocolCapabilities.None)
			{
				ReadOnlySpan<byte> readOnlySpan3 = byteArrayReader.ReadByteString(Math.Max(13, b - 8));
				array = new byte[readOnlySpan2.Length + readOnlySpan3.Length];
				readOnlySpan2.CopyTo(array);
				readOnlySpan3.CopyTo(array.AsSpan(readOnlySpan2.Length));
			}
			if ((protocolCapabilities & ProtocolCapabilities.PluginAuth) != ProtocolCapabilities.None)
			{
				authPluginName = Utility.GetString(Encoding.UTF8, byteArrayReader.ReadNullOrEofTerminatedByteString());
			}
		}
		if (array == null)
		{
			array = readOnlySpan2.ToArray();
		}
		if (byteArrayReader.BytesRemaining != 0)
		{
			throw new FormatException("Extra bytes at end of payload.");
		}
		return new InitialHandshakePayload(protocolCapabilities, readOnlySpan.ToArray(), connectionId, array, authPluginName);
	}

	private InitialHandshakePayload(ProtocolCapabilities protocolCapabilities, byte[] serverVersion, int connectionId, byte[] authPluginData, string? authPluginName)
	{
		ProtocolCapabilities = protocolCapabilities;
		ServerVersion = serverVersion;
		ConnectionId = connectionId;
		AuthPluginData = authPluginData;
		AuthPluginName = authPluginName;
	}
}
