using System;
using System.Buffers.Binary;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

/// <summary>
/// Reads OceanBase Oracle-mode INTERVAL YEAR TO MONTH values (ColumnType.OracleIntervalYM) from the binary row protocol.
/// The on-the-wire representation is fixed-length (7 bytes) and is compatible with Oracle's internal INTERVALYM encoding:
/// - 1 bytes flag, 0=&gt;'+', 1=&gt;'-'
/// - 4 bytes years (little-endian)
/// - 1 bytes months
/// - 1 bytes length of yearstr
/// </summary>
internal sealed class BinaryOracleIntervalYMColumnReader : ColumnReader
{
	public static BinaryOracleIntervalYMColumnReader Instance { get; } = new BinaryOracleIntervalYMColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (data.Length != 7)
		{
			throw new FormatException($"Couldn't interpret value as a valid OracleIntervalYM; expected 7 bytes but got {data.Length}.");
		}
		bool isNegative = data[0] != 0;
		int years = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(1, 4));
		int months = data[5];
		_ = ref data[6];
		return OracleIntervalYM.FromWire(isNegative, years, months);
	}
}
