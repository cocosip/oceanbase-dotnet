using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol;
using OceanBase.Protocol.Serialization;

namespace OceanBase;

public sealed class OracleParameter : DbParameter, IDbDataParameter, IDataParameter, ICloneable
{
	private DbType m_dbType;

	private OracleDbType m_oracleDbType;

	private string m_name;

	private ParameterDirection? m_direction;

	private object? m_value;

	private string m_sourceColumn = "";

	public override DbType DbType
	{
		get
		{
			return m_dbType;
		}
		set
		{
			m_dbType = value;
			m_oracleDbType = TypeMapper.GetOracleDbTypeForDbType(value);
			HasSetDbType = true;
		}
	}

	[DbProviderSpecificTypeProperty(true)]
	public OracleDbType OracleDbType
	{
		get
		{
			return m_oracleDbType;
		}
		set
		{
			m_dbType = TypeMapper.GetDbTypeForOracleDbType(value);
			m_oracleDbType = value;
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

	/// <summary>
	/// Gets or sets per-element sizes for Oracle-style ArrayBinding variable-length parameters.
	/// When set, the length must match <see cref="P:OceanBase.OracleCommand.ArrayBindCount" />.
	/// </summary>
	public int[]? ArrayBindSize { get; set; }

	public string? UdtTypeName { get; set; }

	public override string SourceColumn
	{
		get
		{
			return m_sourceColumn;
		}
		[param: AllowNull]
		set
		{
			m_sourceColumn = value ?? "";
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
			if (HasSetDbType || value == null)
			{
				return;
			}
			if (value is TimeSpan)
			{
				m_dbType = DbType.Object;
				m_oracleDbType = OracleDbType.IntervalDS;
				return;
			}
			if (value is OracleIntervalDS)
			{
				m_dbType = DbType.Object;
				m_oracleDbType = OracleDbType.IntervalDS;
				return;
			}
			if (value is OracleIntervalYM)
			{
				m_dbType = DbType.Int64;
				m_oracleDbType = OracleDbType.IntervalYM;
				return;
			}
			if (value is IOracleCustomType)
			{
				m_dbType = DbType.Object;
				m_oracleDbType = OracleDbType.Object;
				return;
			}
			DbTypeMapping dbTypeMapping = TypeMapper.Instance.GetDbTypeMapping(value.GetType());
			if (dbTypeMapping != null)
			{
				m_dbType = dbTypeMapping.DbTypes[0];
				m_oracleDbType = TypeMapper.GetOracleDbTypeForDbType(m_dbType);
			}
		}
	}

	internal bool HasSetDirection => m_direction.HasValue;

	internal bool HasSetDbType { get; set; }

	internal bool ForceSerializeByOracleDbType { get; set; }

	internal string NormalizedParameterName { get; private set; }

	internal OracleParameterCollection? ParameterCollection { get; set; }

	public OracleParameter()
		: this((string?)null, (object?)null)
	{
	}

	public OracleParameter(string? name, object? value)
	{
		ResetDbType();
		m_name = name ?? "";
		NormalizedParameterName = NormalizeParameterName(m_name);
		Value = value;
		SourceColumn = "";
		SourceVersion = DataRowVersion.Current;
	}

	public OracleParameter(string name, OracleDbType oracleDbType)
		: this(name, oracleDbType, 0)
	{
	}

	public OracleParameter(string name, OracleDbType oracleDbType, int size)
		: this(name, oracleDbType, size, "")
	{
	}

	public OracleParameter(string name, OracleDbType oracleDbType, int size, string sourceColumn)
	{
		m_name = name ?? "";
		NormalizedParameterName = NormalizeParameterName(m_name);
		OracleDbType = oracleDbType;
		Size = size;
		SourceColumn = sourceColumn ?? "";
		SourceVersion = DataRowVersion.Current;
	}

	public OracleParameter(string name, OracleDbType oracleDbType, object value, ParameterDirection direction)
	{
		m_name = name ?? "";
		Direction = direction;
		NormalizedParameterName = NormalizeParameterName(m_name);
		OracleDbType = oracleDbType;
		Value = value;
		SourceColumn = "";
		SourceVersion = DataRowVersion.Current;
	}

	public OracleParameter(string name, OracleDbType oracleDbType, int size, ParameterDirection direction, bool isNullable, byte precision, byte scale, string sourceColumn, DataRowVersion sourceVersion, object value)
		: this(name, oracleDbType, size, sourceColumn)
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
		m_oracleDbType = OracleDbType.Varchar2;
		m_dbType = DbType.String;
		HasSetDbType = false;
	}

	public OracleParameter Clone()
	{
		return new OracleParameter(this);
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	internal OracleParameter WithParameterName(string parameterName)
	{
		return new OracleParameter(this, parameterName);
	}

	private OracleParameter(OracleParameter other)
	{
		m_dbType = other.m_dbType;
		m_oracleDbType = other.m_oracleDbType;
		m_direction = other.m_direction;
		HasSetDbType = other.HasSetDbType;
		ForceSerializeByOracleDbType = other.ForceSerializeByOracleDbType;
		IsNullable = other.IsNullable;
		Size = other.Size;
		ArrayBindSize = other.ArrayBindSize;
		UdtTypeName = other.UdtTypeName;
		m_name = other.m_name;
		NormalizedParameterName = other.NormalizedParameterName;
		m_value = other.m_value;
		Precision = other.Precision;
		Scale = other.Scale;
		SourceColumn = other.SourceColumn;
		SourceColumnNullMapping = other.SourceColumnNullMapping;
		SourceVersion = other.SourceVersion;
	}

	private OracleParameter(OracleParameter other, string parameterName)
		: this(other)
	{
		if (parameterName == null)
		{
			throw new ArgumentNullException("parameterName");
		}
		ParameterName = parameterName;
	}

	internal void AppendSqlString(ByteBufferWriter writer, StatementPreparerOptions options, ServerSession? session = null)
	{
		bool noBackslashEscapes = (options & StatementPreparerOptions.NoBackslashEscapes) == StatementPreparerOptions.NoBackslashEscapes;
		if (OracleUdt.IsNullParameterValue(Value))
		{
			writer.Write("NULL"u8);
		}
		else if (((!HasSetDbType && !UsesInferredOracleTypeSemantics()) || !TryWriteByOracleDbTypeLiteral(writer, noBackslashEscapes, session)) && !TryWriteInferredStringLiterals(writer, noBackslashEscapes, session) && !TryWriteInferredIntegerLiterals(writer) && !TryWriteInferredBinaryLiterals(writer, noBackslashEscapes))
		{
			if (Value is Stream)
			{
				throw new NotSupportedException("Parameter type " + Value.GetType().Name + " can only be used after calling OracleCommand.Prepare.");
			}
			if (!TryWriteInferredBooleanAndFloatingLiterals(writer) && !TryWriteInferredTemporalLiterals(writer, options) && !TryWriteInferredStringBuilderLiteral(writer, noBackslashEscapes) && !TryWriteInferredEnumLiterals(writer))
			{
				throw new NotSupportedException($"Parameter type {Value.GetType().Name} / OracleDbType {OracleDbType} is not supported yet.");
			}
		}
	}

	private bool TryWriteByOracleDbTypeLiteral(ByteBufferWriter writer, bool noBackslashEscapes, ServerSession? session)
	{
		switch (OracleDbType)
		{
		case OracleDbType.Int16:
			writer.WriteString(Convert.ToInt16(Value, CultureInfo.InvariantCulture));
			return true;
		case OracleDbType.Int32:
			writer.WriteString(Convert.ToInt32(Value, CultureInfo.InvariantCulture));
			return true;
		case OracleDbType.Int64:
			writer.WriteString(Convert.ToInt64(Value, CultureInfo.InvariantCulture));
			return true;
		case OracleDbType.Byte:
		{
			Utf8Formatter.TryFormat(Convert.ToByte(Value, CultureInfo.InvariantCulture), writer.GetSpan(3), out var bytesWritten);
			writer.Advance(bytesWritten);
			return true;
		}
		case OracleDbType.Decimal:
			writer.WriteAscii(Convert.ToDecimal(Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
			return true;
		case OracleDbType.Single:
		case OracleDbType.BinaryFloat:
			writer.WriteAscii(Convert.ToSingle(Value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
			return true;
		case OracleDbType.Double:
		case OracleDbType.BinaryDouble:
			writer.WriteAscii(Convert.ToDouble(Value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
			return true;
		case OracleDbType.Char:
		case OracleDbType.Varchar2:
		{
			string text4 = Convert.ToString(Value, CultureInfo.InvariantCulture) ?? "";
			Encoding encoding = session?.CharacterEncoding;
			if (encoding != null && !encoding.Equals(Encoding.UTF8))
			{
				WriteEncodedStringLiteral(writer, noBackslashEscapes, text4.AsSpan(), encoding);
			}
			else
			{
				WriteStringLiteral(writer, noBackslashEscapes, text4.AsSpan());
			}
			return true;
		}
		case OracleDbType.Clob:
		case OracleDbType.XmlType:
		case OracleDbType.Json:
			WriteStringLiteral(writer, noBackslashEscapes, (Convert.ToString(Value, CultureInfo.InvariantCulture) ?? "").AsSpan());
			return true;
		case OracleDbType.NChar:
		case OracleDbType.NVarchar2:
		{
			string text = Convert.ToString(Value, CultureInfo.InvariantCulture) ?? "";
			if (session?.NCharacterSetConnection != null)
			{
				Encoding characterEncoding = session.CharacterEncoding;
				if (characterEncoding != null && !characterEncoding.Equals(Encoding.UTF8))
				{
					if (CanEncodeWithoutReplacement(text, characterEncoding))
					{
						writer.Write("TO_NCHAR("u8);
						WriteEncodedStringLiteral(writer, noBackslashEscapes, text.AsSpan(), characterEncoding);
						writer.Write((byte)41);
					}
					else
					{
						WriteUnicodeLiteral(writer, text.AsSpan());
					}
					goto IL_02b8;
				}
			}
			writer.Write((byte)78);
			WriteStringLiteral(writer, noBackslashEscapes, text.AsSpan());
			goto IL_02b8;
		}
		case OracleDbType.Raw:
		{
			if (!TryGetOracleRawBytes(Value, out byte[] bytes))
			{
				return false;
			}
			writer.Write("hextoraw('"u8);
			WriteHex(writer, bytes);
			writer.Write("')"u8);
			return true;
		}
		case OracleDbType.Blob:
		case OracleDbType.LongRaw:
		{
			ReadOnlySpan<byte> span;
			if (Value is OracleBlob oracleBlob)
			{
				if (oracleBlob.Length > int.MaxValue)
				{
					throw new NotSupportedException($"OracleBlob is too large for text protocol literal rendering (length: {oracleBlob.Length}). Use OracleCommand.Prepare and binary protocol.");
				}
				span = oracleBlob.GetBytes(1L, (int)oracleBlob.Length);
			}
			else if (!TryGetBinary(Value, out span))
			{
				return false;
			}
			writer.Write("hextoraw('"u8);
			WriteHex(writer, span);
			writer.Write("')"u8);
			return true;
		}
		case OracleDbType.IntervalDS:
			if (Value is OracleIntervalDS oracleIntervalDS)
			{
				writer.WriteAscii(oracleIntervalDS.ToOracleLiteral());
				return true;
			}
			if (Value is TimeSpan timeSpan)
			{
				writer.WriteAscii(OracleIntervalDS.FromTimeSpan(timeSpan).ToOracleLiteral());
				return true;
			}
			if (Value is string text2)
			{
				writer.Write("INTERVAL "u8);
				WriteStringLiteral(writer, noBackslashEscapes, text2.AsSpan());
				writer.Write(" DAY(9) TO SECOND(9)"u8);
				return true;
			}
			if (Value is ReadOnlyMemory<char> readOnlyMemory)
			{
				writer.Write("INTERVAL "u8);
				WriteStringLiteral(writer, noBackslashEscapes, readOnlyMemory.Span);
				writer.Write(" DAY(9) TO SECOND(9)"u8);
				return true;
			}
			if (Value is Memory<char> memory)
			{
				writer.Write("INTERVAL "u8);
				WriteStringLiteral(writer, noBackslashEscapes, memory.Span);
				writer.Write(" DAY(9) TO SECOND(9)"u8);
				return true;
			}
			return false;
		case OracleDbType.IntervalYM:
			if (Value is OracleIntervalYM oracleIntervalYM)
			{
				writer.WriteAscii(oracleIntervalYM.ToOracleLiteral());
				return true;
			}
			if (Value is TimeSpan timeSpan2)
			{
				writer.WriteAscii(OracleIntervalYM.FromTimeSpan(timeSpan2).ToOracleLiteral());
				return true;
			}
			if (Value is string text5)
			{
				writer.Write("INTERVAL "u8);
				WriteStringLiteral(writer, noBackslashEscapes, text5.AsSpan());
				writer.Write(" YEAR(9) TO MONTH"u8);
				return true;
			}
			if (Value is ReadOnlyMemory<char> readOnlyMemory2)
			{
				writer.Write("INTERVAL "u8);
				WriteStringLiteral(writer, noBackslashEscapes, readOnlyMemory2.Span);
				writer.Write(" YEAR(9) TO MONTH"u8);
				return true;
			}
			if (Value is Memory<char> memory2)
			{
				writer.Write("INTERVAL "u8);
				WriteStringLiteral(writer, noBackslashEscapes, memory2.Span);
				writer.Write(" YEAR(9) TO MONTH"u8);
				return true;
			}
			return false;
		case OracleDbType.Date:
			if (Value is DateTime dateTime2)
			{
				if (dateTime2.TimeOfDay == TimeSpan.Zero)
				{
					writer.Write("DATE "u8);
					WriteStringLiteral(writer, noBackslashEscapes, dateTime2.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture).AsSpan());
				}
				else
				{
					writer.Write("TO_DATE("u8);
					WriteStringLiteral(writer, noBackslashEscapes, dateTime2.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture).AsSpan());
					writer.Write(", 'YYYY-MM-DD HH24:MI:SS')"u8);
				}
			}
			else if (Value is string text3)
			{
				if (DateTime.TryParse(text3, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
				{
					if (result.TimeOfDay == TimeSpan.Zero)
					{
						writer.Write("DATE "u8);
						WriteStringLiteral(writer, noBackslashEscapes, result.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture).AsSpan());
					}
					else
					{
						writer.Write("TO_DATE("u8);
						WriteStringLiteral(writer, noBackslashEscapes, result.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture).AsSpan());
						writer.Write(", 'YYYY-MM-DD HH24:MI:SS')"u8);
					}
				}
				else
				{
					writer.Write("TO_DATE("u8);
					WriteStringLiteral(writer, noBackslashEscapes, text3.AsSpan());
					writer.Write(", 'YYYY-MM-DD HH24:MI:SS')"u8);
				}
			}
			else
			{
				writer.Write("TO_DATE("u8);
				WriteStringLiteral(writer, noBackslashEscapes, (Convert.ToString(Value, CultureInfo.InvariantCulture) ?? "").AsSpan());
				writer.Write(", 'YYYY-MM-DD HH24:MI:SS')"u8);
			}
			return true;
		case OracleDbType.TimeStamp:
		case OracleDbType.TimeStampLTZ:
			writer.Write("TIMESTAMP "u8);
			if (Value is DateTime dateTime3)
			{
				WriteStringLiteral(writer, noBackslashEscapes, dateTime3.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture).AsSpan());
			}
			else if (Value is DateTimeOffset dateTimeOffset2)
			{
				WriteStringLiteral(writer, noBackslashEscapes, dateTimeOffset2.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture).AsSpan());
			}
			else
			{
				WriteStringLiteral(writer, noBackslashEscapes, (Convert.ToString(Value, CultureInfo.InvariantCulture) ?? "").AsSpan());
			}
			return true;
		case OracleDbType.TimeStampTZ:
			writer.Write("TIMESTAMP "u8);
			if (Value is DateTimeOffset dateTimeOffset)
			{
				WriteStringLiteral(writer, noBackslashEscapes, dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.ffffff zzz", CultureInfo.InvariantCulture).AsSpan());
			}
			else if (Value is OracleTimeStampTZ oracleTimeStampTZ)
			{
				WriteStringLiteral(writer, noBackslashEscapes, oracleTimeStampTZ.ToString(CultureInfo.InvariantCulture).AsSpan());
			}
			else if (Value is DateTime dateTime)
			{
				WriteStringLiteral(writer, noBackslashEscapes, dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture).AsSpan());
			}
			else
			{
				WriteStringLiteral(writer, noBackslashEscapes, (Convert.ToString(Value, CultureInfo.InvariantCulture) ?? "").AsSpan());
			}
			return true;
		case OracleDbType.Array:
		case OracleDbType.Object:
			return TryWriteOracleUdtLiteral(writer, noBackslashEscapes);
		default:
			{
				return false;
			}
			IL_02b8:
			return true;
		}
	}

	private bool TryWriteInferredStringLiterals(ByteBufferWriter writer, bool noBackslashEscapes, ServerSession? session)
	{
		if (Value is string text)
		{
			WriteStringWithSessionEncoding(writer, noBackslashEscapes, text.AsSpan(), session);
			return true;
		}
		if (Value is ReadOnlyMemory<char> readOnlyMemory)
		{
			WriteStringWithSessionEncoding(writer, noBackslashEscapes, readOnlyMemory.Span, session);
			return true;
		}
		if (Value is Memory<char> memory)
		{
			WriteStringWithSessionEncoding(writer, noBackslashEscapes, memory.Span, session);
			return true;
		}
		if (Value is char c)
		{
			writer.Write((byte)39);
			char c2 = c;
			if (c2 != 0)
			{
				if (c2 != '\'')
				{
					if (c2 != '\\' || noBackslashEscapes)
					{
						goto IL_00db;
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
				if (noBackslashEscapes)
				{
					goto IL_00db;
				}
				writer.Write((ushort)12380);
			}
			goto IL_00e8;
		}
		return false;
		IL_00e8:
		writer.Write((byte)39);
		return true;
		IL_00db:
		writer.Write(c.ToString());
		goto IL_00e8;
	}

	private bool TryWriteInferredIntegerLiterals(ByteBufferWriter writer)
	{
		if (Value is byte value)
		{
			Utf8Formatter.TryFormat(value, writer.GetSpan(3), out var bytesWritten);
			writer.Advance(bytesWritten);
			return true;
		}
		if (Value is sbyte value2)
		{
			Utf8Formatter.TryFormat(value2, writer.GetSpan(4), out var bytesWritten2);
			writer.Advance(bytesWritten2);
			return true;
		}
		if (Value is decimal num)
		{
			writer.WriteAscii(num.ToString(CultureInfo.InvariantCulture));
			return true;
		}
		if (Value is short value3)
		{
			writer.WriteString(value3);
			return true;
		}
		if (Value is ushort value4)
		{
			writer.WriteString(value4);
			return true;
		}
		if (Value is int value5)
		{
			writer.WriteString(value5);
			return true;
		}
		if (Value is uint value6)
		{
			writer.WriteString(value6);
			return true;
		}
		if (Value is long value7)
		{
			writer.WriteString(value7);
			return true;
		}
		if (Value is ulong value8)
		{
			writer.WriteString(value8);
			return true;
		}
		return false;
	}

	private bool TryWriteInferredBinaryLiterals(ByteBufferWriter writer, bool noBackslashEscapes)
	{
		object value = Value;
		if ((!(value is byte[]) && !(value is ReadOnlyMemory<byte>) && !(value is Memory<byte>) && !(value is ArraySegment<byte>) && !(value is MemoryStream) && !(value is float[]) && !(value is ReadOnlyMemory<float>) && !(value is Memory<float>) && !(value is Guid)) || 1 == 0)
		{
			return false;
		}
		value = Value;
		ArraySegment<byte> buffer;
		ReadOnlySpan<byte> readOnlySpan = ((value is Guid guid) ? ((ReadOnlySpan<byte>)guid.ToByteArray()) : ((value is byte[] array) ? ((ReadOnlySpan<byte>)array.AsSpan()) : ((value is ArraySegment<byte> segment) ? ((ReadOnlySpan<byte>)segment.AsSpan()) : ((value is Memory<byte> memory) ? ((ReadOnlySpan<byte>)memory.Span) : ((value is MySqlGeometry mySqlGeometry) ? mySqlGeometry.ValueSpan : ((value is MemoryStream memoryStream) ? ((ReadOnlySpan<byte>)(memoryStream.TryGetBuffer(out buffer) ? buffer.AsSpan() : memoryStream.ToArray().AsSpan())) : ((value is float[] array2) ? ConvertFloatsToBytes(array2.AsSpan()) : ((value is Memory<float> memory2) ? ConvertFloatsToBytes(memory2.Span) : ((!(value is ReadOnlyMemory<float> readOnlyMemory)) ? ((ReadOnlyMemory<byte>)Value).Span : ConvertFloatsToBytes(readOnlyMemory.Span))))))))));
		ReadOnlySpan<byte> bytes = readOnlySpan;
		writer.Write("hextoraw('"u8);
		WriteHex(writer, bytes);
		writer.Write("')"u8);
		return true;
	}

	private bool TryWriteInferredBooleanAndFloatingLiterals(ByteBufferWriter writer)
	{
		if (Value is bool flag)
		{
			writer.Write(flag ? "true"u8 : "false"u8);
			return true;
		}
		if (Value is float num)
		{
			writer.WriteAscii(num.ToString("R", CultureInfo.InvariantCulture));
			return true;
		}
		if (Value is double num2)
		{
			writer.WriteAscii(num2.ToString("R", CultureInfo.InvariantCulture));
			return true;
		}
		if (Value is BigInteger bigInteger)
		{
			writer.WriteAscii(bigInteger.ToString(CultureInfo.InvariantCulture));
			return true;
		}
		return false;
	}

	private bool TryWriteInferredTemporalLiterals(ByteBufferWriter writer, StatementPreparerOptions options)
	{
		if (Value is MySqlDateTime mySqlDateTime)
		{
			if (mySqlDateTime.IsValidDateTime)
			{
				string value = FormattableString.Invariant($"TIMESTAMP '{mySqlDateTime.GetDateTime():yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff}' ");
				writer.WriteAscii(value);
			}
			else
			{
				writer.Write("TIMESTAMP '0000-00-00' "u8);
			}
			return true;
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
			string value2 = FormattableString.Invariant($"TIMESTAMP '{dateTime:yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff}'");
			writer.WriteAscii(value2);
			return true;
		}
		if (Value is DateTimeOffset dateTimeOffset)
		{
			string value3 = FormattableString.Invariant($"TIMESTAMP '{dateTimeOffset.UtcDateTime:yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'ffffff}'");
			writer.WriteAscii(value3);
			return true;
		}
		if (Value is TimeSpan timeSpan)
		{
			writer.WriteAscii(OracleIntervalDS.FromTimeSpan(timeSpan).ToOracleLiteral());
			return true;
		}
		if (Value is OracleIntervalDS oracleIntervalDS)
		{
			writer.WriteAscii(oracleIntervalDS.ToOracleLiteral());
			return true;
		}
		if (Value is OracleIntervalYM oracleIntervalYM)
		{
			writer.WriteAscii(oracleIntervalYM.ToOracleLiteral());
			return true;
		}
		return false;
	}

	private bool TryWriteInferredStringBuilderLiteral(ByteBufferWriter writer, bool noBackslashEscapes)
	{
		if (!(Value is StringBuilder stringBuilder))
		{
			return false;
		}
		WriteStringLiteral(writer, noBackslashEscapes, stringBuilder.ToString().AsSpan());
		return true;
	}

	private bool TryWriteInferredEnumLiterals(ByteBufferWriter writer)
	{
		bool flag;
		switch (OracleDbType)
		{
		case OracleDbType.Char:
		case OracleDbType.NChar:
		case OracleDbType.NVarchar2:
		case OracleDbType.Varchar2:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag && HasSetDbType && Value is Enum obj)
		{
			writer.Write((byte)39);
			writer.Write(obj.ToString("G"));
			writer.Write((byte)39);
			return true;
		}
		if (Value is Enum obj2)
		{
			writer.Write(obj2.ToString("d"));
			return true;
		}
		return false;
	}

	private static void WriteStringLiteral(ByteBufferWriter writer, bool noBackslashEscapes, ReadOnlySpan<char> value)
	{
		writer.Write((byte)39);
		int num = 0;
		while (num < value.Length)
		{
			int num2 = num;
			ReadOnlySpan<char> readOnlySpan = value.Slice(num2, value.Length - num2);
			int num3 = readOnlySpan.IndexOfAny('\0', '\'', '\\');
			if (num3 == -1)
			{
				writer.Write(readOnlySpan);
				num += readOnlySpan.Length;
				continue;
			}
			writer.Write(readOnlySpan.Slice(0, num3));
			if (readOnlySpan[num3] == '\\' && !noBackslashEscapes)
			{
				writer.Write((ushort)23644);
			}
			else if (readOnlySpan[num3] == '\\' && noBackslashEscapes)
			{
				writer.Write((byte)92);
			}
			else if (readOnlySpan[num3] == '\'')
			{
				writer.Write((ushort)10023);
			}
			else if (readOnlySpan[num3] == '\0' && !noBackslashEscapes)
			{
				writer.Write((ushort)12380);
			}
			else if (readOnlySpan[num3] == '\0' && noBackslashEscapes)
			{
				writer.Write((byte)0);
			}
			num += num3 + 1;
		}
		writer.Write((byte)39);
	}

	/// <summary>
	/// Writes an OceanBase Oracle Unicode literal using <c>u'\xxxx...'</c> format, where each
	/// UTF-16 code unit is serialized in little-endian byte order.
	/// This bypasses the session character set entirely and is safe for NCHAR/NVARCHAR2 values
	/// containing characters outside the session charset (e.g., CJK Extension A in a GBK session).
	/// </summary>
	private static void WriteUnicodeLiteral(ByteBufferWriter writer, ReadOnlySpan<char> value)
	{
		writer.Write("u'"u8);
		Span<byte> span = stackalloc byte[6];
		span[0] = 92;
		ReadOnlySpan<char> readOnlySpan = value;
		for (int i = 0; i < readOnlySpan.Length; i++)
		{
			char c = readOnlySpan[i];
			ushort value2 = (ushort)(((c & 0xFF) << 8) | ((c & 0xFF00) >> 8));
			WriteHex4(span.Slice(1), value2);
			writer.Write(span);
		}
		writer.Write((byte)39);
	}

	private static void WriteHex4(Span<byte> destination, ushort value)
	{
		destination[0] = (byte)"0123456789ABCDEF"[(value >> 12) & 0xF];
		destination[1] = (byte)"0123456789ABCDEF"[(value >> 8) & 0xF];
		destination[2] = (byte)"0123456789ABCDEF"[(value >> 4) & 0xF];
		destination[3] = (byte)"0123456789ABCDEF"[value & 0xF];
	}

	/// <summary>
	/// Writes a string literal using the session's character encoding when it differs from UTF-8.
	/// This pre-encodes the string to the target charset's bytes and escapes/writes those bytes,
	/// avoiding ByteBufferWriter.Write(ReadOnlySpan&lt;char&gt;) which is hardcoded to UTF-8.
	/// Multi-byte encodings (GBK, Big5, SJIS, etc.) are handled correctly: a lead byte (0x81-0xFE)
	/// followed by a trail byte is written verbatim as a unit without escaping the trail byte,
	/// even if the trail byte happens to equal '\' (0x5C) or '\'' (0x27).
	/// </summary>
	private static void WriteEncodedStringLiteral(ByteBufferWriter writer, bool noBackslashEscapes, ReadOnlySpan<char> value, Encoding encoding)
	{
		byte[] bytes = encoding.GetBytes(value.ToArray());
		writer.Write((byte)39);
		bool flag = IsDoubleByteCharsetEncoding(encoding);
		for (int i = 0; i < bytes.Length; i++)
		{
			byte b = bytes[i];
			if (flag && b >= 129 && b <= 254 && i + 1 < bytes.Length)
			{
				writer.Write(b);
				writer.Write(bytes[++i]);
				continue;
			}
			if (b == 0 && !noBackslashEscapes)
			{
				writer.Write("\\0"u8);
				continue;
			}
			switch (b)
			{
			case 0:
				writer.Write((byte)0);
				continue;
			case 39:
				writer.Write("''"u8);
				continue;
			case 92:
				if (!noBackslashEscapes)
				{
					writer.Write("\\\\"u8);
					continue;
				}
				break;
			}
			writer.Write(b);
		}
		writer.Write((byte)39);
	}

	private static bool IsDoubleByteCharsetEncoding(Encoding encoding)
	{
		string webName = encoding.WebName;
		if (!webName.Equals("gbk", StringComparison.OrdinalIgnoreCase) && !webName.Equals("gb2312", StringComparison.OrdinalIgnoreCase) && !webName.Equals("gb18030", StringComparison.OrdinalIgnoreCase) && !webName.Equals("big5", StringComparison.OrdinalIgnoreCase) && !webName.Equals("shift_jis", StringComparison.OrdinalIgnoreCase))
		{
			return webName.Equals("euc-kr", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool CanEncodeWithoutReplacement(string value, Encoding encoding)
	{
		try
		{
			Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetByteCount(value);
			return true;
		}
		catch (EncoderFallbackException)
		{
			return false;
		}
	}

	private bool ShouldValidateAnsiStringByteLength()
	{
		bool flag = Size > 0;
		if (flag)
		{
			OracleDbType oracleDbType = OracleDbType;
			bool flag2 = ((oracleDbType == OracleDbType.Char || oracleDbType == OracleDbType.Varchar2) ? true : false);
			flag = flag2;
		}
		return flag;
	}

	private void ValidateAnsiStringByteLength(string value, Encoding encoding)
	{
		if (ShouldValidateAnsiStringByteLength())
		{
			int byteCount = encoding.GetByteCount(value);
			if (byteCount > Size)
			{
				throw new ArgumentException($"String parameter '{ParameterName}' value exceeds maximum byte length of {Size} (encoded length {byteCount}).");
			}
		}
	}

	/// <summary>
	/// Helper that dispatches between WriteStringLiteral (UTF-8) and
	/// WriteEncodedStringLiteral based on the session's CharacterEncoding.
	/// </summary>
	private static void WriteStringWithSessionEncoding(ByteBufferWriter writer, bool noBackslashEscapes, ReadOnlySpan<char> value, ServerSession? session)
	{
		Encoding encoding = session?.CharacterEncoding;
		if (encoding != null && !encoding.Equals(Encoding.UTF8))
		{
			WriteEncodedStringLiteral(writer, noBackslashEscapes, value, encoding);
		}
		else
		{
			WriteStringLiteral(writer, noBackslashEscapes, value);
		}
	}

	internal void AppendBinary(ByteBufferWriter writer, StatementPreparerOptions options)
	{
		AppendBinary(writer, options, null);
	}

	internal void AppendBinary(ByteBufferWriter writer, StatementPreparerOptions options, ServerSession? session)
	{
		if (!OracleUdt.IsNullParameterValue(Value))
		{
			AppendBinary(writer, Value, options, session);
		}
	}

	internal void AppendBinaryValue(ByteBufferWriter writer, object value, StatementPreparerOptions options, ServerSession? session)
	{
		object value2 = m_value;
		try
		{
			m_value = value;
			AppendBinary(writer, value, options, session);
		}
		finally
		{
			m_value = value2;
		}
	}

	private void AppendBinary(ByteBufferWriter writer, object value, StatementPreparerOptions options, ServerSession? session)
	{
		if (OracleUdt.IsNullParameterValue(value) || ((HasSetDbType || UsesInferredOracleTypeSemantics()) && ShouldSerializeByOracleDbType() && TryWriteByOracleDbType(writer)))
		{
			return;
		}
		if (value is string text)
		{
			Encoding encoding = session?.CharacterEncoding ?? Encoding.UTF8;
			ValidateAnsiStringByteLength(text, encoding);
			if (!encoding.Equals(Encoding.UTF8))
			{
				byte[] bytes = encoding.GetBytes(text);
				writer.WriteLengthEncodedInteger((ulong)bytes.Length);
				writer.Write(bytes);
			}
			else
			{
				writer.WriteLengthEncodedString(text);
			}
		}
		else if (value is char c)
		{
			writer.WriteLengthEncodedString(c.ToString());
		}
		else if (value is sbyte b)
		{
			writer.Write((byte)b);
		}
		else if (value is byte value2)
		{
			writer.Write(value2);
		}
		else if (value is bool flag)
		{
			writer.Write(flag ? ((byte)1) : ((byte)0));
		}
		else if (value is short num)
		{
			writer.Write((ushort)num);
		}
		else if (value is ushort value3)
		{
			writer.Write(value3);
		}
		else if (value is int value4)
		{
			writer.Write(value4);
		}
		else if (value is uint value5)
		{
			writer.Write(value5);
		}
		else if (value is long value6)
		{
			writer.Write((ulong)value6);
		}
		else if (value is ulong value7)
		{
			writer.Write(value7);
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
			if (value is float value8)
			{
				byte[] bytes2 = BitConverter.GetBytes(value8);
				if (!BitConverter.IsLittleEndian)
				{
					Array.Reverse(bytes2);
				}
				writer.Write(bytes2);
				return;
			}
			if (value is double value9)
			{
				if (BitConverter.IsLittleEndian)
				{
					writer.Write((ulong)BitConverter.DoubleToInt64Bits(value9));
					return;
				}
				byte[] bytes3 = BitConverter.GetBytes(value9);
				Array.Reverse(bytes3);
				writer.Write(bytes3);
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
			if (value is Guid guid)
			{
				byte[] array3 = guid.ToByteArray();
				writer.Write((byte)array3.Length);
				writer.Write(array3);
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
			OracleDbType oracleDbType = OracleDbType;
			bool flag2 = ((oracleDbType == OracleDbType.Char || oracleDbType == OracleDbType.Varchar2) ? true : false);
			if (flag2 && HasSetDbType && value is Enum obj)
			{
				writer.WriteLengthEncodedString(obj.ToString("G"));
				return;
			}
			if (value is Enum)
			{
				object value10 = Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture);
				AppendBinary(writer, value10, options, session);
				return;
			}
			if (OracleDbType == OracleDbType.Int16)
			{
				writer.Write((ushort)(short)value);
				return;
			}
			if (OracleDbType == OracleDbType.Int32)
			{
				writer.Write((int)value);
				return;
			}
			if (OracleDbType != OracleDbType.Int64)
			{
				throw new NotSupportedException($"Parameter type {value.GetType().Name} is not supported; see https://mysqlconnector.net/param-type. Value: {value}");
			}
			writer.Write((ulong)(long)value);
		}
		bool TryWriteByOracleDbType(ByteBufferWriter byteBufferWriter)
		{
			byte[] array4 = null;
			try
			{
				switch (OracleDbType)
				{
				case OracleDbType.Single:
				case OracleDbType.BinaryFloat:
				{
					byte[] bytes4 = BitConverter.GetBytes(Convert.ToSingle(Value, CultureInfo.InvariantCulture));
					if (!BitConverter.IsLittleEndian)
					{
						Array.Reverse(bytes4);
					}
					byteBufferWriter.Write(bytes4);
					return true;
				}
				case OracleDbType.Double:
				case OracleDbType.BinaryDouble:
				{
					byte[] bytes5 = BitConverter.GetBytes(Convert.ToDouble(Value, CultureInfo.InvariantCulture));
					if (!BitConverter.IsLittleEndian)
					{
						Array.Reverse(bytes5);
					}
					byteBufferWriter.Write(bytes5);
					return true;
				}
				case OracleDbType.NChar:
				case OracleDbType.NVarchar2:
				{
					string text3 = Convert.ToString(Value, CultureInfo.InvariantCulture);
					if (text3 == null)
					{
						return false;
					}
					byte[] bytes10 = (session?.NCharacterEncoding ?? session?.CharacterEncoding ?? Encoding.UTF8).GetBytes(text3);
					byteBufferWriter.WriteLengthEncodedInteger((ulong)bytes10.Length);
					byteBufferWriter.Write(bytes10);
					return true;
				}
				case OracleDbType.Char:
				case OracleDbType.Varchar2:
				{
					string text2 = Convert.ToString(Value, CultureInfo.InvariantCulture);
					if (text2 == null)
					{
						return false;
					}
					Encoding encoding2 = session?.CharacterEncoding ?? Encoding.UTF8;
					ValidateAnsiStringByteLength(text2, encoding2);
					byte[] bytes7 = encoding2.GetBytes(text2);
					byteBufferWriter.WriteLengthEncodedInteger((ulong)bytes7.Length);
					byteBufferWriter.Write(bytes7);
					return true;
				}
				case OracleDbType.Blob:
				{
					if (!TryGetOracleBlobBytes(Value, ForceSerializeByOracleDbType, session?.UseLobLocatorV2 ?? false, out byte[] bytes6))
					{
						return false;
					}
					byteBufferWriter.WriteLengthEncodedInteger((ulong)bytes6.Length);
					byteBufferWriter.Write(bytes6);
					return true;
				}
				case OracleDbType.Clob:
				{
					if (!TryGetOracleClobBytes(Value, ForceSerializeByOracleDbType, session?.UseLobLocatorV2 ?? false, session?.CharacterEncoding ?? Encoding.UTF8, out byte[] bytes9))
					{
						return false;
					}
					byteBufferWriter.WriteLengthEncodedInteger((ulong)bytes9.Length);
					byteBufferWriter.Write(bytes9);
					return true;
				}
				case OracleDbType.LongRaw:
				{
					if (!TryGetBinary(Value, out var span))
					{
						return false;
					}
					byteBufferWriter.WriteLengthEncodedInteger((ulong)span.Length);
					byteBufferWriter.Write(span);
					return true;
				}
				case OracleDbType.Raw:
				{
					if (!TryGetOracleRawBytes(Value, out byte[] bytes8))
					{
						return false;
					}
					byteBufferWriter.WriteLengthEncodedInteger((ulong)bytes8.Length);
					byteBufferWriter.Write(bytes8);
					return true;
				}
				case OracleDbType.IntervalDS:
					if (Value is OracleIntervalDS oracleIntervalDS)
					{
						array4 = oracleIntervalDS.ToBytes();
					}
					else if (Value is TimeSpan timeSpan2)
					{
						array4 = OracleIntervalDS.FromTimeSpan(timeSpan2).ToBytes();
					}
					break;
				case OracleDbType.IntervalYM:
					if (Value is OracleIntervalYM oracleIntervalYM)
					{
						array4 = oracleIntervalYM.ToBytes();
					}
					else if (Value is TimeSpan timeSpan3)
					{
						array4 = OracleIntervalYM.FromTimeSpan(timeSpan3).ToBytes();
					}
					break;
				case OracleDbType.Date:
				{
					if (Value is DateTime dateTime3)
					{
						WriteDateTime(byteBufferWriter, dateTime3.Date);
						return true;
					}
					if (Value is string s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
					{
						WriteDateTime(byteBufferWriter, result.Date);
						return true;
					}
					return false;
				}
				case OracleDbType.TimeStamp:
					if (Value is OracleTimeStamp oracleTimeStamp2)
					{
						array4 = oracleTimeStamp2.ToBytes();
					}
					else if (Value is DateTime value12)
					{
						array4 = new OracleTimeStamp(value12).ToBytes();
					}
					else if (Value is DateTimeOffset dateTimeOffset3)
					{
						array4 = new OracleTimeStamp(dateTimeOffset3.DateTime).ToBytes();
					}
					else if (Value is OracleTimeStampLTZ oracleTimeStampLTZ2)
					{
						array4 = new OracleTimeStamp(oracleTimeStampLTZ2.Value).ToBytes();
					}
					else if (Value is OracleTimeStampTZ oracleTimeStampTZ2)
					{
						array4 = oracleTimeStampTZ2.GetOracleTimeStamp().ToBytes();
					}
					break;
				case OracleDbType.TimeStampLTZ:
					if (Value is OracleTimeStampLTZ oracleTimeStampLTZ3)
					{
						array4 = oracleTimeStampLTZ3.ToBytes();
					}
					else if (Value is DateTime value13)
					{
						array4 = new OracleTimeStampLTZ(value13).ToBytes();
					}
					else if (Value is DateTimeOffset dateTimeOffset4)
					{
						array4 = new OracleTimeStampLTZ(dateTimeOffset4.DateTime).ToBytes();
					}
					else if (Value is OracleTimeStamp oracleTimeStamp3)
					{
						array4 = OracleTimeStampLTZ.FromWire(oracleTimeStamp3.Year, oracleTimeStamp3.Month, oracleTimeStamp3.Day, oracleTimeStamp3.Hour, oracleTimeStamp3.Minute, oracleTimeStamp3.Second, oracleTimeStamp3.Nanoseconds, oracleTimeStamp3.Scale, TimeSpan.Zero).ToBytes();
					}
					else if (Value is OracleTimeStampTZ oracleTimeStampTZ3)
					{
						array4 = oracleTimeStampTZ3.GetOracleTimeStampLTZ().ToBytes();
					}
					break;
				case OracleDbType.TimeStampTZ:
					if (Value is OracleTimeStampTZ oracleTimeStampTZ)
					{
						array4 = oracleTimeStampTZ.ToBytes();
					}
					else if (Value is DateTimeOffset { Offset: var offset } dateTimeOffset2)
					{
						int num3 = (int)offset.TotalMinutes;
						int num4 = Math.Abs(num3);
						int num5 = num4 / 60;
						int num6 = num4 % 60;
						sbyte offsetHours = (sbyte)((num3 < 0) ? (-num5) : num5);
						byte offsetMinutes = (byte)num6;
						array4 = OracleTimeStampTZ.FromWire(dateTimeOffset2.Year, dateTimeOffset2.Month, dateTimeOffset2.Day, dateTimeOffset2.Hour, dateTimeOffset2.Minute, dateTimeOffset2.Second, (int)(dateTimeOffset2.Ticks % 10000000 * 100), 9, offsetHours, offsetMinutes).ToBytes();
					}
					else if (Value is DateTime dateTime2)
					{
						int num7 = (int)((dateTime2.Kind == DateTimeKind.Local) ? TimeZoneInfo.Local.GetUtcOffset(dateTime2) : TimeSpan.Zero).TotalMinutes;
						int num8 = Math.Abs(num7);
						int num9 = num8 / 60;
						int num10 = num8 % 60;
						sbyte offsetHours2 = (sbyte)((num7 < 0) ? (-num9) : num9);
						byte offsetMinutes2 = (byte)num10;
						array4 = OracleTimeStampTZ.FromWire(dateTime2.Year, dateTime2.Month, dateTime2.Day, dateTime2.Hour, dateTime2.Minute, dateTime2.Second, (int)(dateTime2.Ticks % 10000000 * 100), 9, offsetHours2, offsetMinutes2).ToBytes();
					}
					else if (Value is OracleTimeStamp oracleTimeStamp)
					{
						array4 = OracleTimeStampTZ.FromWire(oracleTimeStamp.Year, oracleTimeStamp.Month, oracleTimeStamp.Day, oracleTimeStamp.Hour, oracleTimeStamp.Minute, oracleTimeStamp.Second, oracleTimeStamp.Nanoseconds, oracleTimeStamp.Scale, 0, 0).ToBytes();
					}
					else if (Value is OracleTimeStampLTZ { Value: var value11 })
					{
						array4 = OracleTimeStampTZ.FromWire(value11.Year, value11.Month, value11.Day, value11.Hour, value11.Minute, value11.Second, (int)(value11.Ticks % 10000000 * 100), 9, 0, 0).ToBytes();
					}
					break;
				case OracleDbType.Byte:
					byteBufferWriter.Write(Convert.ToByte(Value, CultureInfo.InvariantCulture));
					return true;
				case OracleDbType.Boolean:
					byteBufferWriter.Write(Convert.ToBoolean(Value, CultureInfo.InvariantCulture) ? ((byte)1) : ((byte)0));
					return true;
				case OracleDbType.Int16:
					byteBufferWriter.Write((ushort)Convert.ToInt16(Value, CultureInfo.InvariantCulture));
					return true;
				case OracleDbType.Int32:
					byteBufferWriter.Write(Convert.ToInt32(Value, CultureInfo.InvariantCulture));
					return true;
				case OracleDbType.Int64:
					byteBufferWriter.Write((ulong)Convert.ToInt64(Value, CultureInfo.InvariantCulture));
					return true;
				case OracleDbType.Decimal:
					byteBufferWriter.WriteLengthEncodedAsciiString(Convert.ToDecimal(Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
					return true;
				case OracleDbType.Array:
				case OracleDbType.Object:
				{
					if (!OracleUdt.TryCreateValue(null, Value, UdtTypeName, OracleDbType == OracleDbType.Array, out OracleUdtValue udtValue))
					{
						return false;
					}
					OracleUdtTdsEncoder.WriteBinaryValue(byteBufferWriter, udtValue, session?.CharacterEncoding);
					return true;
				}
				default:
					return false;
				}
			}
			catch
			{
				return false;
			}
			if (array4 == null)
			{
				return false;
			}
			byteBufferWriter.Write((byte)array4.Length);
			byteBufferWriter.Write(array4);
			return true;
		}
	}

	private bool TryWriteOracleUdtLiteral(ByteBufferWriter writer, bool noBackslashEscapes)
	{
		if (OracleUdt.IsNullParameterValue(Value))
		{
			writer.Write("NULL"u8);
			return true;
		}
		if (!OracleUdt.TryCreateValue(null, Value, UdtTypeName, OracleDbType == OracleDbType.Array, out OracleUdtValue udtValue))
		{
			return false;
		}
		OracleUdtTdsEncoder.WriteLiteralValue(writer, udtValue, noBackslashEscapes);
		return true;
	}

	private static bool TryGetBinary(object value, out ReadOnlySpan<byte> span)
	{
		if (!(value is byte[] array))
		{
			if (!(value is ArraySegment<byte> segment))
			{
				if (!(value is Memory<byte> memory))
				{
					if (!(value is ReadOnlyMemory<byte> readOnlyMemory))
					{
						if (value is MemoryStream memoryStream)
						{
							span = (memoryStream.TryGetBuffer(out var buffer) ? buffer.AsSpan() : memoryStream.ToArray().AsSpan());
							return true;
						}
						span = default(ReadOnlySpan<byte>);
						return false;
					}
					span = readOnlyMemory.Span;
					return true;
				}
				span = memory.Span;
				return true;
			}
			span = segment.AsSpan();
			return true;
		}
		span = array;
		return true;
	}

	private static bool TryGetOracleBlobBytes(object value, bool allowLocatorBytes, bool useLobLocatorV2, out byte[] bytes)
	{
		if (value is OracleBlob oracleBlob)
		{
			bytes = GetOracleBlobBytes(oracleBlob, useLobLocatorV2);
			return true;
		}
		if (allowLocatorBytes && TryGetBinary(value, out var span))
		{
			bytes = span.ToArray();
			return true;
		}
		if (!TryGetBinary(value, out var span2))
		{
			bytes = Array.Empty<byte>();
			return false;
		}
		bytes = GetOracleBlobBytes(new OracleBlob(span2, useLobLocatorV2), useLobLocatorV2);
		return true;
	}

	private static byte[] GetOracleBlobBytes(OracleBlob oracleBlob, bool useLobLocatorV2)
	{
		ObLobLocator locatorInternal = oracleBlob.LocatorInternal;
		if (locatorInternal != null && locatorInternal.BinaryData.Length > 0)
		{
			return locatorInternal.BinaryData.ToArray();
		}
		byte[] array = oracleBlob.TryGetInMemoryBytes();
		if (array != null)
		{
			return new OracleBlob(array.AsSpan(), useLobLocatorV2).LocatorInternal.BinaryData.ToArray();
		}
		if (oracleBlob.Length > int.MaxValue)
		{
			throw new NotSupportedException($"OracleBlob is too large for binary protocol parameter binding (length: {oracleBlob.Length}).");
		}
		return new OracleBlob(oracleBlob.GetBytes(1L, (int)oracleBlob.Length).AsSpan(), useLobLocatorV2).LocatorInternal.BinaryData.ToArray();
	}

	private static bool TryGetOracleClobBytes(object value, bool allowLocatorBytes, bool useLobLocatorV2, Encoding encoding, out byte[] bytes)
	{
		if (!(value is OracleClob oracleClob))
		{
			if (!(value is string s))
			{
				if (!(value is char c))
				{
					if (!(value is char[] chars))
					{
						if (!(value is ReadOnlyMemory<char> readOnlyMemory))
						{
							if (!(value is Memory<char> memory))
							{
								if (value is StringBuilder stringBuilder)
								{
									bytes = BuildOracleClobLocator(encoding.GetBytes(stringBuilder.ToString()), useLobLocatorV2);
									return true;
								}
								if (allowLocatorBytes && TryGetBinary(value, out var span))
								{
									bytes = span.ToArray();
									return true;
								}
								if (TryGetBinary(value, out var span2))
								{
									bytes = BuildOracleClobLocator(span2.ToArray(), useLobLocatorV2);
									return true;
								}
								bytes = Array.Empty<byte>();
								return false;
							}
							bytes = BuildOracleClobLocator(encoding.GetBytes(memory.ToArray()), useLobLocatorV2);
							return true;
						}
						bytes = BuildOracleClobLocator(encoding.GetBytes(readOnlyMemory.ToArray()), useLobLocatorV2);
						return true;
					}
					bytes = BuildOracleClobLocator(encoding.GetBytes(chars), useLobLocatorV2);
					return true;
				}
				bytes = BuildOracleClobLocator(encoding.GetBytes(new char[1] { c }), useLobLocatorV2);
				return true;
			}
			bytes = BuildOracleClobLocator(encoding.GetBytes(s), useLobLocatorV2);
			return true;
		}
		bytes = GetOracleClobLocatorBytes(oracleClob, encoding, useLobLocatorV2);
		return true;
	}

	private static byte[] GetOracleClobLocatorBytes(OracleClob oracleClob, Encoding encoding, bool useLobLocatorV2)
	{
		ObLobLocator locatorInternal = oracleClob.LocatorInternal;
		if (locatorInternal != null && locatorInternal.BinaryData.Length > 0)
		{
			return locatorInternal.BinaryData.ToArray();
		}
		string text = oracleClob.GetString();
		return BuildOracleClobLocator(encoding.GetBytes(text ?? string.Empty), useLobLocatorV2);
	}

	private static byte[] BuildOracleClobLocator(byte[] payload, bool useLobLocatorV2)
	{
		if (payload == null)
		{
			payload = Array.Empty<byte>();
		}
		if (useLobLocatorV2)
		{
			byte[] array = new byte[8 + payload.Length];
			BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(0, 4), 1280262689u);
			BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(4, 4), 40962u);
			payload.CopyTo(array.AsSpan(8));
			return array;
		}
		byte[] array2 = new byte[40 + payload.Length];
		BinaryPrimitives.WriteUInt32LittleEndian(array2.AsSpan(0, 4), 1280262689u);
		BinaryPrimitives.WriteUInt32LittleEndian(array2.AsSpan(4, 4), 1u);
		BinaryPrimitives.WriteUInt32LittleEndian(array2.AsSpan(36, 4), (uint)payload.Length);
		payload.CopyTo(array2.AsSpan(40));
		return array2;
	}

	private static bool TryGetOracleRawBytes(object value, out byte[] bytes)
	{
		if (!(value is Guid guid))
		{
			if (!(value is byte[] array))
			{
				if (!(value is ArraySegment<byte> segment))
				{
					if (!(value is Memory<byte> memory))
					{
						if (!(value is ReadOnlyMemory<byte> readOnlyMemory))
						{
							if (!(value is MemoryStream memoryStream))
							{
								if (!(value is string value2))
								{
									if (!(value is Memory<char> memory2))
									{
										if (value is ReadOnlyMemory<char> readOnlyMemory2)
										{
											return TryParseOracleRawString(readOnlyMemory2.ToString(), out bytes);
										}
										bytes = Array.Empty<byte>();
										return false;
									}
									return TryParseOracleRawString(memory2.ToString(), out bytes);
								}
								return TryParseOracleRawString(value2, out bytes);
							}
							bytes = (memoryStream.TryGetBuffer(out var buffer) ? buffer.AsSpan().ToArray() : memoryStream.ToArray());
							return true;
						}
						bytes = readOnlyMemory.ToArray();
						return true;
					}
					bytes = memory.ToArray();
					return true;
				}
				bytes = segment.AsSpan().ToArray();
				return true;
			}
			bytes = array;
			return true;
		}
		bytes = guid.ToByteArray();
		return true;
	}

	private static bool TryParseOracleRawString(string value, out byte[] bytes)
	{
		string text = value.Trim();
		if (text.StartsWith("hextoraw('", StringComparison.OrdinalIgnoreCase) && text.EndsWith("')", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Substring(10, text.Length - 12);
		}
		if (Guid.TryParse(text, out var result))
		{
			bytes = result.ToByteArray();
			return true;
		}
		if ((text.Length & 1) != 0)
		{
			bytes = Array.Empty<byte>();
			return false;
		}
		byte[] array = new byte[text.Length / 2];
		for (int i = 0; i < array.Length; i++)
		{
			int num = ParseHexNibble(text[i * 2]);
			int num2 = ParseHexNibble(text[i * 2 + 1]);
			if (num < 0 || num2 < 0)
			{
				bytes = Array.Empty<byte>();
				return false;
			}
			array[i] = (byte)((num << 4) | num2);
		}
		bytes = array;
		return true;
	}

	private static int ParseHexNibble(char c)
	{
		switch (c)
		{
		case '0':
		case '1':
		case '2':
		case '3':
		case '4':
		case '5':
		case '6':
		case '7':
		case '8':
		case '9':
			return c - 48;
		case 'A':
		case 'B':
		case 'C':
		case 'D':
		case 'E':
		case 'F':
			return c - 65 + 10;
		case 'a':
		case 'b':
		case 'c':
		case 'd':
		case 'e':
		case 'f':
			return c - 97 + 10;
		default:
			return -1;
		}
	}

	private static void WriteHex(ByteBufferWriter writer, ReadOnlySpan<byte> bytes)
	{
		Span<byte> span = writer.GetSpan(bytes.Length * 2);
		int count = 0;
		ReadOnlySpan<byte> readOnlySpan = bytes;
		for (int i = 0; i < readOnlySpan.Length; i++)
		{
			byte b = readOnlySpan[i];
			span[count++] = (byte)"0123456789ABCDEF"[b >> 4];
			span[count++] = (byte)"0123456789ABCDEF"[b & 0xF];
		}
		writer.Advance(count);
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
					if (c == ':' || c == '?' || c == '@')
					{
						goto IL_00d5;
					}
				}
			}
			else
			{
				char c = text[0];
				if (c == ':' || c == '?' || c == '@')
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
					goto IL_00d5;
				}
			}
			return text;
		}
		global::_003CPrivateImplementationDetails_003E.ThrowSwitchExpressionException(text);
		string result = default(string);
		return result;
		IL_00d5:
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
		writer.Write((byte)((num == 0) ? 7u : 11u));
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

	internal bool ShouldSerializeByOracleDbType()
	{
		if (ForceSerializeByOracleDbType)
		{
			return true;
		}
		if (UsesInferredOracleTypeSemantics())
		{
			return true;
		}
		if (HasSetDbType)
		{
			switch (m_oracleDbType)
			{
			case OracleDbType.Blob:
			case OracleDbType.Byte:
			case OracleDbType.Char:
			case OracleDbType.Clob:
			case OracleDbType.Date:
			case OracleDbType.Decimal:
			case OracleDbType.Double:
			case OracleDbType.LongRaw:
			case OracleDbType.Int16:
			case OracleDbType.Int32:
			case OracleDbType.Int64:
			case OracleDbType.IntervalDS:
			case OracleDbType.IntervalYM:
			case OracleDbType.NChar:
			case OracleDbType.NVarchar2:
			case OracleDbType.Raw:
			case OracleDbType.Single:
			case OracleDbType.TimeStamp:
			case OracleDbType.TimeStampLTZ:
			case OracleDbType.TimeStampTZ:
			case OracleDbType.Varchar2:
			case OracleDbType.Array:
			case OracleDbType.Object:
			case OracleDbType.BinaryDouble:
			case OracleDbType.BinaryFloat:
			case OracleDbType.Boolean:
				return true;
			default:
				return false;
			}
		}
		return false;
	}

	private bool UsesInferredOracleTypeSemantics()
	{
		bool flag = !HasSetDbType;
		if (flag)
		{
			object value = m_value;
			bool flag2 = ((value is TimeSpan || value is OracleIntervalDS || value is OracleIntervalYM || value is IOracleCustomType) ? true : false);
			flag = flag2;
		}
		return flag;
	}
}
