using System;
using System.Text;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

/// <summary>
/// Binary-protocol reader for Oracle UDT columns (OBJECT/ARRAY/STRUCT).
/// </summary>
/// <remarks>
/// Decodes the payload using the OceanBase Oracle-mode binary UDT format.
/// The column definition carries the UDT type name in the <see cref="P:OceanBase.Protocol.Payloads.ColumnDefinitionPayload.ComplexTypeName" /> field
/// for complex types.
/// </remarks>
internal sealed class BinaryOracleUdtColumnReader : ColumnReader
{
	private readonly bool m_isArray;

	private readonly Encoding m_stringEncoding;

	public BinaryOracleUdtColumnReader(bool isArray, Encoding stringEncoding)
	{
		m_isArray = isArray;
		m_stringEncoding = stringEncoding;
	}

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return OracleUdtTdsDecoder.ReadValue(data, columnDefinition.ComplexTypeName ?? columnDefinition.SchemaName ?? string.Empty, m_isArray, m_stringEncoding);
	}
}
