using System;
using System.Buffers.Binary;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

/// <summary>
/// Reads OceanBase Oracle-mode TIMESTAMP NANO values (ColumnType.OracleTimeStampNano) from the binary row protocol.
/// The on-the-wire representation is fixed-length (12 bytes) and is compatible with OceanBase Oracle-mode TIMESTAMPLTZ encoding:
/// - 2 bytes year
/// - 1 bytes month
/// - 1 bytes day
/// - 1 bytes hour
/// - 1 byte minute
/// - 1 byte second
/// - 4 bytes nanosecond (little-endian)
/// - 1 bytes scale
/// </summary>
internal sealed class BinaryOracleTimeStampNanoColumnReader : ColumnReader
{
	public static BinaryOracleTimeStampNanoColumnReader Instance { get; } = new BinaryOracleTimeStampNanoColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (data.Length != 12)
		{
			throw new FormatException($"Couldn't interpret value as a valid OracleTimeStampLTZ; expected 12 bytes but got {data.Length}.");
		}
		int year = data[0] * 100 + data[1];
		int month = data[2];
		int day = data[3];
		int hour = data[4];
		int minute = data[5];
		int second = data[6];
		int nanoseconds = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(7, 4));
		int scale = data[11];
		return OracleTimeStamp.FromWire(year, month, day, hour, minute, second, nanoseconds, scale);
	}
}
