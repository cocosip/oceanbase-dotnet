using System;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;

namespace OceanBase.ColumnReaders;

internal abstract class ColumnReader
{
	public static ColumnReader Create(bool isBinary, ColumnDefinitionPayload columnDefinition, ConnectionFacade connection)
	{
		bool flag = (columnDefinition.ColumnFlags & ColumnFlags.Unsigned) != 0;
		switch (columnDefinition.ColumnType)
		{
		case ColumnType.OracleComplex:
		case ColumnType.OracleArray:
		case ColumnType.OracleStruct:
		{
			bool isArray = OracleUdt.ShouldDecodeAsArray(columnDefinition.ComplexTypeName ?? columnDefinition.SchemaName ?? string.Empty, columnDefinition.ColumnType == ColumnType.OracleArray);
			Encoding encoding = connection.Session?.CharacterEncoding ?? Encoding.UTF8;
			if (!isBinary)
			{
				return new TextOracleUdtColumnReader(isArray, encoding);
			}
			return new BinaryOracleUdtColumnReader(isArray, encoding);
		}
		case ColumnType.OracleCursor:
			return new OracleCursorColumnReader(connection);
		case ColumnType.Tiny:
			if (connection.TreatTinyAsBoolean && columnDefinition.ColumnLength == 1 && !flag)
			{
				if (!isBinary)
				{
					return TextBooleanColumnReader.Instance;
				}
				return BinaryBooleanColumnReader.Instance;
			}
			if (!isBinary)
			{
				if (!flag)
				{
					return TextSignedInt8ColumnReader.Instance;
				}
				return TextUnsignedInt8ColumnReader.Instance;
			}
			if (!flag)
			{
				return BinarySignedInt8ColumnReader.Instance;
			}
			return BinaryUnsignedInt8ColumnReader.Instance;
		case ColumnType.Long:
		case ColumnType.Int24:
		case ColumnType.OracleNumber:
			if (!isBinary)
			{
				if (!flag)
				{
					return TextSignedInt32ColumnReader.Instance;
				}
				return TextUnsignedInt32ColumnReader.Instance;
			}
			if (!flag)
			{
				return BinarySignedInt32ColumnReader.Instance;
			}
			return BinaryUnsignedInt32ColumnReader.Instance;
		case ColumnType.Longlong:
			if (!isBinary)
			{
				if (!flag)
				{
					return TextSignedInt64ColumnReader.Instance;
				}
				return TextUnsignedInt64ColumnReader.Instance;
			}
			if (!flag)
			{
				return BinarySignedInt64ColumnReader.Instance;
			}
			return BinaryUnsignedInt64ColumnReader.Instance;
		case ColumnType.Bit:
			return BitColumnReader.Instance;
		case ColumnType.String:
			if (connection.GuidFormat == MySqlGuidFormat.Char36 && columnDefinition.ColumnLength / ProtocolUtility.GetBytesPerCharacter(columnDefinition.CharacterSet) == 36)
			{
				return GuidChar36ColumnReader.Instance;
			}
			if (connection.GuidFormat == MySqlGuidFormat.Char32 && columnDefinition.ColumnLength / ProtocolUtility.GetBytesPerCharacter(columnDefinition.CharacterSet) == 32)
			{
				return GuidChar32ColumnReader.Instance;
			}
			goto case ColumnType.VarChar;
		case ColumnType.VarChar:
		case ColumnType.Enum:
		case ColumnType.Set:
		case ColumnType.TinyBlob:
		case ColumnType.MediumBlob:
		case ColumnType.LongBlob:
		case ColumnType.Blob:
		case ColumnType.VarString:
		case ColumnType.OracleVarchar2:
			if (columnDefinition.CharacterSet != CharacterSet.Binary)
			{
				StringColumnReader stringColumnReader = connection.Session?.CachedStringColumnReader;
				if (stringColumnReader != null)
				{
					return stringColumnReader;
				}
				return StringColumnReader.Instance;
			}
			if (columnDefinition.ColumnLength == 16)
			{
				return connection.GuidFormat switch
				{
					MySqlGuidFormat.Binary16 => GuidBinary16ColumnReader.Instance, 
					MySqlGuidFormat.TimeSwapBinary16 => GuidTimeSwapBinary16ColumnReader.Instance, 
					MySqlGuidFormat.LittleEndianBinary16 => GuidLittleEndianBinary16ColumnReader.Instance, 
					_ => BytesColumnReader.Instance, 
				};
			}
			return BytesColumnReader.Instance;
		case ColumnType.OracleNVarChar2:
		case ColumnType.OracleNChar:
			if (columnDefinition.CharacterSet != CharacterSet.Binary)
			{
				return new OracleNStringColumnReader(connection);
			}
			return BytesColumnReader.Instance;
		case ColumnType.OracleRaw:
		case ColumnType.OracleBlob:
		case ColumnType.OracleClob:
			return BytesColumnReader.Instance;
		case ColumnType.OracleRowId:
			return StringColumnReader.Instance;
		case ColumnType.Json:
			return StringColumnReader.Instance;
		case ColumnType.Short:
			if (!isBinary)
			{
				if (!flag)
				{
					return TextSignedInt16ColumnReader.Instance;
				}
				return TextUnsignedInt16ColumnReader.Instance;
			}
			if (!flag)
			{
				return BinarySignedInt16ColumnReader.Instance;
			}
			return BinaryUnsignedInt16ColumnReader.Instance;
		case ColumnType.Timestamp:
		case ColumnType.Date:
		case ColumnType.DateTime:
		case ColumnType.NewDate:
			if (!isBinary)
			{
				return new TextDateTimeColumnReader(connection);
			}
			return new BinaryDateTimeColumnReader(connection);
		case ColumnType.Time:
			if (!isBinary)
			{
				return TextTimeColumnReader.Instance;
			}
			return BinaryTimeColumnReader.Instance;
		case ColumnType.Year:
			if (!isBinary)
			{
				return TextSignedInt32ColumnReader.Instance;
			}
			return BinaryYearColumnReader.Instance;
		case ColumnType.OracleIntervalDS:
			return BinaryOracleIntervalDSColumnReader.Instance;
		case ColumnType.OracleIntervalYM:
			return BinaryOracleIntervalYMColumnReader.Instance;
		case ColumnType.OracleTimeStampLTZ:
			return BinaryOracleTimeStampLTZColumnReader.Instance;
		case ColumnType.OracleTimeStampTZ:
			return BinaryOracleTimeStampTZColumnReader.Instance;
		case ColumnType.OracleTimeStampNano:
			return BinaryOracleTimeStampNanoColumnReader.Instance;
		case ColumnType.Float:
			if (!isBinary)
			{
				return TextFloatColumnReader.Instance;
			}
			return BinaryFloatColumnReader.Instance;
		case ColumnType.OracleBinaryFloat:
			if (!isBinary)
			{
				return TextFloatColumnReader.Instance;
			}
			return BinaryFloatColumnReader.Instance;
		case ColumnType.OracleNumberFloat:
			return TextDoubleColumnReader.Instance;
		case ColumnType.Double:
		case ColumnType.OracleBinaryDouble:
			if (!isBinary)
			{
				return TextDoubleColumnReader.Instance;
			}
			return BinaryDoubleColumnReader.Instance;
		case ColumnType.Decimal:
		case ColumnType.NewDecimal:
			return DecimalColumnReader.Instance;
		case ColumnType.Geometry:
			return BytesColumnReader.Instance;
		case ColumnType.Null:
			return NullColumnReader.Instance;
		case ColumnType.Vector:
			return VectorColumnReader.Instance;
		default:
			throw new NotImplementedException($"Reading {columnDefinition.ColumnType} not implemented");
		}
	}

	public abstract object ReadValue(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition);

	public virtual int? TryReadInt32(ReadOnlySpan<byte> data, ColumnDefinitionPayload columnDefinition)
	{
		return null;
	}
}
