using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol;

/// <summary>
/// Decodes the wire bytes for an Oracle UDT column into an <see cref="T:OceanBase.OracleUdtValue" />.
/// </summary>
/// <remarks>
/// Reads the OceanBase Oracle-mode binary UDT format (mirroring JDBC
/// <c>OBStructParameter#writeBinary</c> and <c>OBArrayParameter#writeBinary</c>).
/// </remarks>
internal static class OracleUdtTdsDecoder
{
	/// <summary>
	/// Represents the inferred type information for a UDT attribute.
	/// </summary>
	private readonly struct AttributeTypeInfo(OracleUdtComplexScalarType scalarType, bool isNestedUdt, string? nestedUdtTypeName, bool isArray)
	{
		public OracleUdtComplexScalarType ScalarType { get; } = scalarType;

		public bool IsNestedUdt { get; } = isNestedUdt;

		public string? NestedUdtTypeName { get; } = nestedUdtTypeName;

		public bool IsArray { get; } = isArray;
	}

	/// <summary>
	/// Decodes the UDT payload (without a length prefix) from <paramref name="data" />.
	/// The caller must supply <paramref name="typeName" /> and <paramref name="isArray" />
	/// because the native wire format does not embed them.
	/// </summary>
	/// <param name="data">The wire bytes to decode.</param>
	/// <param name="typeName">The UDT type name used to infer attribute layout.</param>
	/// <param name="isArray">Whether the value is an array UDT.</param>
	/// <param name="stringEncoding">
	/// Session character encoding for VARCHAR2 attributes (from the connection string <c>CharacterSet</c>).
	/// NUMBER attributes always use UTF-8 because the wire format stores them as ASCII-compatible decimal strings
	/// (digits 0-9, sign, decimal point), which are identical in UTF-8 and all single-byte encodings.
	/// This matches JDBC <c>BinaryRowProtocol#getComplexAttrData</c> behavior.
	/// </param>
	public static OracleUdtValue ReadValue(ReadOnlySpan<byte> data, string typeName, bool isArray, Encoding? stringEncoding = null)
	{
		if (stringEncoding == null)
		{
			stringEncoding = Encoding.UTF8;
		}
		ByteArrayReader reader = new ByteArrayReader(data);
		isArray = OracleUdt.ShouldDecodeAsArray(typeName, isArray);
		if (isArray)
		{
			return ReadArray(ref reader, typeName, stringEncoding);
		}
		return ReadStruct(ref reader, typeName, stringEncoding);
	}

	/// <summary>
	/// Decodes the UDT payload from the current position of <paramref name="reader" />.
	/// Advances the reader past the UDT data. Returns the parsed <see cref="T:OceanBase.OracleUdtValue" />.
	/// </summary>
	public static OracleUdtValue ReadValue(ref ByteArrayReader reader, string typeName, bool isArray, Encoding? stringEncoding = null)
	{
		if (stringEncoding == null)
		{
			stringEncoding = Encoding.UTF8;
		}
		isArray = OracleUdt.ShouldDecodeAsArray(typeName, isArray);
		if (isArray)
		{
			return ReadArray(ref reader, typeName, stringEncoding);
		}
		return ReadStruct(ref reader, typeName, stringEncoding);
	}

	private static OracleUdtValue ReadStruct(ref ByteArrayReader reader, string typeName, Encoding stringEncoding)
	{
		int num = InferAttributeCount(typeName);
		IReadOnlyList<AttributeTypeInfo> readOnlyList = InferAttributeTypes(typeName);
		int length = (num + 7 + 2) / 8;
		ReadOnlySpan<byte> readOnlySpan = ((num > 0) ? reader.ReadByteString(length) : ((ReadOnlySpan<byte>)Array.Empty<byte>()));
		List<object> list = new List<object>(num);
		for (int i = 0; i < num; i++)
		{
			if ((readOnlySpan[(i + 2) / 8] & (1 << (i + 2) % 8)) != 0)
			{
				list.Add(DBNull.Value);
				continue;
			}
			AttributeTypeInfo attrType = ((i < readOnlyList.Count) ? readOnlyList[i] : default(AttributeTypeInfo));
			if (attrType.IsNestedUdt && !string.IsNullOrEmpty(attrType.NestedUdtTypeName))
			{
				if (attrType.IsArray)
				{
					list.Add(ReadArray(ref reader, attrType.NestedUdtTypeName, stringEncoding));
				}
				else
				{
					list.Add(ReadStruct(ref reader, attrType.NestedUdtTypeName, stringEncoding));
				}
			}
			else
			{
				list.Add(ReadComplexAttrData(ref reader, attrType, stringEncoding));
			}
		}
		return new OracleUdtValue(typeName, isArray: false, list);
	}

	private static int InferAttributeCount(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			throw new InvalidOperationException("A UDT type name is required to decode a structured UDT value.");
		}
		if (!OracleUdtTypeRegistry.TryGetCustomType(typeName, out Type type))
		{
			throw new InvalidOperationException("Custom type mapping for '" + typeName + "' is not specified or is invalid.");
		}
		return (from m in type.GetTypeInfo().DeclaredMembers
			where (m is FieldInfo || m is PropertyInfo) ? true : false
			where m.GetCustomAttribute<OracleObjectMappingAttribute>() != null || m.GetCustomAttribute<OracleArrayMappingAttribute>() != null
			orderby m.MetadataToken
			select m).ToArray().Length;
	}

	/// <summary>
	/// Infers the type information for each attribute of a UDT struct.
	/// Returns a list of <see cref="T:OceanBase.Protocol.OracleUdtTdsDecoder.AttributeTypeInfo" /> in metadata token order.
	/// </summary>
	private static IReadOnlyList<AttributeTypeInfo> InferAttributeTypes(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return Array.Empty<AttributeTypeInfo>();
		}
		if (!OracleUdtTypeRegistry.TryGetCustomType(typeName, out Type type))
		{
			return Array.Empty<AttributeTypeInfo>();
		}
		List<AttributeTypeInfo> list = new List<AttributeTypeInfo>();
		MemberInfo[] array = (from m in type.GetTypeInfo().DeclaredMembers
			where (m is FieldInfo || m is PropertyInfo) ? true : false
			where m.GetCustomAttribute<OracleObjectMappingAttribute>() != null || m.GetCustomAttribute<OracleArrayMappingAttribute>() != null
			orderby m.MetadataToken
			select m).ToArray();
		foreach (MemberInfo memberInfo in array)
		{
			Type type2 = ((memberInfo is PropertyInfo propertyInfo) ? propertyInfo.PropertyType : ((memberInfo is FieldInfo fieldInfo) ? fieldInfo.FieldType : null));
			if ((object)type2 == null)
			{
				list.Add(new AttributeTypeInfo(OracleUdtComplexScalarType.Unknown, isNestedUdt: false, null, isArray: false));
				continue;
			}
			bool flag = typeof(IOracleCustomType).IsAssignableFrom(type2);
			bool isArray = type2.IsArray;
			string nestedUdtTypeName = null;
			if (flag)
			{
				OracleCustomTypeMappingAttribute customAttribute = type2.GetTypeInfo().GetCustomAttribute<OracleCustomTypeMappingAttribute>();
				if (customAttribute != null)
				{
					nestedUdtTypeName = customAttribute.UdtTypeName;
				}
			}
			else if (isArray)
			{
				Type elementType = type2.GetElementType();
				if ((object)elementType != null && OracleUdtTypeRegistry.TryGetArrayTypeNameForElementType(elementType, out string typeName2))
				{
					flag = true;
					nestedUdtTypeName = typeName2;
				}
			}
			OracleUdtComplexScalarType scalarType = ((!flag) ? OracleUdtComplexScalarInference.InferMemberScalarType(type2) : OracleUdtComplexScalarType.Unknown);
			list.Add(new AttributeTypeInfo(scalarType, flag, nestedUdtTypeName, isArray));
		}
		return list;
	}

	private static OracleUdtValue ReadArray(ref ByteArrayReader reader, string typeName, Encoding stringEncoding)
	{
		int num = checked((int)reader.ReadLengthEncodedInteger());
		int length = (num + 7 + 2) / 8;
		ReadOnlySpan<byte> readOnlySpan = reader.ReadByteString(length);
		AttributeTypeInfo attrType = InferArrayElementType(typeName);
		List<object> list = new List<object>(num);
		for (int i = 0; i < num; i++)
		{
			if ((readOnlySpan[(i + 2) / 8] & (1 << (i + 2) % 8)) != 0)
			{
				list.Add(DBNull.Value);
			}
			else if (attrType.IsNestedUdt && !string.IsNullOrEmpty(attrType.NestedUdtTypeName))
			{
				if (attrType.IsArray)
				{
					list.Add(ReadArray(ref reader, attrType.NestedUdtTypeName, stringEncoding));
				}
				else
				{
					list.Add(ReadStruct(ref reader, attrType.NestedUdtTypeName, stringEncoding));
				}
			}
			else
			{
				list.Add(ReadComplexAttrData(ref reader, attrType, stringEncoding));
			}
		}
		return new OracleUdtValue(typeName, isArray: true, list);
	}

	/// <summary>
	/// Infers the element type information for an array UDT.
	/// Returns whether the element is a nested UDT and its type name.
	/// </summary>
	private static AttributeTypeInfo InferArrayElementType(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return new AttributeTypeInfo(OracleUdtComplexScalarType.Unknown, isNestedUdt: false, null, isArray: false);
		}
		if (OracleUdtTypeRegistry.TryGetArrayFactoryType(typeName, out Type type))
		{
			Type type2 = (((IOracleArrayTypeFactory)Activator.CreateInstance(type))?.CreateArray(1))?.GetType().GetElementType();
			if ((object)type2 != null)
			{
				return InferElementTypeInfo(type2);
			}
		}
		if (!OracleUdtTypeRegistry.TryGetCustomType(typeName, out Type type3))
		{
			return new AttributeTypeInfo(OracleUdtComplexScalarType.Unknown, isNestedUdt: false, null, isArray: false);
		}
		MemberInfo memberInfo = type3.GetTypeInfo().DeclaredMembers.Where((MemberInfo m) => (m is FieldInfo || m is PropertyInfo) ? true : false).FirstOrDefault((MemberInfo m) => m.GetCustomAttribute<OracleArrayMappingAttribute>() != null);
		if ((object)memberInfo == null)
		{
			return new AttributeTypeInfo(OracleUdtComplexScalarType.Unknown, isNestedUdt: false, null, isArray: false);
		}
		Type type4 = ((memberInfo is PropertyInfo propertyInfo) ? propertyInfo.PropertyType : ((memberInfo is FieldInfo fieldInfo) ? fieldInfo.FieldType : null));
		if ((object)type4 == null)
		{
			return new AttributeTypeInfo(OracleUdtComplexScalarType.Unknown, isNestedUdt: false, null, isArray: false);
		}
		return InferElementTypeInfo(type4.IsArray ? type4.GetElementType() : type4);
	}

	private static AttributeTypeInfo InferElementTypeInfo(Type elementType)
	{
		bool flag = typeof(IOracleCustomType).IsAssignableFrom(elementType);
		bool isArray = elementType.IsArray;
		string nestedUdtTypeName = null;
		if (flag)
		{
			OracleCustomTypeMappingAttribute customAttribute = elementType.GetTypeInfo().GetCustomAttribute<OracleCustomTypeMappingAttribute>();
			if (customAttribute != null)
			{
				nestedUdtTypeName = customAttribute.UdtTypeName;
			}
		}
		else if (isArray)
		{
			Type elementType2 = elementType.GetElementType();
			if ((object)elementType2 != null && OracleUdtTypeRegistry.TryGetArrayTypeNameForElementType(elementType2, out string typeName))
			{
				flag = true;
				nestedUdtTypeName = typeName;
			}
		}
		return new AttributeTypeInfo((!flag) ? OracleUdtComplexScalarInference.InferMemberScalarType(elementType) : OracleUdtComplexScalarType.Unknown, flag, nestedUdtTypeName, isArray);
	}

	private static object? ReadComplexAttrData(ref ByteArrayReader reader, AttributeTypeInfo attrType, Encoding stringEncoding)
	{
		if (reader.BytesRemaining == 0)
		{
			return DBNull.Value;
		}
		return attrType.ScalarType switch
		{
			OracleUdtComplexScalarType.Date => ParseComplexDate(ReadLengthPrefixedBytes(ref reader)), 
			OracleUdtComplexScalarType.Timestamp => ParseComplexTimestamp(ReadLengthPrefixedBytes(ref reader)), 
			OracleUdtComplexScalarType.Raw => ReadLengthPrefixedBytes(ref reader).ToArray(), 
			OracleUdtComplexScalarType.Varchar2 => stringEncoding.GetString(ReadLengthPrefixedBytes(ref reader)), 
			OracleUdtComplexScalarType.Char => ParseComplexChar(ReadLengthPrefixedBytes(ref reader)), 
			OracleUdtComplexScalarType.Number => ParseComplexNumber(ReadLengthPrefixedBytes(ref reader)), 
			OracleUdtComplexScalarType.IntervalYM => ParseComplexIntervalYm(ReadLengthPrefixedBytes(ref reader)), 
			OracleUdtComplexScalarType.IntervalDS => ParseComplexIntervalDs(ReadLengthPrefixedBytes(ref reader)), 
			OracleUdtComplexScalarType.BinaryFloat => ParseComplexBinaryFloat(reader.ReadByteString(4)), 
			OracleUdtComplexScalarType.BinaryDouble => ParseComplexBinaryDouble(reader.ReadByteString(8)), 
			_ => ParseComplexNumberOrString(ReadLengthPrefixedBytes(ref reader), stringEncoding), 
		};
	}

	private static ReadOnlySpan<byte> ReadLengthPrefixedBytes(ref ByteArrayReader reader)
	{
		int length = checked((int)reader.ReadLengthEncodedInteger());
		return reader.ReadByteString(length);
	}

	private static DateTime ParseComplexTimestamp(ReadOnlySpan<byte> bits)
	{
		if (bits.Length < 11)
		{
			throw new FormatException($"Expected at least 11 bytes for TIMESTAMP UDT attribute, but got {bits.Length}.");
		}
		int year = (bits[0] & 0xFF) * 100 + (bits[1] & 0xFF);
		byte month = bits[2];
		byte day = bits[3];
		byte hour = bits[4];
		byte minute = bits[5];
		byte second = bits[6];
		int num;
		if (bits.Length <= 7)
		{
			num = 0;
		}
		else
		{
			num = BinaryPrimitives.ReadInt32LittleEndian(bits.Slice(7, bits.Length - 7));
		}
		int nanoseconds = num;
		return OracleTimeStamp.FromWire(year, month, day, hour, minute, second, nanoseconds, (bits.Length > 11) ? bits[11] : 9).Value;
	}

	private static DateTime ParseComplexDate(ReadOnlySpan<byte> bits)
	{
		if (bits.IsEmpty)
		{
			return default(DateTime);
		}
		ushort year = BinaryPrimitives.ReadUInt16LittleEndian(bits);
		byte month = bits[2];
		byte day = bits[3];
		byte hour = (byte)((bits.Length > 4) ? bits[4] : 0);
		byte minute = (byte)((bits.Length > 5) ? bits[5] : 0);
		byte second = (byte)((bits.Length > 6) ? bits[6] : 0);
		long num = 0L;
		if (bits.Length > 7)
		{
			num = (long)BinaryPrimitives.ReadUInt32LittleEndian(bits.Slice(7, bits.Length - 7)) * 10L;
		}
		return new DateTime(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified).Ticks + num, DateTimeKind.Unspecified);
	}

	private static char ParseComplexChar(ReadOnlySpan<byte> bits)
	{
		if (bits.Length <= 0)
		{
			return '\0';
		}
		return (char)bits[0];
	}

	private static float ParseComplexBinaryFloat(ReadOnlySpan<byte> bits)
	{
		if (bits.Length != 4)
		{
			throw new FormatException($"Expected 4 bytes for BINARY_FLOAT UDT attribute, but got {bits.Length}.");
		}
		byte[] array = bits.ToArray();
		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(array);
		}
		return BitConverter.ToSingle(array, 0);
	}

	private static double ParseComplexBinaryDouble(ReadOnlySpan<byte> bits)
	{
		if (bits.Length != 8)
		{
			throw new FormatException($"Expected 8 bytes for BINARY_DOUBLE UDT attribute, but got {bits.Length}.");
		}
		byte[] array = bits.ToArray();
		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(array);
		}
		return BitConverter.ToDouble(array, 0);
	}

	private static OracleIntervalYM ParseComplexIntervalYm(ReadOnlySpan<byte> bits)
	{
		if (bits.Length != 7)
		{
			throw new FormatException($"Expected 7 bytes for INTERVAL YEAR TO MONTH UDT attribute, but got {bits.Length}.");
		}
		bool isNegative = bits[0] != 0;
		int years = BinaryPrimitives.ReadInt32LittleEndian(bits.Slice(1, bits.Length - 1));
		int months = bits[5];
		return OracleIntervalYM.FromWire(isNegative, years, months);
	}

	private static OracleIntervalDS ParseComplexIntervalDs(ReadOnlySpan<byte> bits)
	{
		if (bits.Length != 14)
		{
			throw new FormatException($"Expected 14 bytes for INTERVAL DAY TO SECOND UDT attribute, but got {bits.Length}.");
		}
		bool isNegative = bits[0] != 0;
		int days = BinaryPrimitives.ReadInt32LittleEndian(bits.Slice(1, bits.Length - 1));
		byte hours = bits[5];
		byte minutes = bits[6];
		byte seconds = bits[7];
		int fractionalNanoseconds = BinaryPrimitives.ReadInt32LittleEndian(bits.Slice(8, bits.Length - 8));
		byte scale = bits[13];
		return OracleIntervalDS.FromWire(isNegative, days, hours, minutes, seconds, fractionalNanoseconds, scale);
	}

	internal static bool TryParseIntervalYmWireHex(ReadOnlySpan<byte> bits, out OracleIntervalYM interval)
	{
		interval = OracleIntervalYM.Null;
		if (bits.Length != 7)
		{
			return false;
		}
		try
		{
			interval = ParseComplexIntervalYm(bits);
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	internal static bool TryParseIntervalDsWireHex(ReadOnlySpan<byte> bits, out OracleIntervalDS interval)
	{
		interval = OracleIntervalDS.Null;
		if (bits.Length != 14)
		{
			return false;
		}
		try
		{
			interval = ParseComplexIntervalDs(bits);
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	private static object ParseComplexNumber(ReadOnlySpan<byte> bits)
	{
		string text = Encoding.UTF8.GetString(bits).Trim();
		if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
		{
			return result2;
		}
		if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var result3))
		{
			return result3;
		}
		if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result4))
		{
			return result4;
		}
		return text;
	}

	private static object ParseComplexNumberOrString(ReadOnlySpan<byte> bits, Encoding stringEncoding)
	{
		string text = stringEncoding.GetString(bits);
		if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
		{
			return result2;
		}
		if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var result3))
		{
			return result3;
		}
		if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result4))
		{
			return result4;
		}
		return text;
	}
}
