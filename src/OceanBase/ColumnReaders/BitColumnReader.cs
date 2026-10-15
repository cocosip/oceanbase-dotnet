using System;
using System.Buffers.Text;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class BitColumnReader : ColumnReader
{
	public static BitColumnReader Instance { get; } = new BitColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return DoReadValue(data, columnDefinition);
	}

	public override int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return checked((int)DoReadValue(data, columnDefinition));
	}

	private static ulong DoReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if ((columnDefinition.ColumnFlags & ColumnFlags.Binary) == 0)
		{
			ulong num = 0uL;
			for (int i = 0; i < data.Length; i++)
			{
				num = num * 256 + data[i];
			}
			return num;
		}
		if (columnDefinition.ColumnLength <= 5 && data.Length == 1 && data[0] < (byte)(1 << (int)columnDefinition.ColumnLength))
		{
			return data[0];
		}
		if (Utf8Parser.TryParse(data, out ulong value, out int bytesConsumed, '\0') && bytesConsumed == data.Length)
		{
			return value;
		}
		throw new FormatException();
	}
}
