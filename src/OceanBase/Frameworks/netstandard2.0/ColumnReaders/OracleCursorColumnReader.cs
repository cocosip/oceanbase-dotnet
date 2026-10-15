using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using OceanBase.Core;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

internal sealed class OracleCursorColumnReader : ColumnReader
{
	private readonly ConnectionFacade m_connection;

	public OracleCursorColumnReader(ConnectionFacade connection)
	{
		m_connection = connection;
	}

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (data.Length == 0)
		{
			throw new FormatException("Couldn't interpret value as a valid OracleCursorId; data is empty.");
		}
		int value;
		int bytesConsumed;
		if (data.Length == 4)
		{
			value = BinaryPrimitives.ReadInt32LittleEndian(data);
		}
		else if (!Utf8Parser.TryParse(data, out value, out bytesConsumed, '\0'))
		{
			throw new FormatException("Invalid cursor id payload for Oracle cursor column.");
		}
		return new OracleRefCursor(m_connection.AsOracleConnection ?? throw new InvalidOperationException("Oracle cursor values can only be read from an OracleConnection."), value);
	}
}
