using System;
using System.Buffers.Text;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol.Payloads;
using OceanBase.Utilities;

namespace OceanBase.ColumnReaders;

internal sealed class TextDateTimeColumnReader : ColumnReader
{
	private readonly bool m_allowZeroDateTime;

	private readonly bool m_convertZeroDateTime;

	private readonly DateTimeKind m_dateTimeKind;

	public TextDateTimeColumnReader(ConnectionFacade connection)
	{
		m_allowZeroDateTime = connection.AllowZeroDateTime;
		m_convertZeroDateTime = connection.ConvertZeroDateTime;
		m_dateTimeKind = connection.DateTimeKind;
	}

	public override object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return ParseDateTime(data, m_convertZeroDateTime, m_allowZeroDateTime, m_dateTimeKind);
	}

	public static object ParseDateTime(ReadOnlySpan<byte> data, bool convertZeroDateTime, bool allowZeroDateTime, DateTimeKind dateTimeKind)
	{
		Exception innerException = null;
		int value2;
		int value3;
		int value4;
		int value5;
		int value6;
		int value7;
		if (Utf8Parser.TryParse(data, out int value, out int i, '\0') && i == 4 && data.Length >= 5 && data[4] == 45)
		{
			if (Utf8Parser.TryParse(data.Slice(5, data.Length - 5), out value2, out i, '\0') && i == 2 && data.Length >= 8 && data[7] == 45)
			{
				if (Utf8Parser.TryParse(data.Slice(8, data.Length - 8), out value3, out i, '\0') && i == 2)
				{
					if (value == 0 && value2 == 0 && value3 == 0)
					{
						if (convertZeroDateTime)
						{
							return DateTime.MinValue;
						}
						if (allowZeroDateTime)
						{
							return default(MySqlDateTime);
						}
						throw new InvalidCastException("Unable to convert MySQL date/time to System.DateTime, set AllowZeroDateTime=True or ConvertZeroDateTime=True in the connection string. See https://mysqlconnector.net/connection-options/");
					}
					if (data.Length == 10)
					{
						value4 = 0;
						value5 = 0;
						value6 = 0;
						value7 = 0;
						goto IL_0238;
					}
					if (data[10] == 32)
					{
						if (Utf8Parser.TryParse(data.Slice(11, data.Length - 11), out value4, out i, '\0') && i == 2 && data.Length >= 14 && data[13] == 58)
						{
							if (Utf8Parser.TryParse(data.Slice(14, data.Length - 14), out value5, out i, '\0') && i == 2 && data.Length >= 17 && data[16] == 58)
							{
								if (Utf8Parser.TryParse(data.Slice(17, data.Length - 17), out value6, out i, '\0') && i == 2)
								{
									if (data.Length == 19)
									{
										value7 = 0;
										goto IL_0238;
									}
									if (data[19] == 46)
									{
										if (Utf8Parser.TryParse(data.Slice(20, data.Length - 20), out value7, out i, '\0') && i == data.Length - 20 && i <= 7)
										{
											for (; i < 7; i++)
											{
												value7 *= 10;
											}
											goto IL_0238;
										}
									}
								}
							}
						}
					}
				}
			}
		}
		goto IL_0290;
		IL_0238:
		try
		{
			object result;
			if (!allowZeroDateTime)
			{
				result = new DateTime(value, value2, value3, value4, value5, value6, dateTimeKind).AddTicks(value7);
			}
			else
			{
				if (value7 % 10 != 0)
				{
					throw new NotSupportedException("MySqlDateTime does not support sub-microsecond precision");
				}
				result = new MySqlDateTime(value, value2, value3, value4, value5, value6, value7 / 10);
			}
			return result;
		}
		catch (Exception ex)
		{
			innerException = ex;
		}
		goto IL_0290;
		IL_0290:
		throw new FormatException("Couldn't interpret value as a valid DateTime: " + Utility.GetString(Encoding.UTF8, data), innerException);
	}
}
