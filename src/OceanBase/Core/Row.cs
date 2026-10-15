using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using OceanBase.ColumnReaders;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Core;

internal sealed class Row
{
	private readonly struct OffsetLength(int offset, int length)
	{
		public int Offset { get; } = offset;

		public int Length { get; } = length;

		public static implicit operator OffsetLength((int Offset, int Length) x)
		{
			return new OffsetLength(x.Offset, x.Length);
		}

		public void Deconstruct(out int offset, out int length)
		{
			offset = Offset;
			length = Length;
		}
	}

	private readonly bool m_isBinary;

	private readonly OffsetLength[] m_dataOffsetLengths;

	private readonly ColumnReader[] m_columnReaders;

	private ReadOnlyMemory<byte> m_data;

	public object this[int ordinal] => GetValue(ordinal);

	public object this[string name] => GetValue(ResultSet.GetOrdinal(name));

	private ResultSet ResultSet { get; }

	private ConnectionFacade Connection => ResultSet.Connection;

	public Row(bool isBinary, ResultSet resultSet)
	{
		m_isBinary = isBinary;
		ResultSet = resultSet;
		ReadOnlySpan<ColumnDefinitionPayload> columnDefinitions = ResultSet.ColumnDefinitions;
		m_dataOffsetLengths = new OffsetLength[columnDefinitions.Length];
		m_columnReaders = new ColumnReader[columnDefinitions.Length];
		for (int i = 0; i < m_columnReaders.Length; i++)
		{
			m_columnReaders[i] = ColumnReader.Create(isBinary, columnDefinitions[i], resultSet.Connection);
		}
	}

	public void SetData(ReadOnlyMemory<byte> data)
	{
		m_data = data;
		if (m_isBinary)
		{
			Array.Clear(m_dataOffsetLengths, 0, m_dataOffsetLengths.Length);
			for (int i = 0; i < m_dataOffsetLengths.Length; i++)
			{
				if ((data.Span[(i + 2) / 8 + 1] & (1 << (i + 2) % 8)) != 0)
				{
					m_dataOffsetLengths[i] = (Offset: -1, Length: 0);
				}
			}
			ByteArrayReader reader = new ByteArrayReader(data.Span);
			reader.Offset += 1 + (m_dataOffsetLengths.Length + 7 + 2) / 8;
			for (int j = 0; j < m_dataOffsetLengths.Length; j++)
			{
				if (m_dataOffsetLengths[j].Offset != -1)
				{
					ColumnDefinitionPayload columnDefinitionPayload = ResultSet.ColumnDefinitions[j];
					int num;
					switch (columnDefinitionPayload.ColumnType)
					{
					case ColumnType.Double:
					case ColumnType.Longlong:
					case ColumnType.OracleBinaryDouble:
						num = 8;
						break;
					case ColumnType.Long:
					case ColumnType.Float:
					case ColumnType.Int24:
					case ColumnType.OracleNumber:
					case ColumnType.OracleBinaryFloat:
						num = 4;
						break;
					case ColumnType.Short:
					case ColumnType.Year:
						num = 2;
						break;
					case ColumnType.Tiny:
						num = 1;
						break;
					case ColumnType.OracleCursor:
						num = 4;
						break;
					case ColumnType.OracleComplex:
					case ColumnType.OracleArray:
					case ColumnType.OracleStruct:
						num = ReadUdtPayloadLength(ref reader, columnDefinitionPayload);
						break;
					case ColumnType.OracleIntervalDS:
						num = ReadFixedLengthPayloadLength(ref reader, 14, columnDefinitionPayload.ColumnType);
						break;
					case ColumnType.OracleIntervalYM:
						num = ReadFixedLengthPayloadLength(ref reader, 7, columnDefinitionPayload.ColumnType);
						break;
					case ColumnType.OracleTimeStampLTZ:
					case ColumnType.OracleTimeStampNano:
						num = 12;
						break;
					case ColumnType.Timestamp:
					case ColumnType.Date:
					case ColumnType.Time:
					case ColumnType.DateTime:
					case ColumnType.NewDate:
						num = reader.ReadByte();
						break;
					case ColumnType.Timestamp2:
					case ColumnType.DateTime2:
						throw new NotSupportedException($"ColumnType {columnDefinitionPayload.ColumnType} is not supported");
					default:
						num = checked((int)reader.ReadLengthEncodedInteger());
						break;
					}
					int num2 = num;
					m_dataOffsetLengths[j] = (Offset: reader.Offset, Length: num2);
					reader.Offset += num2;
				}
			}
		}
		else
		{
			ByteArrayReader byteArrayReader = new ByteArrayReader(data.Span);
			for (int k = 0; k < m_dataOffsetLengths.Length; k++)
			{
				int num3 = byteArrayReader.ReadLengthEncodedIntegerOrNull();
				m_dataOffsetLengths[k] = ((num3 == -1) ? (Offset: -1, Length: 0) : (Offset: byteArrayReader.Offset, Length: num3));
				byteArrayReader.Offset += m_dataOffsetLengths[k].Length;
			}
		}
	}

	public object GetValue(int ordinal)
	{
		if (ordinal < 0 || ordinal >= ResultSet.ColumnDefinitions.Length)
		{
			throw new ArgumentOutOfRangeException("ordinal");
		}
		if (m_dataOffsetLengths[ordinal].Offset == -1)
		{
			return DBNull.Value;
		}
		OffsetLength offsetLength = m_dataOffsetLengths[ordinal];
		offsetLength.Deconstruct(out var offset, out var length);
		int start = offset;
		int length2 = length;
		ReadOnlySpan<byte> span = m_data.Slice(start, length2).Span;
		ColumnDefinitionPayload columnDefinition = ResultSet.ColumnDefinitions[ordinal];
		return m_columnReaders[ordinal].ReadValue(span, columnDefinition);
	}

	public bool GetBoolean(int ordinal)
	{
		object value = GetValue(ordinal);
		if (!(value is bool result))
		{
			if (!(value is sbyte b))
			{
				if (!(value is byte b2))
				{
					if (!(value is short num))
					{
						if (!(value is ushort num2))
						{
							if (!(value is int num3))
							{
								if (!(value is uint num4))
								{
									if (!(value is long num5))
									{
										if (!(value is ulong num6))
										{
											if (value is decimal num7)
											{
												return num7 != 0m;
											}
											return (bool)value;
										}
										return num6 != 0;
									}
									return num5 != 0;
								}
								return num4 != 0;
							}
							return num3 != 0;
						}
						return num2 != 0;
					}
					return num != 0;
				}
				return b2 != 0;
			}
			return b != 0;
		}
		return result;
	}

	public sbyte GetSByte(int ordinal)
	{
		object value = GetValue(ordinal);
		checked
		{
			if (!(value is sbyte result))
			{
				if (!(value is byte b))
				{
					if (!(value is short num))
					{
						if (!(value is ushort num2))
						{
							if (!(value is int num3))
							{
								if (!(value is uint num4))
								{
									if (!(value is long num5))
									{
										if (!(value is ulong num6))
										{
											if (!(value is decimal num7))
											{
												if (value is bool flag)
												{
													return flag ? ((sbyte)1) : ((sbyte)0);
												}
												return (sbyte)value;
											}
											return (sbyte)num7;
										}
										return (sbyte)num6;
									}
									return (sbyte)num5;
								}
								return (sbyte)num4;
							}
							return (sbyte)num3;
						}
						return (sbyte)num2;
					}
					return (sbyte)num;
				}
				return (sbyte)b;
			}
			return result;
		}
	}

	public byte GetByte(int ordinal)
	{
		object value = GetValue(ordinal);
		checked
		{
			if (!(value is byte result))
			{
				if (!(value is sbyte b))
				{
					if (!(value is short num))
					{
						if (!(value is ushort num2))
						{
							if (!(value is int num3))
							{
								if (!(value is uint num4))
								{
									if (!(value is long num5))
									{
										if (!(value is ulong num6))
										{
											if (!(value is decimal num7))
											{
												if (value is bool flag)
												{
													return flag ? ((byte)1) : ((byte)0);
												}
												return (byte)value;
											}
											return (byte)num7;
										}
										return (byte)num6;
									}
									return (byte)num5;
								}
								return (byte)num4;
							}
							return (byte)num3;
						}
						return (byte)num2;
					}
					return (byte)num;
				}
				return (byte)b;
			}
			return result;
		}
	}

	public long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
	{
		CheckBinaryColumn(ordinal);
		if (buffer == null)
		{
			return m_dataOffsetLengths[ordinal].Length;
		}
		CheckBufferArguments(dataOffset, buffer, bufferOffset, length);
		int num = (int)dataOffset;
		int num2 = Math.Max(0, Math.Min(m_dataOffsetLengths[ordinal].Length - num, length));
		if (num2 > 0)
		{
			m_data.Slice(m_dataOffsetLengths[ordinal].Offset + num, num2).Span.CopyTo(buffer.AsSpan(bufferOffset));
		}
		return num2;
	}

	public char GetChar(int ordinal)
	{
		string text = (string)GetValue(ordinal);
		if (text.Length <= 0)
		{
			throw new InvalidCastException();
		}
		return text[0];
	}

	public long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
	{
		string text = GetString(ordinal);
		if (buffer == null)
		{
			return text.Length;
		}
		CheckBufferArguments(dataOffset, buffer, bufferOffset, length);
		int num = (int)dataOffset;
		int num2 = Math.Max(0, Math.Min(text.Length - num, length));
		if (num2 > 0)
		{
			text.CopyTo(num, buffer, bufferOffset, num2);
		}
		return num2;
	}

	public Guid GetGuid(int ordinal)
	{
		object value = GetValue(ordinal);
		if (value is Guid)
		{
			return (Guid)value;
		}
		if (value is string input && Guid.TryParse(input, out var result))
		{
			return result;
		}
		if (value is byte[] array && array.Length == 16)
		{
			return Connection.GuidFormat switch
			{
				MySqlGuidFormat.Binary16 => GuidBinary16ColumnReader.ReadGuid(array), 
				MySqlGuidFormat.TimeSwapBinary16 => GuidTimeSwapBinary16ColumnReader.ReadGuid(array), 
				_ => GuidLittleEndianBinary16ColumnReader.ReadGuid(array), 
			};
		}
		return (Guid)value;
	}

	public short GetInt16(int ordinal)
	{
		object value = GetValue(ordinal);
		checked
		{
			if (!(value is short result))
			{
				if (!(value is sbyte result2))
				{
					if (!(value is byte result3))
					{
						if (!(value is ushort num))
						{
							if (!(value is int num2))
							{
								if (!(value is uint num3))
								{
									if (!(value is long num4))
									{
										if (!(value is ulong num5))
										{
											if (!(value is decimal num6))
											{
												if (value is bool flag)
												{
													return flag ? ((short)1) : ((short)0);
												}
												return (short)value;
											}
											return (short)num6;
										}
										return (short)num5;
									}
									return (short)num4;
								}
								return (short)num3;
							}
							return (short)num2;
						}
						return (short)num;
					}
					return result3;
				}
				return result2;
			}
			return result;
		}
	}

	public int GetInt32(int ordinal)
	{
		if (ordinal < 0)
		{
			throw new ArgumentOutOfRangeException("ordinal");
		}
		if (ordinal >= ResultSet.ColumnDefinitions.Length)
		{
			throw new ArgumentOutOfRangeException("ordinal");
		}
		if (m_dataOffsetLengths[ordinal].Offset == -1)
		{
			throw new InvalidCastException("Can't convert NULL to Int32");
		}
		OffsetLength offsetLength = m_dataOffsetLengths[ordinal];
		offsetLength.Deconstruct(out var offset, out var length);
		int start = offset;
		int length2 = length;
		ReadOnlySpan<byte> span = m_data.Slice(start, length2).Span;
		ColumnDefinitionPayload columnDefinition = ResultSet.ColumnDefinitions[ordinal];
		int? num = m_columnReaders[ordinal].TryReadInt32(span, columnDefinition);
		if (num.HasValue)
		{
			return num.GetValueOrDefault();
		}
		throw new InvalidCastException($"Can't convert {ResultSet.GetColumnType(ordinal)} to Int32");
	}

	public long GetInt64(int ordinal)
	{
		object value = GetValue(ordinal);
		if (!(value is long result))
		{
			if (!(value is sbyte b))
			{
				if (!(value is byte b2))
				{
					if (!(value is short num))
					{
						if (!(value is ushort num2))
						{
							if (!(value is int num3))
							{
								if (!(value is uint num4))
								{
									if (!(value is ulong num5))
									{
										if (!(value is decimal num6))
										{
											if (value is bool flag)
											{
												return flag ? 1 : 0;
											}
											return (long)value;
										}
										return (long)num6;
									}
									return checked((long)num5);
								}
								return num4;
							}
							return num3;
						}
						return num2;
					}
					return num;
				}
				return b2;
			}
			return b;
		}
		return result;
	}

	public ushort GetUInt16(int ordinal)
	{
		object value = GetValue(ordinal);
		checked
		{
			if (!(value is ushort result))
			{
				if (!(value is sbyte b))
				{
					if (!(value is byte result2))
					{
						if (!(value is short num))
						{
							if (!(value is int num2))
							{
								if (!(value is uint num3))
								{
									if (!(value is long num4))
									{
										if (!(value is ulong num5))
										{
											if (!(value is decimal num6))
											{
												if (value is bool flag)
												{
													return flag ? ((ushort)1) : ((ushort)0);
												}
												return (ushort)value;
											}
											return (ushort)num6;
										}
										return (ushort)num5;
									}
									return (ushort)num4;
								}
								return (ushort)num3;
							}
							return (ushort)num2;
						}
						return (ushort)num;
					}
					return result2;
				}
				return (ushort)b;
			}
			return result;
		}
	}

	public uint GetUInt32(int ordinal)
	{
		object value = GetValue(ordinal);
		checked
		{
			if (!(value is uint result))
			{
				if (!(value is sbyte b))
				{
					if (!(value is byte result2))
					{
						if (!(value is short num))
						{
							if (!(value is ushort result3))
							{
								if (!(value is int num2))
								{
									if (!(value is long num3))
									{
										if (!(value is ulong num4))
										{
											if (!(value is decimal num5))
											{
												if (value is bool flag)
												{
													return flag ? 1u : 0u;
												}
												return (uint)value;
											}
											return (uint)num5;
										}
										return (uint)num4;
									}
									return (uint)num3;
								}
								return (uint)num2;
							}
							return result3;
						}
						return (uint)num;
					}
					return result2;
				}
				return (uint)b;
			}
			return result;
		}
	}

	public ulong GetUInt64(int ordinal)
	{
		object value = GetValue(ordinal);
		checked
		{
			if (!(value is ulong result))
			{
				if (!(value is sbyte b))
				{
					if (!(value is byte b2))
					{
						if (!(value is short num))
						{
							if (!(value is ushort num2))
							{
								if (!(value is int num3))
								{
									if (!(value is uint num4))
									{
										if (!(value is long num5))
										{
											if (!(value is decimal num6))
											{
												if (value is bool flag)
												{
													return unchecked((ulong)(flag ? 1 : 0));
												}
												return (ulong)value;
											}
											return (ulong)num6;
										}
										return (ulong)num5;
									}
									return num4;
								}
								return (ulong)num3;
							}
							return num2;
						}
						return (ulong)num;
					}
					return b2;
				}
				return (ulong)b;
			}
			return result;
		}
	}

	public DateTime GetDateTime(int ordinal)
	{
		object obj = GetValue(ordinal);
		if (obj is string { Length: var length } text)
		{
			if (length < 10 || length > 26)
			{
				throw new FormatException($"Couldn't interpret value as a valid DateTime: {obj}");
			}
			obj = TextDateTimeColumnReader.ParseDateTime(Encoding.UTF8.GetBytes(text), Connection.ConvertZeroDateTime, Connection.AllowZeroDateTime, Connection.DateTimeKind);
		}
		if (obj is MySqlDateTime mySqlDateTime)
		{
			return mySqlDateTime.GetDateTime();
		}
		return (DateTime)obj;
	}

	public DateTimeOffset GetDateTimeOffset(int ordinal)
	{
		return new DateTimeOffset(DateTime.SpecifyKind(GetDateTime(ordinal), DateTimeKind.Utc));
	}

	public Stream GetStream(int ordinal)
	{
		CheckBinaryColumn(ordinal);
		OffsetLength offsetLength = m_dataOffsetLengths[ordinal];
		var (num3, count) = offsetLength;
		if (!MemoryMarshal.TryGetArray(m_data, out var segment))
		{
			throw new InvalidOperationException("Can't get underlying array.");
		}
		return new MemoryStream(segment.Array, segment.Offset + num3, count, writable: false);
	}

	public string GetString(int ordinal)
	{
		return (string)GetValue(ordinal);
	}

	public decimal GetDecimal(int ordinal)
	{
		object value = GetValue(ordinal);
		if (!(value is decimal result))
		{
			if (!(value is double num))
			{
				if (value is float num2)
				{
					return (decimal)num2;
				}
				return (decimal)value;
			}
			return (decimal)num;
		}
		return result;
	}

	public double GetDouble(int ordinal)
	{
		object value = GetValue(ordinal);
		if (!(value is double result))
		{
			if (!(value is float num))
			{
				if (value is decimal num2)
				{
					return (double)num2;
				}
				return (double)value;
			}
			return num;
		}
		return result;
	}

	public float GetFloat(int ordinal)
	{
		object value = GetValue(ordinal);
		object obj = value;
		if (!(obj is float result))
		{
			if (!(obj is double num))
			{
				if (obj is decimal num2)
				{
					return (float)num2;
				}
				return (float)value;
			}
			if (num >= -3.4028234663852886E+38 && num <= 3.4028234663852886E+38)
			{
				return (float)num;
			}
			throw new InvalidCastException("The value cannot be safely cast to Single.");
		}
		return result;
	}

	public MySqlDateTime GetMySqlDateTime(int ordinal)
	{
		object value = GetValue(ordinal);
		if (value is DateTime dt)
		{
			return new MySqlDateTime(dt);
		}
		return (MySqlDateTime)value;
	}

	public MySqlGeometry GetMySqlGeometry(int ordinal)
	{
		if (GetValue(ordinal) is byte[] bytes && ResultSet.ColumnDefinitions[ordinal].ColumnType == ColumnType.Geometry)
		{
			return new MySqlGeometry(bytes);
		}
		throw new InvalidCastException($"Can't convert {ResultSet.ColumnDefinitions[ordinal].ColumnType} to MySqlGeometry.");
	}

	public MySqlDecimal GetMySqlDecimal(int ordinal)
	{
		if (IsDBNull(ordinal))
		{
			return (MySqlDecimal)GetValue(ordinal);
		}
		OffsetLength offsetLength = m_dataOffsetLengths[ordinal];
		offsetLength.Deconstruct(out var offset, out var length);
		int start = offset;
		int length2 = length;
		ReadOnlySpan<byte> span = m_data.Slice(start, length2).Span;
		ColumnType columnType = ResultSet.ColumnDefinitions[ordinal].ColumnType;
		if ((columnType == ColumnType.Decimal || columnType == ColumnType.NewDecimal) ? true : false)
		{
			return new MySqlDecimal(Encoding.UTF8.GetString(span));
		}
		throw new InvalidCastException($"Can't convert {ResultSet.ColumnDefinitions[ordinal].ColumnType} to MySqlDecimal.");
	}

	public int GetValues(object[] values)
	{
		if (values == null)
		{
			throw new ArgumentNullException("values");
		}
		int num = Math.Min(values.Length, ResultSet.ColumnDefinitions.Length);
		for (int i = 0; i < num; i++)
		{
			values[i] = GetValue(i);
		}
		return num;
	}

	public bool IsDBNull(int ordinal)
	{
		return m_dataOffsetLengths[ordinal].Offset == -1;
	}

	private void CheckBinaryColumn(int ordinal)
	{
		if (m_dataOffsetLengths[ordinal].Offset == -1)
		{
			throw new InvalidCastException("Column is NULL.");
		}
		ColumnDefinitionPayload obj = ResultSet.ColumnDefinitions[ordinal];
		ColumnType columnType = obj.ColumnType;
		if ((obj.ColumnFlags & ColumnFlags.Binary) == 0 || (columnType != ColumnType.String && columnType != ColumnType.VarString && columnType != ColumnType.TinyBlob && columnType != ColumnType.Blob && columnType != ColumnType.MediumBlob && columnType != ColumnType.LongBlob && columnType != ColumnType.Geometry && columnType != ColumnType.Vector))
		{
			throw new InvalidCastException($"Can't convert {columnType} to bytes.");
		}
	}

	private static void CheckBufferArguments<T>(long dataOffset, T[] buffer, int bufferOffset, int length)
	{
		if (dataOffset < 0)
		{
			throw new ArgumentOutOfRangeException("dataOffset");
		}
		if (dataOffset > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException("dataOffset");
		}
		if (length < 0)
		{
			throw new ArgumentOutOfRangeException("length");
		}
		if (bufferOffset < 0)
		{
			throw new ArgumentOutOfRangeException("bufferOffset");
		}
		if (bufferOffset > buffer.Length)
		{
			throw new ArgumentOutOfRangeException("bufferOffset");
		}
		if (checked(bufferOffset + length) > buffer.Length)
		{
			throw new ArgumentException("bufferOffset + length cannot exceed buffer.Length", "length");
		}
	}

	private static int ReadFixedLengthPayloadLength(ref ByteArrayReader reader, int expectedLength, ColumnType columnType)
	{
		int num = checked((int)reader.ReadLengthEncodedInteger());
		if (num != expectedLength)
		{
			throw new FormatException($"ColumnType {columnType} expected payload length {expectedLength} but got {num}.");
		}
		return num;
	}

	/// <summary>
	/// Reads a UDT payload length by parsing the self-describing struct/array header.
	/// Mirrors JDBC BinaryRowProtocol#getComplexStruct/getComplexArray behavior.
	/// </summary>
	private int ReadUdtPayloadLength(ref ByteArrayReader reader, ColumnDefinitionPayload columnDefinition)
	{
		int offset = reader.Offset;
		string typeName = columnDefinition.ComplexTypeName ?? columnDefinition.SchemaName;
		bool isArray = columnDefinition.ColumnType == ColumnType.OracleArray;
		Encoding stringEncoding = ResultSet.Session?.CharacterEncoding ?? Encoding.UTF8;
		ByteArrayReader reader2 = reader;
		OracleUdtTdsDecoder.ReadValue(ref reader2, typeName, isArray, stringEncoding);
		return reader2.Offset - offset;
	}
}
