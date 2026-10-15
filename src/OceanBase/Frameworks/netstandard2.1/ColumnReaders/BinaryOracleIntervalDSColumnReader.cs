using System;
using System.Buffers.Binary;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

/// <summary>
/// Reads OceanBase Oracle-mode INTERVAL DAY TO SECOND values (ColumnType.OracleIntervalDS) from the binary row protocol.
/// The on-the-wire representation is fixed-length (14 bytes) and is compatible with OceanBase Oracle-mode INTERVALDS encoding:
/// - 1 bytes flag, 0=&gt;'+', 1=&gt;'-'
/// - 4 bytes days (little-endian)
/// - 1 byte hours
/// - 1 byte minutes
/// - 1 byte seconds
/// - 4 bytes nanoseconds (little-endian)
/// - 1 bytes length of the sting of the days
/// - 1 bytes scale of nanoseconds
/// </summary>
internal sealed class BinaryOracleIntervalDSColumnReader : ColumnReader
{
	public static BinaryOracleIntervalDSColumnReader Instance { get; } = new BinaryOracleIntervalDSColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (data.Length != 14)
		{
			throw new FormatException($"Couldn't interpret value as a valid OracleIntervalDS; expected 14 bytes but got {data.Length}.");
		}
		bool isNegative = data[0] != 0;
		int days = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(1, 4));
		int hours = data[5];
		int minutes = data[6];
		int seconds = data[7];
		int fractionalNanoseconds = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(8, 4));
		int scale = data[13];
		return OracleIntervalDS.FromWire(isNegative, days, hours, minutes, seconds, fractionalNanoseconds, scale);
	}
}
