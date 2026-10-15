using System;
using OceanBase.Protocol.Payloads;
using OceanBase.Utilities;

namespace OceanBase.ColumnReaders;

internal sealed class TextTimeColumnReader : ColumnReader
{
	public static TextTimeColumnReader Instance { get; } = new TextTimeColumnReader();

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return Utility.ParseTimeSpan(data);
	}
}
