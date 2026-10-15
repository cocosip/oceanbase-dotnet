using System;
using System.Buffers.Binary;
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
using System.Text.Unicode;
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
		ArgumentNullException.ThrowIfNull(parameterName, "parameterName");
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
			num.TryFormat(writer.GetSpan(31), out var bytesWritten3, default(ReadOnlySpan<char>), CultureInfo.InvariantCulture);
			writer.Advance(bytesWritten3);
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
		bool shouldAppend = ((value9 is byte[] || value9 is ReadOnlyMemory<byte> || value9 is Memory<byte> || value9 is ArraySegment<byte> || value9 is MySqlGeometry || value9 is MemoryStream || value9 is float[] || value9 is ReadOnlyMemory<float> || value9 is Memory<float>) ? true : false);
		bool shouldAppend2;
		if (shouldAppend)
		{
			value9 = Value;
			ReadOnlySpan<byte> readOnlySpan = ((value9 is byte[] array) ? ((ReadOnlySpan<byte>)array.AsSpan()) : ((value9 is ArraySegment<byte> segment) ? ((ReadOnlySpan<byte>)segment.AsSpan()) : ((value9 is Memory<byte> memory2) ? ((ReadOnlySpan<byte>)memory2.Span) : ((value9 is MySqlGeometry mySqlGeometry) ? mySqlGeometry.ValueSpan : ((value9 is MemoryStream memoryStream) ? ((ReadOnlySpan<byte>)(memoryStream.TryGetBuffer(out var buffer) ? buffer.AsSpan() : memoryStream.ToArray().AsSpan())) : ((value9 is float[] array2) ? ConvertFloatsToBytes(array2.AsSpan()) : ((value9 is Memory<float> memory3) ? ConvertFloatsToBytes(memory3.Span) : ((!(value9 is ReadOnlyMemory<float> readOnlyMemory2)) ? ((ReadOnlyMemory<byte>)Value).Span : ConvertFloatsToBytes(readOnlyMemory2.Span)))))))));
			ReadOnlySpan<byte> readOnlySpan2 = readOnlySpan;
			int num2 = readOnlySpan2.Length + BinaryBytes.Length + 1;
			readOnlySpan = readOnlySpan2;
			for (int i = 0; i < readOnlySpan.Length; i++)
			{
				byte b = readOnlySpan[i];
				shouldAppend = b == 39;
				if (!shouldAppend)
				{
					shouldAppend2 = ((b == 0 || b == 92) ? true : false);
					shouldAppend = shouldAppend2 && !flag;
				}
				if (shouldAppend)
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
		if (Value is bool flag2)
		{
			writer.Write(flag2 ? "true"u8 : "false"u8);
			return;
		}
		if (Value is float num3)
		{
			num3.TryFormat(writer.GetSpan(14), out var bytesWritten4, "R".AsSpan(), CultureInfo.InvariantCulture);
			writer.Advance(bytesWritten4);
			return;
		}
		if (Value is double num4)
		{
			num4.TryFormat(writer.GetSpan(24), out var bytesWritten5, "R".AsSpan(), CultureInfo.InvariantCulture);
			writer.Advance(bytesWritten5);
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
				Span<byte> span2 = writer.GetSpan(39);
				Span<byte> destination = span2;
				IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
				IFormatProvider provider = invariantCulture;
				Utf8.TryWriteInterpolatedStringHandler handler = new Utf8.TryWriteInterpolatedStringHandler(13, 1, span2, invariantCulture, out shouldAppend);
				if (shouldAppend && handler.AppendLiteral("timestamp('") && handler.AppendFormatted(mySqlDateTime.GetDateTime(), "yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff"))
				{
					handler.AppendLiteral("')");
				}
				else
					_ = 0;
				Utf8.TryWrite(destination, provider, ref handler, out var bytesWritten6);
				writer.Advance(bytesWritten6);
			}
			else
			{
				writer.Write("timestamp('0000-00-00')"u8);
			}
			return;
		}
		if (Value is DateOnly value10)
		{
			Span<byte> span2 = writer.GetSpan(23);
			Span<byte> destination2 = span2;
			IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
			IFormatProvider provider2 = invariantCulture;
			Utf8.TryWriteInterpolatedStringHandler handler2 = new Utf8.TryWriteInterpolatedStringHandler(13, 1, span2, invariantCulture, out shouldAppend2);
			if (shouldAppend2 && handler2.AppendLiteral("timestamp('") && handler2.AppendFormatted(value10, "yyyy'-'MM'-'dd"))
			{
				handler2.AppendLiteral("')");
			}
			else
				_ = 0;
			Utf8.TryWrite(destination2, provider2, ref handler2, out var bytesWritten7);
			writer.Advance(bytesWritten7);
			return;
		}
		if (Value is DateTime value11)
		{
			if ((options & StatementPreparerOptions.DateTimeUtc) != StatementPreparerOptions.None && value11.Kind == DateTimeKind.Local)
			{
				throw new MySqlException("DateTime.Kind must not be Local when DateTimeKind setting is Utc (parameter name: " + ParameterName + ")");
			}
			if ((options & StatementPreparerOptions.DateTimeLocal) != StatementPreparerOptions.None && value11.Kind == DateTimeKind.Utc)
			{
				throw new MySqlException("DateTime.Kind must not be Utc when DateTimeKind setting is Local (parameter name: " + ParameterName + ")");
			}
			Span<byte> span2 = writer.GetSpan(39);
			Span<byte> destination3 = span2;
			IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
			IFormatProvider provider3 = invariantCulture;
			bool shouldAppend3;
			Utf8.TryWriteInterpolatedStringHandler handler3 = new Utf8.TryWriteInterpolatedStringHandler(13, 1, span2, invariantCulture, out shouldAppend3);
			if (shouldAppend3 && handler3.AppendLiteral("timestamp('") && handler3.AppendFormatted(value11, "yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff"))
			{
				handler3.AppendLiteral("')");
			}
			else
				_ = 0;
			Utf8.TryWrite(destination3, provider3, ref handler3, out var bytesWritten8);
			writer.Advance(bytesWritten8);
			return;
		}
		if (Value is DateTimeOffset dateTimeOffset)
		{
			Span<byte> span2 = writer.GetSpan(39);
			Span<byte> destination4 = span2;
			IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
			IFormatProvider provider4 = invariantCulture;
			bool shouldAppend4;
			Utf8.TryWriteInterpolatedStringHandler handler4 = new Utf8.TryWriteInterpolatedStringHandler(13, 1, span2, invariantCulture, out shouldAppend4);
			if (shouldAppend4 && handler4.AppendLiteral("timestamp('") && handler4.AppendFormatted(dateTimeOffset.UtcDateTime, "yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff"))
			{
				handler4.AppendLiteral("')");
			}
			else
				_ = 0;
			Utf8.TryWrite(destination4, provider4, ref handler4, out var bytesWritten9);
			writer.Advance(bytesWritten9);
			return;
		}
		if (Value is TimeOnly value12)
		{
			Span<byte> span2 = writer.GetSpan(22);
			Span<byte> destination5 = span2;
			IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
			IFormatProvider provider5 = invariantCulture;
			bool shouldAppend5;
			Utf8.TryWriteInterpolatedStringHandler handler5 = new Utf8.TryWriteInterpolatedStringHandler(7, 1, span2, invariantCulture, out shouldAppend5);
			if (shouldAppend5 && handler5.AppendLiteral("time '") && handler5.AppendFormatted(value12, "HH':'mm':'ss'.'ffffff"))
			{
				handler5.AppendLiteral("'");
			}
			else
				_ = 0;
			Utf8.TryWrite(destination5, provider5, ref handler5, out var bytesWritten10);
			writer.Advance(bytesWritten10);
			return;
		}
		if (Value is TimeSpan value13)
		{
			writer.Write("time '"u8);
			if (value13.Ticks < 0)
			{
				writer.Write((byte)45);
				value13 = TimeSpan.FromTicks(-value13.Ticks);
			}
			Span<byte> span2 = writer.GetSpan(17);
			Span<byte> destination6 = span2;
			IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
			IFormatProvider provider6 = invariantCulture;
			bool shouldAppend6;
			Utf8.TryWriteInterpolatedStringHandler handler6 = new Utf8.TryWriteInterpolatedStringHandler(2, 2, span2, invariantCulture, out shouldAppend6);
			if (shouldAppend6 && handler6.AppendFormatted(value13.Days * 24 + value13.Hours) && handler6.AppendLiteral(":") && handler6.AppendFormatted(value13, "mm':'ss'.'ffffff"))
			{
				handler6.AppendLiteral("'");
			}
			else
				_ = 0;
			Utf8.TryWrite(destination6, provider6, ref handler6, out var bytesWritten11);
			writer.Advance(bytesWritten11);
			return;
		}
		if (Value is Guid value14)
		{
			StatementPreparerOptions statementPreparerOptions = options & StatementPreparerOptions.GuidFormatMask;
			int i;
			if ((statementPreparerOptions == StatementPreparerOptions.GuidFormatBinary16 || statementPreparerOptions == StatementPreparerOptions.GuidFormatTimeSwapBinary16 || statementPreparerOptions == StatementPreparerOptions.GuidFormatLittleEndianBinary16) ? true : false)
			{
				Span<byte> span3 = stackalloc byte[16];
				value14.TryWriteBytes(span3, statementPreparerOptions != StatementPreparerOptions.GuidFormatLittleEndianBinary16, out i);
				if (statementPreparerOptions != StatementPreparerOptions.GuidFormatLittleEndianBinary16 && statementPreparerOptions == StatementPreparerOptions.GuidFormatTimeSwapBinary16)
				{
					Utility.SwapBytes(span3, 0, 4);
					Utility.SwapBytes(span3, 1, 5);
					Utility.SwapBytes(span3, 2, 6);
					Utility.SwapBytes(span3, 3, 7);
					Utility.SwapBytes(span3, 0, 2);
					Utility.SwapBytes(span3, 1, 3);
				}
				writer.Write(BinaryBytes);
				Span<byte> span2 = span3;
				for (i = 0; i < span2.Length; i++)
				{
					byte b3 = span2[i];
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
				bool flag3 = statementPreparerOptions == StatementPreparerOptions.GuidFormatChar32;
				int num5 = (flag3 ? 34 : 38);
				Span<byte> span4 = writer.GetSpan(num5);
				span4[0] = 39;
				Utf8Formatter.TryFormat(value14, span4.Slice(1, span4.Length - 1), out i, flag3 ? 'N' : 'D');
				span4[num5 - 1] = 39;
				writer.Advance(num5);
			}
			return;
		}
		if (Value is StringBuilder stringBuilder)
		{
			writer.Write((byte)39);
			StringBuilder.ChunkEnumerator enumerator = stringBuilder.GetChunks().GetEnumerator();
			while (enumerator.MoveNext())
			{
				WriteStringChunk(writer, flag, enumerator.Current.Span);
			}
			if (stringBuilder.Length != 0)
			{
				writer.Write("".AsSpan(), flush: true);
			}
			writer.Write((byte)39);
			return;
		}
		MySqlDbType mySqlDbType = MySqlDbType;
		bool flag4 = (uint)(mySqlDbType - 253) <= 1u;
		if (flag4 && HasSetDbType && Value is Enum obj)
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
		static void WriteStringChunk(ByteBufferWriter byteBufferWriter, bool noBackslashEscapes, ReadOnlySpan<char> readOnlySpan4)
		{
			int num6 = 0;
			while (num6 < readOnlySpan4.Length)
			{
				int num7 = num6;
				ReadOnlySpan<char> readOnlySpan5 = readOnlySpan4.Slice(num7, readOnlySpan4.Length - num7);
				int num8 = readOnlySpan5.IndexOfAny('\0', '\'', '\\');
				if (num8 == -1)
				{
					byteBufferWriter.Write(readOnlySpan5, flush: false);
					num6 += readOnlySpan5.Length;
				}
				else
				{
					byteBufferWriter.Write(readOnlySpan5.Slice(0, num8), flush: true);
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
				Span<byte> span = stackalloc byte[4];
				BinaryPrimitives.WriteSingleLittleEndian(span, value9);
				writer.Write(span);
				return;
			}
			if (value is double value10)
			{
				Span<byte> span2 = stackalloc byte[8];
				BinaryPrimitives.WriteDoubleLittleEndian(span2, value10);
				writer.Write(span2);
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
			if (value is DateOnly dateOnly)
			{
				WriteDateOnly(writer, dateOnly);
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
			if (value is TimeOnly timeOnly)
			{
				WriteTime(writer, timeOnly.ToTimeSpan());
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
				int bytesWritten;
				if ((statementPreparerOptions == StatementPreparerOptions.GuidFormatBinary16 || statementPreparerOptions == StatementPreparerOptions.GuidFormatTimeSwapBinary16 || statementPreparerOptions == StatementPreparerOptions.GuidFormatLittleEndianBinary16) ? true : false)
				{
					Span<byte> span3 = stackalloc byte[16];
					value11.TryWriteBytes(span3, statementPreparerOptions != StatementPreparerOptions.GuidFormatLittleEndianBinary16, out bytesWritten);
					if (statementPreparerOptions != StatementPreparerOptions.GuidFormatLittleEndianBinary16 && statementPreparerOptions == StatementPreparerOptions.GuidFormatTimeSwapBinary16)
					{
						Utility.SwapBytes(span3, 0, 4);
						Utility.SwapBytes(span3, 1, 5);
						Utility.SwapBytes(span3, 2, 6);
						Utility.SwapBytes(span3, 3, 7);
						Utility.SwapBytes(span3, 0, 2);
						Utility.SwapBytes(span3, 1, 3);
					}
					writer.Write((byte)16);
					writer.Write(span3);
				}
				else
				{
					bool flag2 = statementPreparerOptions == StatementPreparerOptions.GuidFormatChar32;
					int num3 = (flag2 ? 32 : 36);
					writer.Write((byte)num3);
					Span<byte> span4 = writer.GetSpan(num3);
					Utf8Formatter.TryFormat(value11, span4, out bytesWritten, flag2 ? 'N' : 'D');
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

	private static void WriteDateOnly(ByteBufferWriter writer, DateOnly dateOnly)
	{
		writer.Write((byte)4);
		writer.Write((ushort)dateOnly.Year);
		writer.Write((byte)dateOnly.Month);
		writer.Write((byte)dateOnly.Day);
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
			BinaryPrimitives.WriteSingleLittleEndian(array.AsSpan(i * 4), floats[i]);
		}
		return array;
	}
}
