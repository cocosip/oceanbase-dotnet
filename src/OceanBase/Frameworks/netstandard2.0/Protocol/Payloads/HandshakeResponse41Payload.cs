using System;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

internal static class HandshakeResponse41Payload
{
	private static ByteBufferWriter CreateCapabilitiesPayload(ProtocolCapabilities serverCapabilities, ConnectionSettings cs, CompressionMethod compressionMethod, CharacterSet characterSet, ProtocolCapabilities additionalCapabilities = ProtocolCapabilities.None)
	{
		ByteBufferWriter byteBufferWriter = new ByteBufferWriter();
		ProtocolCapabilities protocolCapabilities = (ProtocolCapabilities)((ulong)(ProtocolCapabilities.Protocol41 | ProtocolCapabilities.QueryAttributes | (cs.InteractiveSession ? (serverCapabilities & ProtocolCapabilities.Interactive) : ProtocolCapabilities.None) | (serverCapabilities & ProtocolCapabilities.LongPassword) | (serverCapabilities & ProtocolCapabilities.Transactions) | ProtocolCapabilities.SecureConnection | (serverCapabilities & ProtocolCapabilities.PluginAuth) | (serverCapabilities & ProtocolCapabilities.PluginAuthLengthEncodedClientData) | ProtocolCapabilities.MultiStatements | ProtocolCapabilities.MultiResults) | (ulong)(cs.AllowLoadLocalInfile ? 128u : 0u) | (ulong)(string.IsNullOrWhiteSpace(cs.Database) ? 0u : 8u) | (ulong)(cs.UseAffectedRows ? 0u : 2u) | (ulong)((compressionMethod == CompressionMethod.Zlib) ? 32u : 0u) | (ulong)(serverCapabilities & ProtocolCapabilities.ConnectionAttributes) | (ulong)(serverCapabilities & ProtocolCapabilities.SessionTrack) | (ulong)(serverCapabilities & ProtocolCapabilities.DeprecateEof) | (ulong)((compressionMethod == CompressionMethod.Zstandard) ? 67108864u : 0u) | (ulong)(serverCapabilities & ProtocolCapabilities.MariaDbCacheMetadata) | 0x20000000 | (ulong)additionalCapabilities);
		byteBufferWriter.Write((int)protocolCapabilities);
		byteBufferWriter.Write(1073741824);
		byteBufferWriter.Write((byte)characterSet);
		ReadOnlySpan<byte> span = new byte[19]
		{
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0
		};
		byteBufferWriter.Write(span);
		if ((serverCapabilities & ProtocolCapabilities.LongPassword) == ProtocolCapabilities.None)
		{
			byteBufferWriter.Write((int)((ulong)protocolCapabilities >> 32));
		}
		else
		{
			byteBufferWriter.Write(0u);
		}
		return byteBufferWriter;
	}

	public static PayloadData CreateWithSsl(ProtocolCapabilities serverCapabilities, ConnectionSettings cs, CompressionMethod compressionMethod, CharacterSet characterSet)
	{
		return CreateCapabilitiesPayload(serverCapabilities, cs, compressionMethod, characterSet, ProtocolCapabilities.Ssl).ToPayloadData();
	}

	public static PayloadData Create(InitialHandshakePayload handshake, ConnectionSettings cs, byte[] authenticationResponse, CompressionMethod compressionMethod, int? compressionLevel, CharacterSet characterSet, byte[]? connectionAttributes)
	{
		ByteBufferWriter byteBufferWriter = CreateCapabilitiesPayload(handshake.ProtocolCapabilities, cs, compressionMethod, characterSet, ProtocolCapabilities.None);
		byteBufferWriter.WriteNullTerminatedString(cs.UserID);
		byteBufferWriter.Write((byte)authenticationResponse.Length);
		byteBufferWriter.Write(authenticationResponse);
		if (!string.IsNullOrWhiteSpace(cs.Database))
		{
			byteBufferWriter.WriteNullTerminatedString(cs.Database);
		}
		if ((handshake.ProtocolCapabilities & ProtocolCapabilities.PluginAuth) != ProtocolCapabilities.None)
		{
			byteBufferWriter.Write((handshake.AuthPluginName == "caching_sha2_password") ? "caching_sha2_password\0"u8 : "mysql_native_password\0"u8);
		}
		if (connectionAttributes != null)
		{
			byteBufferWriter.Write(connectionAttributes);
		}
		if (compressionMethod == CompressionMethod.Zstandard)
		{
			byteBufferWriter.Write((byte)(compressionLevel ?? 10));
		}
		return byteBufferWriter.ToPayloadData();
	}
}
