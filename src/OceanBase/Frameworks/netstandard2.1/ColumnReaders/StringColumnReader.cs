using System;
using System.Text;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class StringColumnReader : ColumnReader
{
	private readonly Encoding m_encoding;

	public static StringColumnReader Instance { get; } = new StringColumnReader(Encoding.UTF8);

	public StringColumnReader(Encoding encoding)
	{
		m_encoding = encoding;
	}

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return m_encoding.GetString(data);
	}
}
