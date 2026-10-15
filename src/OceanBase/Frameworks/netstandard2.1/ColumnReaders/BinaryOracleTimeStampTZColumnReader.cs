using System;
using System.Buffers.Binary;
using System.Text;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

/// <summary>
/// Reads OceanBase Oracle-mode TIMESTAMP WITH TIME ZONE values (ColumnType.OracleTimeStampTZ) from the binary row protocol.
/// - 2 bytes year
/// - 1 bytes month
/// - 1 bytes day
/// - 1 bytes hour
/// - 1 byte minute
/// - 1 byte second
/// - 4 bytes nanosecond (little-endian)
/// - 1 bytes scale
/// </summary>
internal sealed class BinaryOracleTimeStampTZColumnReader : ColumnReader
{
	public static BinaryOracleTimeStampTZColumnReader Instance { get; } = new BinaryOracleTimeStampTZColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (data.Length < 14)
		{
			throw new FormatException($"Couldn't interpret value as a valid OracleTimeStampTZ; expected at least 14 bytes but got {data.Length}.");
		}
		int year = data[0] * 100 + data[1];
		int month = data[2];
		int day = data[3];
		int hour = data[4];
		int minute = data[5];
		int second = data[6];
		int nanoseconds = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(7, 4));
		int scale = data[11];
		sbyte offsetHours = (sbyte)data[12];
		byte offsetMinutes = data[13];
		string timeZoneName = null;
		if (data.Length >= 15)
		{
			int num = data[14];
			if (num != 0 && data.Length >= 15 + num)
			{
				byte[] array = new byte[num];
				data.Slice(15, num).CopyTo(array);
				timeZoneName = Encoding.UTF8.GetString(array);
			}
		}
		return OracleTimeStampTZ.FromWire(year, month, day, hour, minute, second, nanoseconds, scale, offsetHours, offsetMinutes, timeZoneName);
	}
}
