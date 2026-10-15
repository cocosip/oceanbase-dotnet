using System;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

/// <summary>
///     Helper class to parse Column count packet.
///     https://mariadb.com/kb/en/result-set-packets/#column-count-packet
///     Packet contains a columnCount, and - if capability MARIADB_CLIENT_CACHE_METADATA is set - a flag to indicate if metadata follows
/// </summary>
internal readonly struct ColumnCountPayload
{
	public int ColumnCount { get; }

	public bool MetadataFollows { get; }

	private ColumnCountPayload(int columnCount, bool metadataFollows)
	{
		ColumnCount = columnCount;
		MetadataFollows = metadataFollows;
	}

	public static ColumnCountPayload Create(ReadOnlySpan<byte> span, bool supportsOptionalMetadata)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(span);
		int columnCount = (int)byteArrayReader.ReadLengthEncodedInteger();
		bool metadataFollows = !supportsOptionalMetadata || byteArrayReader.BytesRemaining == 0 || byteArrayReader.ReadByte() == 1;
		return new ColumnCountPayload(columnCount, metadataFollows);
	}
}
