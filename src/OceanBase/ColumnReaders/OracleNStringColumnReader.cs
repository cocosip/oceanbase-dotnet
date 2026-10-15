using System;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol.Payloads;

namespace OceanBase.ColumnReaders;

/// <summary>
/// Column reader for Oracle NCHAR/NVARCHAR2 result columns.
/// Per the dual-charset design, NCHAR/NVARCHAR2 columns are decoded with
/// <see cref="P:OceanBase.Core.ServerSession.NCharacterEncoding" /> (negotiated via NCHARACTER_SET_CONNECTION),
/// NOT the session CharacterEncoding (from SET NAMES). This ensures NCHAR types are
/// independent of the connection-level CharacterSet option (e.g., GBK for VARCHAR2).
///
/// Fallback: NCharacterEncoding → CharacterEncoding → UTF8.
/// </summary>
internal sealed class OracleNStringColumnReader : ColumnReader
{
	private readonly Encoding m_encoding;

	public OracleNStringColumnReader(ConnectionFacade connection)
	{
		m_encoding = connection.Session?.NCharacterEncoding ?? connection.Session?.CharacterEncoding ?? Encoding.UTF8;
	}

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		if (TryDecodeWidenedUtf16LittleEndianPrefix(data, m_encoding, out string value))
		{
			return value;
		}
		return m_encoding.GetString(data);
	}

	/// <summary>
	/// Decodes the OceanBase UTF16 compatibility layout in which every UTF-16LE code unit is
	/// widened to four bytes: <c>low-byte, high-byte, 0, 0</c>. Fixed-width NCHAR values can
	/// append their padding in the normal national encoding, so only the widened prefix is decoded here.
	/// </summary>
	private static bool TryDecodeWidenedUtf16LittleEndianPrefix(ReadOnlySpan<byte> data, Encoding fallbackEncoding, out string value)
	{
		value = "";
		if (data.Length < 4)
		{
			return false;
		}
		int i;
		for (i = 0; i + 4 <= data.Length && data[i + 2] == 0 && data[i + 3] == 0; i += 4)
		{
		}
		if (i == 0)
		{
			return false;
		}
		StringBuilder stringBuilder = new StringBuilder(i / 4 + (data.Length - i) / 2);
		for (int j = 0; j < i; j += 4)
		{
			stringBuilder.Append((char)(data[j] | (data[j + 1] << 8)));
		}
		if (i < data.Length)
		{
			int num = i;
			stringBuilder.Append(fallbackEncoding.GetString(data.Slice(num, data.Length - num)));
		}
		value = stringBuilder.ToString();
		return true;
	}
}
