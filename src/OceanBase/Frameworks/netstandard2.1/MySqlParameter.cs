using System;
using System.Buffers.Text;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase;

public sealed class MySqlParameter : DbParameter, IDbDataParameter, IDataParameter, ICloneable
{
	[CompilerGenerated]
	private string _003CSourceColumn_003Ek__BackingField;

	private DbType m_dbType;

	private MySqlDbType m_mySqlDbType;

	private string m_name;

	private ParameterDirection? m_direction;

	private object? m_value;

	public override DbType DbType
	{
		get
		{
			return m_dbType;
		}
		set
		{
			m_dbType = value;
			m_mySqlDbType = TypeMapper.Instance.GetMySqlDbTypeForDbType(value);
			HasSetDbType = true;
		}
	}

	[DbProviderSpecificTypeProperty(true)]
	public MySqlDbType MySqlDbType
	{
		get
		{
			return m_mySqlDbType;
		}
		set
		{
			m_dbType = TypeMapper.Instance.GetDbTypeForMySqlDbType(value);
			m_mySqlDbType = value;
			HasSetDbType = true;
		}
	}

	public override ParameterDirection Direction
	{
		get
		{
			return m_direction ?? ParameterDirection.Input;
		}
		set
		{
			if (((uint)(value - 1) > 2u && value != ParameterDirection.ReturnValue) || 1 == 0)
			{
				throw new ArgumentOutOfRangeException("value", $"{value} is not a supported value for ParameterDirection");
			}
			m_direction = value;
		}
	}

	public override bool IsNullable { get; set; }

	public override byte Precision { get; set; }

	public override byte Scale { get; set; }

	public override string ParameterName
	{
		get
		{
			return m_name;
		}
		[param: AllowNull]
		set
		{
			m_name = value ?? "";
			string text = ((value == null) ? "" : NormalizeParameterName(m_name));
			ParameterCollection?.ChangeParameterName(this, NormalizedParameterName, text);
			NormalizedParameterName = text;
		}
	}

	public override int Size { get; set; }

	public override string SourceColumn
	{
		[CompilerGenerated]
		get
		{
			return _003CSourceColumn_003Ek__BackingField;
		}
		[param: AllowNull]
		set
		{
			_003CSourceColumn_003Ek__BackingField = value ?? "";
		}
	}

	public override bool SourceColumnNullMapping { get; set; }

	public override DataRowVersion SourceVersion { get; set; }

	public override object? Value
	{
		get
		{
			return m_value;
		}
		set
		{
			m_value = value;
			if (!HasSetDbType && value != null)
			{
				DbTypeMapping dbTypeMapping = TypeMapper.Instance.GetDbTypeMapping(value.GetType());
				if (dbTypeMapping != null)
				{
					m_dbType = dbTypeMapping.DbTypes[0];
					m_mySqlDbType = TypeMapper.Instance.GetMySqlDbTypeForDbType(m_dbType);
				}
			}
		}
	}

	internal bool HasSetDirection => m_direction.HasValue;

	internal bool HasSetDbType { get; set; }

	internal string NormalizedParameterName { get; private set; }

	internal MySqlParameterCollection? ParameterCollection { get; set; }

	private static ReadOnlySpan<byte> BinaryBytes => "_binary'"u8;

	public MySqlParameter()
		: this((string?)null, (object?)null)
	{
	}

	public MySqlParameter(string? name, object? value)
	{
		ResetDbType();
		m_name = name ?? "";
		NormalizedParameterName = NormalizeParameterName(m_name);
		Value = value;
		SourceColumn = "";
		SourceVersion = DataRowVersion.Current;
	}

	public MySqlParameter(string name, MySqlDbType mySqlDbType)
		: this(name, mySqlDbType, 0)
	{
	}

	public MySqlParameter(string name, MySqlDbType mySqlDbType, int size)
		: this(name, mySqlDbType, size, "")
	{
	}

	public MySqlParameter(string name, MySqlDbType mySqlDbType, int size, string sourceColumn)
	{
		m_name = name ?? "";
		NormalizedParameterName = NormalizeParameterName(m_name);
		MySqlDbType = mySqlDbType;
		Size = size;
		SourceColumn = sourceColumn ?? "";
		SourceVersion = DataRowVersion.Current;
	}

	public MySqlParameter(string name, MySqlDbType mySqlDbType, int size, ParameterDirection direction, bool isNullable, byte precision, byte scale, string sourceColumn, DataRowVersion sourceVersion, object value)
		: this(name, mySqlDbType, size, sourceColumn)
	{
		Direction = direction;
		IsNullable = isNullable;
		Precision = precision;
		Scale = scale;
		SourceVersion = sourceVersion;
		Value = value;
	}

	public override void ResetDbType()
	{
		m_mySqlDbType = MySqlDbType.VarChar;
		m_dbType = DbType.String;
		HasSetDbType = false;
	}

	public MySqlParameter Clone()
	{
		return new MySqlParameter(this);
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	internal MySqlParameter WithParameterName(string parameterName)
	{
		return new MySqlParameter(this, parameterName);
	}

	private MySqlParameter(MySqlParameter other)
	{
		m_dbType = other.m_dbType;
		m_mySqlDbType = other.m_mySqlDbType;
		m_direction = other.m_direction;
		HasSetDbType = other.HasSetDbType;
		IsNullable = other.IsNullable;
		Size = other.Size;
		m_name = other.m_name;
		NormalizedParameterName = other.NormalizedParameterName;
		m_value = other.m_value;
		Precision = other.Precision;
		Scale = other.Scale;
		SourceColumn = other.SourceColumn;
		SourceColumnNullMapping = other.SourceColumnNullMapping;
		SourceVersion = other.SourceVersion;
	}

	private MySqlParameter(MySqlParameter other, string parameterName)
		: this(other)
	{
		ExceptionExtensions.ThrowIfNull(parameterName, "parameterName");
		ParameterName = parameterName;
	}

	internal void AppendSqlString(ByteBufferWriter writer, StatementPreparerOptions options)
	{
		bool flag = (options & StatementPreparerOptions.NoBackslashEscapes) == StatementPreparerOptions.NoBackslashEscapes;
		if (Value == null || Value == DBNull.Value)
		{
			writer.Write("NULL"u8);
			return;
		}
		if (Value is string text)
		{
			WriteString(writer, flag, text.AsSpan());
			return;
		}
		if (Value is ReadOnlyMemory<char> readOnlyMemory)
		{
			WriteString(writer, flag, readOnlyMemory.Span);
			return;
		}
		if (Value is Memory<char> memory)
		{
			WriteString(writer, flag, memory.Span);
			return;
		}
		if (Value is char c)
		{
			writer.Write((byte)39);
			char c2 = c;
			if (c2 != 0)
			{
				if (c2 != '\'')
				{
					if (c2 != '\\' || flag)
					{
						goto IL_0105;
					}
					writer.Write((ushort)23644);
				}
				else
				{
					writer.Write((ushort)10023);
				}
			}
			else
			{
				if (flag)
				{
					goto IL_0105;
				}
				writer.Write((ushort)12380);
			}
			goto IL_0112;
		}
		if (Value is byte value)
		{
			Utf8Formatter.TryFormat(value, writer.GetSpan(3), out var bytesWritten);
			writer.Advance(bytesWritten);
			return;
		}
		if (Value is sbyte value2)
		{
			Utf8Formatter.TryFormat(value2, writer.GetSpan(4), out var bytesWritten2);
			writer.Advance(bytesWritten2);
			return;
		}
		if (Value is decimal num)
		{
			writer.WriteAscii(num.ToString(CultureInfo.InvariantCulture));
			return;
		}
		if (Value is short value3)
		{
			writer.WriteString(value3);
			return;
		}
		if (Value is ushort value4)
		{
			writer.WriteString(value4);
			return;
		}
		if (Value is int value5)
		{
			writer.WriteString(value5);
			return;
		}
		if (Value is uint value6)
		{
			writer.WriteString(value6);
			return;
		}
		if (Value is long value7)
		{
			writer.WriteString(value7);
			return;
		}
		if (Value is ulong value8)
		{
			writer.WriteString(value8);
			return;
		}
		object value9 = Value;
		bool flag2;
		if ((value9 is byte[] || value9 is ReadOnlyMemory<byte> || value9 is Memory<byte> || value9 is ArraySegment<byte> || value9 is MySqlGeometry || value9 is MemoryStream || value9 is float[] || value9 is ReadOnlyMemory<float> || value9 is Memory<float>) ? true : false)
		{
			value9 = Value;
			ReadOnlySpan<byte> readOnlySpan = ((value9 is byte[] array) ? ((ReadOnlySpan<byte>)array.AsSpan()) : ((value9 is ArraySegment<byte> segment) ? ((ReadOnlySpan<byte>)segment.AsSpan()) : ((value9 is Memory<byte> memory2) ? ((ReadOnlySpan<byte>)memory2.Span) : ((value9 is MySqlGeometry mySqlGeometry) ? mySqlGeometry.ValueSpan : ((value9 is MemoryStream memoryStream) ? ((ReadOnlySpan<byte>)(memoryStream.TryGetBuffer(out var buffer) ? buffer.AsSpan() : memoryStream.ToArray().AsSpan())) : ((value9 is float[] array2) ? ConvertFloatsToBytes(array2.AsSpan()) : ((value9 is Memory<float> memory3) ? ConvertFloatsToBytes(memory3.Span) : ((!(value9 is ReadOnlyMemory<float> readOnlyMemory2)) ? ((ReadOnlyMemory<byte>)Value).Span : ConvertFloatsToBytes(readOnlyMemory2.Span)))))))));
			ReadOnlySpan<byte> readOnlySpan2 = readOnlySpan;
			int num2 = readOnlySpan2.Length + BinaryBytes.Length + 1;
			readOnlySpan = readOnlySpan2;
			for (int i = 0; i < readOnlySpan.Length; i++)
			{
				byte b = readOnlySpan[i];
				flag2 = b == 39;
				if (!flag2)
				{
					bool flag3 = ((b == 0 || b == 92) ? true : false);
					flag2 = flag3 && !flag;
				}
				if (flag2)
				{
					num2++;
				}
			}
			Span<byte> span = writer.GetSpan(num2);
			BinaryBytes.CopyTo(span);
			int length = BinaryBytes.Length;
			ReadOnlySpan<byte> readOnlySpan3 = readOnlySpan2;
			for (int i = 0; i < readOnlySpan3.Length; i++)
			{
				byte b2 = readOnlySpan3[i];
				if (b2 == 0 && !flag)
				{
					span[length++] = 92;
					span[length++] = 48;
					continue;
				}
				if (b2 == 39 || (b2 == 92 && !flag))
				{
					span[length++] = b2;
				}
				span[length++] = b2;
			}
			span[length++] = 39;
			writer.Advance(length);
			return;
		}
		if (Value is Stream)
		{
			throw new NotSupportedException("Parameter type " + Value.GetType().Name + " can only be used after calling MySqlCommand.Prepare.");
		}
		if (Value is bool flag4)
		{
			writer.Write(flag4 ? "true"u8 : "false"u8);
			return;
		}
		if (Value is float num3)
		{
			writer.WriteAscii(num3.ToString("R", CultureInfo.InvariantCulture));
			return;
		}
		if (Value is double num4)
		{
			writer.WriteAscii(num4.ToString("R", CultureInfo.InvariantCulture));
			return;
		}
		if (Value is BigInteger bigInteger)
		{
			writer.WriteAscii(bigInteger.ToString(CultureInfo.InvariantCulture));
			return;
		}
		if (Value is MySqlDecimal mySqlDecimal)
		{
			writer.WriteAscii(mySqlDecimal.ToString());
			return;
		}
		if (Value is MySqlDateTime mySqlDateTime)
		{
			if (mySqlDateTime.IsValidDateTime)
			{
				string value10 = FormattableString.Invariant($"timestamp('{mySqlDateTime.GetDateTime():yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff}')");
				writer.WriteAscii(value10);
			}
			else
			{
				writer.Write("timestamp('0000-00-00')"u8);
			}
			return;
		}
		if (Value is DateTime dateTime)
		{
			if ((options & StatementPreparerOptions.DateTimeUtc) != StatementPreparerOptions.None && dateTime.Kind == DateTimeKind.Local)
			{
				throw new MySqlException("DateTime.Kind must not be Local when DateTimeKind setting is Utc (parameter name: " + ParameterName + ")");
			}
			if ((options & StatementPreparerOptions.DateTimeLocal) != StatementPreparerOptions.None && dateTime.Kind == DateTimeKind.Utc)
			{
				throw new MySqlException("DateTime.Kind must not be Utc when DateTimeKind setting is Local (parameter name: " + ParameterName + ")");
			}
			string value11 = FormattableString.Invariant($"timestamp('{dateTime:yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff}')");
			writer.WriteAscii(value11);
			return;
		}
		if (Value is DateTimeOffset dateTimeOffset)
		{
			string value12 = FormattableString.Invariant($"timestamp('{dateTimeOffset.UtcDateTime:yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff}')");
			writer.WriteAscii(value12);
			return;
		}
		if (Value is TimeSpan timeSpan)
		{
			writer.Write("time '"u8);
			if (timeSpan.Ticks < 0)
			{
				writer.Write((byte)45);
				timeSpan = TimeSpan.FromTicks(-timeSpan.Ticks);
			}
			string value13 = FormattableString.Invariant($"{timeSpan.Days * 24 + timeSpan.Hours}:{timeSpan:mm':'ss'.'ffffff}'");
			writer.WriteAscii(value13);
			return;
		}
		if (Value is Guid value14)
		{
			StatementPreparerOptions statementPreparerOptions = options & StatementPreparerOptions.GuidFormatMask;
			if ((statementPreparerOptions == StatementPreparerOptions.GuidFormatBinary16 || statementPreparerOptions == StatementPreparerOptions.GuidFormatTimeSwapBinary16 || statementPreparerOptions == StatementPreparerOptions.GuidFormatLittleEndianBinary16) ? true : false)
			{
				Span<byte> span2 = stackalloc byte[16];
				value14.TryWriteBytes(span2);
				if (statementPreparerOptions != StatementPreparerOptions.GuidFormatLittleEndianBinary16)
				{
					Utility.SwapBytes(span2, 0, 3);
					Utility.SwapBytes(span2, 1, 2);
					Utility.SwapBytes(span2, 4, 5);
					Utility.SwapBytes(span2, 6, 7);
					if (statementPreparerOptions == StatementPreparerOptions.GuidFormatTimeSwapBinary16)
					{
						Utility.SwapBytes(span2, 0, 4);
						Utility.SwapBytes(span2, 1, 5);
						Utility.SwapBytes(span2, 2, 6);
						Utility.SwapBytes(span2, 3, 7);
						Utility.SwapBytes(span2, 0, 2);
						Utility.SwapBytes(span2, 1, 3);
					}
				}
				writer.Write(BinaryBytes);
				Span<byte> span3 = span2;
				for (int i = 0; i < span3.Length; i++)
				{
					byte b3 = span3[i];
					if ((b3 == 0 || b3 == 39 || b3 == 92) ? true : false)
					{
						writer.Write((byte)92);
					}
					writer.Write((byte)((b3 == 0) ? 48 : b3));
				}
				writer.Write((byte)39);
			}
			else
			{
				bool flag5 = statementPreparerOptions == StatementPreparerOptions.GuidFormatChar32;
				int num5 = (flag5 ? 34 : 38);
				Span<byte> span4 = writer.GetSpan(num5);
				span4[0] = 39;
				Utf8Formatter.TryFormat(value14, span4.Slice(1, span4.Length - 1), out var _, flag5 ? 'N' : 'D');
				span4[num5 - 1] = 39;
				writer.Advance(num5);
			}
			return;
		}
		if (Value is StringBuilder stringBuilder)
		{
			WriteString(writer, flag, stringBuilder.ToString().AsSpan());
			return;
		}
		MySqlDbType mySqlDbType = MySqlDbType;
		flag2 = (uint)(mySqlDbType - 253) <= 1u;
		if (flag2 && HasSetDbType && Value is Enum obj)
		{
			writer.Write((byte)39);
			writer.Write(obj.ToString("G"));
			writer.Write((byte)39);
			return;
		}
		if (Value is Enum obj2)
		{
			writer.Write(obj2.ToString("d"));
			return;
		}
		if (MySqlDbType == MySqlDbType.Int16)
		{
			writer.WriteString((short)Value);
			return;
		}
		if (MySqlDbType == MySqlDbType.UInt16)
		{
			writer.WriteString((ushort)Value);
			return;
		}
		if (MySqlDbType == MySqlDbType.Int32)
		{
			writer.WriteString((int)Value);
			return;
		}
		if (MySqlDbType == MySqlDbType.UInt32)
		{
			writer.WriteString((uint)Value);
			return;
		}
		if (MySqlDbType == MySqlDbType.Int64)
		{
			writer.WriteString((long)Value);
			return;
		}
		if (MySqlDbType == MySqlDbType.UInt64)
		{
			writer.WriteString((ulong)Value);
			return;
		}
		throw new NotSupportedException($"Parameter type {Value.GetType().Name} is not supported; see https://mysqlconnector.net/param-type. Value: {Value}");
		IL_0105:
		writer.Write(c.ToString());
		goto IL_0112;
		IL_0112:
		writer.Write((byte)39);
		static void WriteString(ByteBufferWriter byteBufferWriter, bool noBackslashEscapes, ReadOnlySpan<char> readOnlySpan4)
		{
			byteBufferWriter.Write((byte)39);
			int num6 = 0;
			while (num6 < readOnlySpan4.Length)
			{
				int num7 = num6;
				ReadOnlySpan<char> readOnlySpan5 = readOnlySpan4.Slice(num7, readOnlySpan4.Length - num7);
				int num8 = readOnlySpan5.IndexOfAny('\0', '\'', '\\');
				if (num8 == -1)
				{
					byteBufferWriter.Write(readOnlySpan5);
					num6 += readOnlySpan5.Length;
				}
				else
				{
					byteBufferWriter.Write(readOnlySpan5.Slice(0, num8));
					if (readOnlySpan5[num8] == '\\' && !noBackslashEscapes)
					{
						byteBufferWriter.Write((ushort)23644);
					}
					else if (readOnlySpan5[num8] == '\\' && noBackslashEscapes)
					{
						byteBufferWriter.Write((byte)92);
					}
					else if (readOnlySpan5[num8] == '\'')
					{
						byteBufferWriter.Write((ushort)10023);
					}
					else if (readOnlySpan5[num8] == '\0' && !noBackslashEscapes)
					{
						byteBufferWriter.Write((ushort)12380);
					}
					else if (readOnlySpan5[num8] == '\0' && noBackslashEscapes)
					{
						byteBufferWriter.Write((byte)0);
					}
					num6 += num8 + 1;
				}
			}
			byteBufferWriter.Write((byte)39);
		}
	}

	internal void AppendBinary(ByteBufferWriter writer, StatementPreparerOptions options)
	{
		if (Value != null && Value != DBNull.Value)
		{
			AppendBinary(writer, Value, options);
		}
	}

	private void AppendBinary(ByteBufferWriter writer, object value, StatementPreparerOptions options)
	{
		if (value is string value2)
		{
			writer.WriteLengthEncodedString(value2);
		}
		else if (value is char c)
		{
			writer.WriteLengthEncodedString(c.ToString());
		}
		else if (value is sbyte b)
		{
			writer.Write((byte)b);
		}
		else if (value is byte value3)
		{
			writer.Write(value3);
		}
		else if (value is bool flag)
		{
			writer.Write(flag ? ((byte)1) : ((byte)0));
		}
		else if (value is short num)
		{
			writer.Write((ushort)num);
		}
		else if (value is ushort value4)
		{
			writer.Write(value4);
		}
		else if (value is int value5)
		{
			writer.Write(value5);
		}
		else if (value is uint value6)
		{
			writer.Write(value6);
		}
		else if (value is long value7)
		{
			writer.Write((ulong)value7);
		}
		else if (value is ulong value8)
		{
			writer.Write(value8);
		}
		else if (value is byte[] array)
		{
			writer.WriteLengthEncodedInteger((ulong)array.Length);
			writer.Write(array);
		}
		else if (value is ReadOnlyMemory<byte> readOnlyMemory)
		{
			writer.WriteLengthEncodedInteger((ulong)readOnlyMemory.Length);
			writer.Write(readOnlyMemory.Span);
		}
		else if (value is Memory<byte> memory)
		{
			writer.WriteLengthEncodedInteger((ulong)memory.Length);
			writer.Write(memory.Span);
		}
		else if (value is ArraySegment<byte> arraySegment)
		{
			writer.WriteLengthEncodedInteger((ulong)arraySegment.Count);
			writer.Write(arraySegment);
		}
		else if (value is MySqlGeometry mySqlGeometry)
		{
			writer.WriteLengthEncodedInteger((ulong)mySqlGeometry.ValueSpan.Length);
			writer.Write(mySqlGeometry.ValueSpan);
		}
		else if (value is MemoryStream memoryStream)
		{
			if (!memoryStream.TryGetBuffer(out var buffer))
			{
				buffer = new ArraySegment<byte>(memoryStream.ToArray());
			}
			writer.WriteLengthEncodedInteger((ulong)buffer.Count);
			writer.Write(buffer);
		}
		else
		{
			if (value is Stream)
			{
				return;
			}
			if (value is float value9)
			{
				byte[] bytes = BitConverter.GetBytes(value9);
				if (!BitConverter.IsLittleEndian)
				{
					Array.Reverse(bytes);
				}
				writer.Write(bytes);
				return;
			}
			if (value is double value10)
			{
				if (BitConverter.IsLittleEndian)
				{
					writer.Write((ulong)BitConverter.DoubleToInt64Bits(value10));
					return;
				}
				byte[] bytes2 = BitConverter.GetBytes(value10);
				Array.Reverse(bytes2);
				writer.Write(bytes2);
				return;
			}
			if (value is float[] array2)
			{
				writer.WriteLengthEncodedInteger((ulong)array2.Length * 4uL);
				writer.Write(ConvertFloatsToBytes(array2.AsSpan()));
				return;
			}
			if (value is Memory<float> memory2)
			{
				writer.WriteLengthEncodedInteger((ulong)memory2.Length * 4uL);
				writer.Write(ConvertFloatsToBytes(memory2.Span));
				return;
			}
			if (value is ReadOnlyMemory<float> readOnlyMemory2)
			{
				writer.WriteLengthEncodedInteger((ulong)readOnlyMemory2.Length * 4uL);
				writer.Write(ConvertFloatsToBytes(readOnlyMemory2.Span));
				return;
			}
			if (value is decimal num2)
			{
				writer.WriteLengthEncodedAsciiString(num2.ToString(CultureInfo.InvariantCulture));
				return;
			}
			if (value is BigInteger bigInteger)
			{
				writer.WriteLengthEncodedAsciiString(bigInteger.ToString(CultureInfo.InvariantCulture));
				return;
			}
			if (value is MySqlDateTime mySqlDateTime)
			{
				if (mySqlDateTime.IsValidDateTime)
				{
					WriteDateTime(writer, mySqlDateTime.GetDateTime());
				}
				else
				{
					writer.Write((byte)0);
				}
				return;
			}
			if (value is MySqlDecimal mySqlDecimal)
			{
				writer.WriteLengthEncodedAsciiString(mySqlDecimal.ToString());
				return;
			}
			if (value is DateTime dateTime)
			{
				if ((options & StatementPreparerOptions.DateTimeUtc) != StatementPreparerOptions.None && dateTime.Kind == DateTimeKind.Local)
				{
					throw new MySqlException("DateTime.Kind must not be Local when DateTimeKind setting is Utc (parameter name: " + ParameterName + ")");
				}
				if ((options & StatementPreparerOptions.DateTimeLocal) != StatementPreparerOptions.None && dateTime.Kind == DateTimeKind.Utc)
				{
					throw new MySqlException("DateTime.Kind must not be Utc when DateTimeKind setting is Local (parameter name: " + ParameterName + ")");
				}
				WriteDateTime(writer, dateTime);
				return;
			}
			if (value is DateTimeOffset dateTimeOffset)
			{
				WriteDateTime(writer, dateTimeOffset.UtcDateTime);
				return;
			}
			if (value is TimeSpan timeSpan)
			{
				WriteTime(writer, timeSpan);
				return;
			}
			if (value is Guid value11)
			{
				StatementPreparerOptions statementPreparerOptions = options & StatementPreparerOptions.GuidFormatMask;
				if ((statementPreparerOptions == StatementPreparerOptions.GuidFormatBinary16 || statementPreparerOptions == StatementPreparerOptions.GuidFormatTimeSwapBinary16 || statementPreparerOptions == StatementPreparerOptions.GuidFormatLittleEndianBinary16) ? true : false)
				{
					Span<byte> span = stackalloc byte[16];
					value11.TryWriteBytes(span);
					if (statementPreparerOptions != StatementPreparerOptions.GuidFormatLittleEndianBinary16)
					{
						Utility.SwapBytes(span, 0, 3);
						Utility.SwapBytes(span, 1, 2);
						Utility.SwapBytes(span, 4, 5);
						Utility.SwapBytes(span, 6, 7);
						if (statementPreparerOptions == StatementPreparerOptions.GuidFormatTimeSwapBinary16)
						{
							Utility.SwapBytes(span, 0, 4);
							Utility.SwapBytes(span, 1, 5);
							Utility.SwapBytes(span, 2, 6);
							Utility.SwapBytes(span, 3, 7);
							Utility.SwapBytes(span, 0, 2);
							Utility.SwapBytes(span, 1, 3);
						}
					}
					writer.Write((byte)16);
					writer.Write(span);
				}
				else
				{
					bool flag2 = statementPreparerOptions == StatementPreparerOptions.GuidFormatChar32;
					int num3 = (flag2 ? 32 : 36);
					writer.Write((byte)num3);
					Span<byte> span2 = writer.GetSpan(num3);
					Utf8Formatter.TryFormat(value11, span2, out var _, flag2 ? 'N' : 'D');
					writer.Advance(num3);
				}
				return;
			}
			if (value is ReadOnlyMemory<char> readOnlyMemory3)
			{
				writer.WriteLengthEncodedString(readOnlyMemory3.Span);
				return;
			}
			if (value is Memory<char> memory3)
			{
				writer.WriteLengthEncodedString(memory3.Span);
				return;
			}
			if (value is StringBuilder stringBuilder)
			{
				writer.WriteLengthEncodedString(stringBuilder);
				return;
			}
			MySqlDbType mySqlDbType = MySqlDbType;
			bool flag3 = (uint)(mySqlDbType - 253) <= 1u;
			if (flag3 && HasSetDbType && value is Enum obj)
			{
				writer.WriteLengthEncodedString(obj.ToString("G"));
				return;
			}
			if (value is Enum)
			{
				object value12 = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture);
				AppendBinary(writer, value12, options);
				return;
			}
			if (MySqlDbType == MySqlDbType.Int16)
			{
				writer.Write((ushort)(short)value);
				return;
			}
			if (MySqlDbType == MySqlDbType.UInt16)
			{
				writer.Write((ushort)value);
				return;
			}
			if (MySqlDbType == MySqlDbType.Int32)
			{
				writer.Write((int)value);
				return;
			}
			if (MySqlDbType == MySqlDbType.UInt32)
			{
				writer.Write((uint)value);
				return;
			}
			if (MySqlDbType == MySqlDbType.Int64)
			{
				writer.Write((ulong)(long)value);
				return;
			}
			if (MySqlDbType != MySqlDbType.UInt64)
			{
				throw new NotSupportedException($"Parameter type {value.GetType().Name} is not supported; see https://mysqlconnector.net/param-type. Value: {value}");
			}
			writer.Write((ulong)value);
		}
	}

	internal static string NormalizeParameterName(string name)
	{
		string text = name.Trim();
		int length;
		if (text != null)
		{
			length = text.Length;
			if (length < 3)
			{
				if (length >= 1)
				{
					char c = text[0];
					if (c == '?' || c == '@')
					{
						goto IL_00c9;
					}
				}
			}
			else
			{
				char c = text[0];
				if (c == '?' || c == '@')
				{
					switch (text[1])
					{
					case '`':
					{
						string text2 = text.Substring(2, length - 1 - 2);
						if (text[length - 1] != '`')
						{
							break;
						}
						return text2.Replace("``", "`");
					}
					case '\'':
					{
						string text2 = text.Substring(2, length - 1 - 2);
						if (text[length - 1] != '\'')
						{
							break;
						}
						return text2.Replace("''", "'");
					}
					case '"':
					{
						string text2 = text.Substring(2, length - 1 - 2);
						if (text[length - 1] != '"')
						{
							break;
						}
						return text2.Replace("\"\"", "\"");
					}
					}
					goto IL_00c9;
				}
			}
			return text;
		}
		global::_003CPrivateImplementationDetails_003E.ThrowSwitchExpressionException(text);
		string result = default(string);
		return result;
		IL_00c9:
		return text.Substring(1, length - 1);
	}

	private static void WriteDateTime(ByteBufferWriter writer, DateTime dateTime)
	{
		int num = (int)(dateTime.Ticks % 10000000) / 10;
		byte b = (byte)((num != 0) ? 11 : ((dateTime.Hour == 0 && dateTime.Minute == 0 && dateTime.Second == 0) ? 4 : 7));
		writer.Write(b);
		writer.Write((ushort)dateTime.Year);
		writer.Write((byte)dateTime.Month);
		writer.Write((byte)dateTime.Day);
		if (b > 4)
		{
			writer.Write((byte)dateTime.Hour);
			writer.Write((byte)dateTime.Minute);
			writer.Write((byte)dateTime.Second);
			if (b > 7)
			{
				writer.Write(num);
			}
		}
	}

	private static void WriteTime(ByteBufferWriter writer, TimeSpan timeSpan)
	{
		long ticks = timeSpan.Ticks;
		if (ticks == 0L)
		{
			writer.Write((byte)0);
			return;
		}
		if (ticks < 0)
		{
			timeSpan = TimeSpan.FromTicks(-ticks);
		}
		int num = (int)(timeSpan.Ticks % 10000000) / 10;
		writer.Write((byte)((num == 0) ? 8u : 12u));
		writer.Write((ticks < 0) ? ((byte)1) : ((byte)0));
		writer.Write(timeSpan.Days);
		writer.Write((byte)timeSpan.Hours);
		writer.Write((byte)timeSpan.Minutes);
		writer.Write((byte)timeSpan.Seconds);
		if (num != 0)
		{
			writer.Write(num);
		}
	}

	internal static ReadOnlySpan<byte> ConvertFloatsToBytes(ReadOnlySpan<float> floats)
	{
		if (BitConverter.IsLittleEndian)
		{
			return MemoryMarshal.AsBytes(floats);
		}
		byte[] array = new byte[floats.Length * 4];
		for (int i = 0; i < floats.Length; i++)
		{
			byte[] bytes = BitConverter.GetBytes(floats[i]);
			Array.Reverse(bytes);
			bytes.CopyTo(array, i * 4);
		}
		return array;
	}
}
