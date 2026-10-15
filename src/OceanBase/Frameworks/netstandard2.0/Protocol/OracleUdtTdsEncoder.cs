using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol;

/// <summary>
/// Encodes an <see cref="T:OceanBase.OracleUdtValue" /> to wire bytes.
/// </summary>
/// <remarks>
/// <para>
/// Binary encoding follows the OceanBase Oracle-mode UDT wire format (mirroring JDBC
/// <c>OBStructParameter#writeBinary</c> and <c>OBArrayParameter#writeBinary</c>).
/// </para>
/// <list type="bullet">
/// <item><description>A text constructor literal (<see cref="M:OceanBase.Protocol.OracleUdtTdsEncoder.WriteLiteralValue(OceanBase.Protocol.Serialization.ByteBufferWriter,OceanBase.OracleUdtValue,System.Boolean)" />) used by the SQL text protocol.</description></item>
/// <item><description>The native binary format (<see cref="M:OceanBase.Protocol.OracleUdtTdsEncoder.WriteBinaryValue(OceanBase.Protocol.Serialization.ByteBufferWriter,OceanBase.OracleUdtValue,System.Text.Encoding)" />) used for binary parameter binding and reading.</description></item>
/// </list>
/// </remarks>
internal static class OracleUdtTdsEncoder
{
	/// <summary>
	/// Writes the UDT value as a UTF-8 constructor literal (e.g. <c>TYPE_NAME(1,'a')</c>).
	/// The output has no length prefix.
	/// </summary>
	public static void WriteLiteralValue(ByteBufferWriter writer, OracleUdtValue value, bool noBackslashEscapes = false)
	{
		WriteLiteralUdtValue(writer, value, noBackslashEscapes);
	}

	/// <summary>
	/// Encodes the UDT value as a UTF-8 constructor literal and returns the bytes (no length prefix).
	/// </summary>
	public static byte[] EncodeLiteralValue(OracleUdtValue value, bool noBackslashEscapes = false)
	{
		ByteBufferWriter byteBufferWriter = new ByteBufferWriter();
		WriteLiteralValue(byteBufferWriter, value, noBackslashEscapes);
		return byteBufferWriter.ArraySegment.AsSpan().ToArray();
	}

	/// <summary>
	/// Writes the UDT value using the OceanBase Oracle-mode binary format.
	/// The output has no length prefix.
	/// </summary>
	/// <param name="writer">The buffer to write encoded bytes into.</param>
	/// <param name="value">The UDT value to encode.</param>
	/// <param name="stringEncoding">
	/// Session character encoding for VARCHAR2/CHAR attributes (from the connection string <c>CharacterSet</c>).
	/// NUMBER attributes always use UTF-8 because the wire format stores them as ASCII-compatible decimal strings
	/// (digits 0-9, sign, decimal point), which are identical in UTF-8 and all single-byte encodings.
	/// This matches JDBC <c>ComplexUtil.storeComplexAttrData</c> behavior.
	/// </param>
	public static void WriteBinaryValue(ByteBufferWriter writer, OracleUdtValue value, Encoding? stringEncoding = null)
	{
		if (stringEncoding == null)
		{
			stringEncoding = Encoding.UTF8;
		}
		if (value.IsArray)
		{
			WriteBinaryArray(writer, value, stringEncoding);
		}
		else
		{
			WriteBinaryStruct(writer, value, stringEncoding);
		}
	}

	/// <summary>
	/// Encodes the UDT value using the native binary format and returns the bytes (no length prefix).
	/// </summary>
	public static byte[] EncodeBinaryValue(OracleUdtValue value, Encoding? stringEncoding = null)
	{
		ByteBufferWriter byteBufferWriter = new ByteBufferWriter();
		WriteBinaryValue(byteBufferWriter, value, stringEncoding);
		return byteBufferWriter.ArraySegment.AsSpan().ToArray();
	}

	private static void WriteBinaryStruct(ByteBufferWriter writer, OracleUdtValue value, Encoding stringEncoding)
	{
		int count = value.Values.Count;
		byte[] array = new byte[(count + 7) / 8];
		for (int i = 0; i < count; i++)
		{
			if (IsNullValue(value.Values[i]))
			{
				array[i / 8] |= (byte)(1 << i % 8);
			}
		}
		writer.Write(array);
		IReadOnlyList<OracleUdtComplexScalarType> readOnlyList = OracleUdtComplexScalarInference.InferStructAttributeTypes(value.TypeName, count);
		for (int j = 0; j < count; j++)
		{
			object value2 = value.Values[j];
			if (!IsNullValue(value2))
			{
				OracleUdtComplexScalarType scalarType = ((j < readOnlyList.Count) ? readOnlyList[j] : OracleUdtComplexScalarType.Unknown);
				WriteComplexAttrData(writer, value2, scalarType, stringEncoding);
			}
		}
	}

	private static void WriteBinaryArray(ByteBufferWriter writer, OracleUdtValue value, Encoding stringEncoding)
	{
		int count = value.Values.Count;
		int num = (count + 7) / 8;
		writer.WriteLengthEncodedInteger((ulong)count);
		byte[] array = new byte[num];
		for (int i = 0; i < count; i++)
		{
			if (IsNullValue(value.Values[i]))
			{
				array[i / 8] |= (byte)(1 << i % 8);
			}
		}
		writer.Write(array);
		OracleUdtComplexScalarType scalarType = InferArrayElementScalarType(value.TypeName);
		for (int j = 0; j < count; j++)
		{
			object value2 = value.Values[j];
			if (!IsNullValue(value2))
			{
				WriteComplexAttrData(writer, value2, scalarType, stringEncoding);
			}
		}
	}

	private static OracleUdtComplexScalarType InferArrayElementScalarType(string typeName)
	{
		if (string.IsNullOrEmpty(typeName) || !OracleUdtTypeRegistry.TryGetCustomType(typeName, out Type type))
		{
			return OracleUdtComplexScalarType.Unknown;
		}
		MemberInfo memberInfo = type.GetTypeInfo().DeclaredMembers.Where((MemberInfo m) => (m is FieldInfo || m is PropertyInfo) ? true : false).FirstOrDefault((MemberInfo m) => m.GetCustomAttribute<OracleArrayMappingAttribute>() != null);
		if ((object)memberInfo == null)
		{
			return OracleUdtComplexScalarType.Unknown;
		}
		Type type2 = ((memberInfo is PropertyInfo propertyInfo) ? propertyInfo.PropertyType : ((memberInfo is FieldInfo fieldInfo) ? fieldInfo.FieldType : null));
		if ((object)type2 == null)
		{
			return OracleUdtComplexScalarType.Unknown;
		}
		if (type2.IsArray)
		{
			Type elementType = type2.GetElementType();
			if ((object)elementType != null)
			{
				return OracleUdtComplexScalarInference.InferFromClrType(elementType);
			}
		}
		return OracleUdtComplexScalarInference.InferFromClrType(type2);
	}

	private static bool IsNullValue(object? value)
	{
		return OracleUdt.IsNullParameterValue(value);
	}

	private static void WriteComplexAttrData(ByteBufferWriter writer, object value, OracleUdtComplexScalarType scalarType, Encoding stringEncoding)
	{
		OracleUdtValue udtValue;
		OracleUdtValue udtValue2;
		if (value is OracleUdtValue value2)
		{
			WriteBinaryValue(writer, value2, stringEncoding);
		}
		else if (value is IOracleCustomType value3 && OracleUdt.TryCreateValue(null, value3, null, treatAsArray: false, out udtValue))
		{
			WriteBinaryValue(writer, udtValue, stringEncoding);
		}
		else if (value is IEnumerable enumerable && !(enumerable is string) && OracleUdt.TryCreateArrayValue(enumerable, out udtValue2))
		{
			WriteBinaryValue(writer, udtValue2, stringEncoding);
		}
		else if (scalarType == OracleUdtComplexScalarType.Unknown || !WriteComplexAttrDataByScalarType(writer, value, scalarType, stringEncoding))
		{
			if (value is int num)
			{
				WriteLengthPrefixedUtf8String(writer, num.ToString(CultureInfo.InvariantCulture));
			}
			else if (value is short num2)
			{
				WriteLengthPrefixedUtf8String(writer, num2.ToString(CultureInfo.InvariantCulture));
			}
			else if (value is long num3)
			{
				WriteLengthPrefixedUtf8String(writer, num3.ToString(CultureInfo.InvariantCulture));
			}
			else if (value is decimal num4)
			{
				WriteLengthPrefixedUtf8String(writer, num4.ToString(CultureInfo.InvariantCulture));
			}
			else if (value is float value4)
			{
				writer.Write(GetLittleEndianBytes(value4));
			}
			else if (value is double value5)
			{
				writer.Write(GetLittleEndianBytes(value5));
			}
			else if (value is byte b)
			{
				WriteLengthPrefixedUtf8String(writer, b.ToString(CultureInfo.InvariantCulture));
			}
			else if (value is sbyte b2)
			{
				WriteLengthPrefixedUtf8String(writer, b2.ToString(CultureInfo.InvariantCulture));
			}
			else if (value is ushort num5)
			{
				WriteLengthPrefixedUtf8String(writer, num5.ToString(CultureInfo.InvariantCulture));
			}
			else if (value is uint num6)
			{
				WriteLengthPrefixedUtf8String(writer, num6.ToString(CultureInfo.InvariantCulture));
			}
			else if (value is ulong num7)
			{
				WriteLengthPrefixedUtf8String(writer, num7.ToString(CultureInfo.InvariantCulture));
			}
			else if (value is bool flag)
			{
				WriteLengthPrefixedUtf8String(writer, flag ? "true" : "false");
			}
			else if (value is string s)
			{
				byte[] bytes = stringEncoding.GetBytes(s);
				writer.WriteLengthEncodedInteger((ulong)bytes.Length);
				writer.Write(bytes);
			}
			else if (value is char c)
			{
				byte[] bytes2 = stringEncoding.GetBytes(c.ToString());
				writer.WriteLengthEncodedInteger((ulong)bytes2.Length);
				writer.Write(bytes2);
			}
			else if (value is byte[] array)
			{
				writer.WriteLengthEncodedInteger((ulong)array.Length);
				writer.Write(array);
			}
			else if (value is DateTime value6)
			{
				WriteFixedLengthPrefixedBytes(writer, new OracleTimeStamp(value6).ToBytes());
			}
			else if (value is DateTimeOffset dateTimeOffset)
			{
				WriteFixedLengthPrefixedBytes(writer, new OracleTimeStamp(dateTimeOffset.DateTime).ToBytes());
			}
			else if (value is OracleTimeStamp oracleTimeStamp)
			{
				WriteFixedLengthPrefixedBytes(writer, oracleTimeStamp.ToBytes());
			}
			else if (value is OracleTimeStampLTZ oracleTimeStampLTZ)
			{
				WriteFixedLengthPrefixedBytes(writer, new OracleTimeStamp(oracleTimeStampLTZ.Value).ToBytes());
			}
			else if (value is OracleTimeStampTZ oracleTimeStampTZ)
			{
				WriteFixedLengthPrefixedBytes(writer, oracleTimeStampTZ.GetOracleTimeStamp().ToBytes());
			}
			else if (value is OracleIntervalDS oracleIntervalDS)
			{
				WriteFixedLengthPrefixedBytes(writer, oracleIntervalDS.ToBytes());
			}
			else if (value is OracleIntervalYM oracleIntervalYM)
			{
				WriteFixedLengthPrefixedBytes(writer, oracleIntervalYM.ToBytes());
			}
			else
			{
				string value7 = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
				WriteLengthPrefixedString(writer, value7, stringEncoding);
			}
		}
	}

	private static bool WriteComplexAttrDataByScalarType(ByteBufferWriter writer, object value, OracleUdtComplexScalarType scalarType, Encoding stringEncoding)
	{
		switch (scalarType)
		{
		case OracleUdtComplexScalarType.Number:
			WriteLengthPrefixedUtf8String(writer, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0");
			return true;
		case OracleUdtComplexScalarType.Varchar2:
			if (value is bool flag)
			{
				WriteLengthPrefixedUtf8String(writer, flag ? "true" : "false");
				return true;
			}
			if (value is string s)
			{
				byte[] bytes3 = stringEncoding.GetBytes(s);
				writer.WriteLengthEncodedInteger((ulong)bytes3.Length);
				writer.Write(bytes3);
				return true;
			}
			WriteLengthPrefixedString(writer, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, stringEncoding);
			return true;
		case OracleUdtComplexScalarType.Char:
			if (value is char c)
			{
				byte[] bytes = stringEncoding.GetBytes(c.ToString());
				writer.WriteLengthEncodedInteger((ulong)bytes.Length);
				writer.Write(bytes);
				return true;
			}
			return false;
		case OracleUdtComplexScalarType.Raw:
			if (value is byte[] array)
			{
				writer.WriteLengthEncodedInteger((ulong)array.Length);
				writer.Write(array);
				return true;
			}
			return false;
		case OracleUdtComplexScalarType.Date:
		{
			if (TryGetDateTime(value, out var dateTime))
			{
				WriteDateTimeBinary(writer, dateTime);
				return true;
			}
			return false;
		}
		case OracleUdtComplexScalarType.Timestamp:
		{
			if (TryGetOracleTimestampBytes(value, out byte[] bytes2))
			{
				WriteFixedLengthPrefixedBytes(writer, bytes2);
				return true;
			}
			return false;
		}
		case OracleUdtComplexScalarType.IntervalYM:
			if (value is OracleIntervalYM oracleIntervalYM)
			{
				WriteFixedLengthPrefixedBytes(writer, oracleIntervalYM.ToBytes());
				return true;
			}
			return false;
		case OracleUdtComplexScalarType.IntervalDS:
			if (value is OracleIntervalDS oracleIntervalDS)
			{
				WriteFixedLengthPrefixedBytes(writer, oracleIntervalDS.ToBytes());
				return true;
			}
			return false;
		case OracleUdtComplexScalarType.BinaryFloat:
			if (value is float value3)
			{
				writer.Write(GetLittleEndianBytes(value3));
				return true;
			}
			return false;
		case OracleUdtComplexScalarType.BinaryDouble:
			if (value is double value2)
			{
				writer.Write(GetLittleEndianBytes(value2));
				return true;
			}
			return false;
		default:
			return false;
		}
	}

	private static bool TryGetDateTime(object value, out DateTime dateTime)
	{
		if (!(value is DateTime dateTime2))
		{
			if (!(value is DateTimeOffset dateTimeOffset))
			{
				if (!(value is OracleTimeStamp oracleTimeStamp))
				{
					if (!(value is OracleTimeStampLTZ oracleTimeStampLTZ))
					{
						if (value is OracleTimeStampTZ oracleTimeStampTZ)
						{
							dateTime = oracleTimeStampTZ.Value;
							return true;
						}
						dateTime = default(DateTime);
						return false;
					}
					dateTime = oracleTimeStampLTZ.Value;
					return true;
				}
				dateTime = oracleTimeStamp.Value;
				return true;
			}
			dateTime = dateTimeOffset.DateTime;
			return true;
		}
		dateTime = dateTime2;
		return true;
	}

	private static bool TryGetOracleTimestampBytes(object value, out byte[] bytes)
	{
		if (!(value is OracleTimeStamp oracleTimeStamp))
		{
			if (!(value is DateTime value2))
			{
				if (!(value is DateTimeOffset dateTimeOffset))
				{
					if (!(value is OracleTimeStampLTZ oracleTimeStampLTZ))
					{
						if (value is OracleTimeStampTZ oracleTimeStampTZ)
						{
							bytes = oracleTimeStampTZ.GetOracleTimeStamp().ToBytes();
							return true;
						}
						bytes = Array.Empty<byte>();
						return false;
					}
					bytes = new OracleTimeStamp(oracleTimeStampLTZ.Value).ToBytes();
					return true;
				}
				bytes = new OracleTimeStamp(dateTimeOffset.DateTime).ToBytes();
				return true;
			}
			bytes = new OracleTimeStamp(value2).ToBytes();
			return true;
		}
		bytes = oracleTimeStamp.ToBytes();
		return true;
	}

	private static void WriteLengthPrefixedUtf8String(ByteBufferWriter writer, string value)
	{
		WriteLengthPrefixedString(writer, value, Encoding.UTF8);
	}

	private static void WriteLengthPrefixedString(ByteBufferWriter writer, string value, Encoding encoding)
	{
		byte[] bytes = encoding.GetBytes(value);
		writer.WriteLengthEncodedInteger((ulong)bytes.Length);
		writer.Write(bytes);
	}

	/// <summary>
	/// Writes a fixed-length binary payload prefixed with a single length byte (mirrors <see cref="T:OceanBase.OracleParameter" /> interval/timestamp binary bind).
	/// </summary>
	private static void WriteFixedLengthPrefixedBytes(ByteBufferWriter writer, ReadOnlySpan<byte> bytes)
	{
		writer.Write((byte)bytes.Length);
		writer.Write(bytes);
	}

	private static byte[] GetLittleEndianBytes(float value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse((Array)bytes);
		}
		return bytes;
	}

	private static byte[] GetLittleEndianBytes(double value)
	{
		byte[] bytes = BitConverter.GetBytes(value);
		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse((Array)bytes);
		}
		return bytes;
	}

	private static void WriteDateTimeBinary(ByteBufferWriter writer, DateTime dt)
	{
		bool num = dt.Ticks % 10000000 != 0;
		byte value = (byte)(num ? 11 : 7);
		writer.Write(value);
		writer.Write((ushort)dt.Year);
		writer.Write((byte)dt.Month);
		writer.Write((byte)dt.Day);
		writer.Write((byte)dt.Hour);
		writer.Write((byte)dt.Minute);
		writer.Write((byte)dt.Second);
		if (num)
		{
			int value2 = (int)(dt.Ticks % 10000000 / 10);
			writer.Write((uint)value2);
		}
	}

	private static void WriteLiteralUdtValue(ByteBufferWriter writer, OracleUdtValue value, bool noBackslashEscapes)
	{
		writer.WriteAscii(value.TypeName);
		writer.Write((byte)40);
		IReadOnlyList<OracleUdtComplexScalarType> readOnlyList = (value.IsArray ? null : OracleUdtComplexScalarInference.InferStructAttributeTypes(value.TypeName, value.Values.Count));
		for (int i = 0; i < value.Values.Count; i++)
		{
			if (i != 0)
			{
				writer.Write((byte)44);
			}
			OracleUdtComplexScalarType scalarType = ((readOnlyList != null && i < readOnlyList.Count) ? readOnlyList[i] : OracleUdtComplexScalarType.Unknown);
			WriteLiteralUdtElement(writer, value.Values[i], scalarType, noBackslashEscapes);
		}
		writer.Write((byte)41);
	}

	private static void WriteLiteralUdtElement(ByteBufferWriter writer, object? value, OracleUdtComplexScalarType scalarType, bool noBackslashEscapes)
	{
		OracleUdtValue udtValue;
		OracleUdtValue udtValue2;
		if (OracleUdt.IsNullParameterValue(value))
		{
			writer.Write("NULL"u8);
		}
		else if (value is OracleUdtValue value2)
		{
			WriteLiteralUdtValue(writer, value2, noBackslashEscapes);
		}
		else if (value is IOracleCustomType value3 && OracleUdt.TryCreateValue(null, value3, null, treatAsArray: false, out udtValue))
		{
			WriteLiteralUdtValue(writer, udtValue, noBackslashEscapes);
		}
		else if (value is IEnumerable enumerable && !(enumerable is string) && OracleUdt.TryCreateArrayValue(enumerable, out udtValue2))
		{
			WriteLiteralUdtValue(writer, udtValue2, noBackslashEscapes);
		}
		else
		{
			if (scalarType != OracleUdtComplexScalarType.Unknown && WriteLiteralUdtElementByScalarType(writer, value, scalarType, noBackslashEscapes))
			{
				return;
			}
			if (!(value is string text))
			{
				if (!(value is char c))
				{
					if (!(value is bool flag))
					{
						if (!(value is byte value4))
						{
							if (!(value is sbyte value5))
							{
								if (!(value is short value6))
								{
									if (!(value is ushort value7))
									{
										if (!(value is int value8))
										{
											if (!(value is uint value9))
											{
												if (!(value is long value10))
												{
													if (!(value is ulong value11))
													{
														if (!(value is decimal num))
														{
															if (!(value is float num2))
															{
																if (!(value is double num3))
																{
																	if (!(value is DateTime dateTime))
																	{
																		if (!(value is DateTimeOffset dateTimeOffset))
																		{
																			if (!(value is OracleIntervalDS oracleIntervalDS))
																			{
																				if (!(value is OracleIntervalYM oracleIntervalYM))
																				{
																					if (!(value is OracleTimeStamp oracleTimeStamp))
																					{
																						if (!(value is OracleTimeStampLTZ oracleTimeStampLTZ))
																						{
																							if (!(value is OracleTimeStampTZ oracleTimeStampTZ))
																							{
																								if (value is byte[] array)
																								{
																									writer.Write("hextoraw('"u8);
																									WriteHexLiteral(writer, array);
																									writer.Write("')"u8);
																								}
																								else
																								{
																									WriteStringLiteral(writer, noBackslashEscapes, (Convert.ToString(value, CultureInfo.InvariantCulture) ?? "").AsSpan());
																								}
																							}
																							else
																							{
																								WriteDateTimeLiteral(writer, oracleTimeStampTZ.Value, OracleDbType.TimeStamp, noBackslashEscapes);
																							}
																						}
																						else
																						{
																							WriteDateTimeLiteral(writer, oracleTimeStampLTZ.Value, OracleDbType.TimeStamp, noBackslashEscapes);
																						}
																					}
																					else
																					{
																						WriteDateTimeLiteral(writer, oracleTimeStamp.Value, OracleDbType.TimeStamp, noBackslashEscapes);
																					}
																				}
																				else
																				{
																					writer.WriteAscii(oracleIntervalYM.ToOracleLiteral());
																				}
																			}
																			else
																			{
																				writer.WriteAscii(oracleIntervalDS.ToOracleLiteral());
																			}
																		}
																		else
																		{
																			WriteDateTimeLiteral(writer, dateTimeOffset.UtcDateTime, OracleDbType.TimeStamp, noBackslashEscapes);
																		}
																	}
																	else
																	{
																		WriteDateTimeLiteral(writer, dateTime, OracleDbType.TimeStamp, noBackslashEscapes);
																	}
																}
																else
																{
																	writer.WriteAscii(num3.ToString("R", CultureInfo.InvariantCulture));
																}
															}
															else
															{
																writer.WriteAscii(num2.ToString("R", CultureInfo.InvariantCulture));
															}
														}
														else
														{
															writer.WriteAscii(num.ToString(CultureInfo.InvariantCulture));
														}
													}
													else
													{
														writer.WriteString(value11);
													}
												}
												else
												{
													writer.WriteString(value10);
												}
											}
											else
											{
												writer.WriteString(value9);
											}
										}
										else
										{
											writer.WriteString(value8);
										}
									}
									else
									{
										writer.WriteString(value7);
									}
								}
								else
								{
									writer.WriteString(value6);
								}
							}
							else
							{
								writer.WriteString(value5);
							}
						}
						else
						{
							writer.WriteString(value4);
						}
					}
					else
					{
						WriteStringLiteral(writer, noBackslashEscapes, (flag ? "true" : "false").AsSpan());
					}
				}
				else
				{
					WriteStringLiteral(writer, noBackslashEscapes, c.ToString().AsSpan());
				}
			}
			else
			{
				WriteStringLiteral(writer, noBackslashEscapes, text.AsSpan());
			}
		}
	}

	private static bool WriteLiteralUdtElementByScalarType(ByteBufferWriter writer, object value, OracleUdtComplexScalarType scalarType, bool noBackslashEscapes)
	{
		switch (scalarType)
		{
		case OracleUdtComplexScalarType.Number:
			if (!(value is byte value2))
			{
				if (!(value is sbyte value3))
				{
					if (!(value is short value4))
					{
						if (!(value is ushort value5))
						{
							if (!(value is int value6))
							{
								if (!(value is uint value7))
								{
									if (!(value is long value8))
									{
										if (!(value is ulong value9))
										{
											if (value is decimal num)
											{
												writer.WriteAscii(num.ToString(CultureInfo.InvariantCulture));
												return true;
											}
											writer.WriteAscii(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0");
											return true;
										}
										writer.WriteString(value9);
										return true;
									}
									writer.WriteString(value8);
									return true;
								}
								writer.WriteString(value7);
								return true;
							}
							writer.WriteString(value6);
							return true;
						}
						writer.WriteString(value5);
						return true;
					}
					writer.WriteString(value4);
					return true;
				}
				writer.WriteString(value3);
				return true;
			}
			writer.WriteString(value2);
			return true;
		case OracleUdtComplexScalarType.Varchar2:
			if (value is bool flag)
			{
				WriteStringLiteral(writer, noBackslashEscapes, (flag ? "true" : "false").AsSpan());
				return true;
			}
			if (value is string text)
			{
				WriteStringLiteral(writer, noBackslashEscapes, text.AsSpan());
				return true;
			}
			WriteStringLiteral(writer, noBackslashEscapes, (Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).AsSpan());
			return true;
		case OracleUdtComplexScalarType.Char:
			if (value is char c)
			{
				WriteStringLiteral(writer, noBackslashEscapes, c.ToString().AsSpan());
				return true;
			}
			return false;
		case OracleUdtComplexScalarType.Raw:
			if (value is byte[] array)
			{
				writer.Write("hextoraw('"u8);
				WriteHexLiteral(writer, array);
				writer.Write("')"u8);
				return true;
			}
			return false;
		case OracleUdtComplexScalarType.Timestamp:
			if (value is OracleTimeStamp oracleTimeStamp)
			{
				WriteDateTimeLiteral(writer, oracleTimeStamp.Value, OracleDbType.TimeStamp, noBackslashEscapes);
				return true;
			}
			if (value is DateTime dateTime)
			{
				WriteDateTimeLiteral(writer, dateTime, OracleDbType.TimeStamp, noBackslashEscapes);
				return true;
			}
			if (value is DateTimeOffset dateTimeOffset)
			{
				WriteDateTimeLiteral(writer, dateTimeOffset.DateTime, OracleDbType.TimeStamp, noBackslashEscapes);
				return true;
			}
			if (value is OracleTimeStampLTZ oracleTimeStampLTZ)
			{
				WriteDateTimeLiteral(writer, oracleTimeStampLTZ.Value, OracleDbType.TimeStamp, noBackslashEscapes);
				return true;
			}
			if (value is OracleTimeStampTZ oracleTimeStampTZ)
			{
				WriteDateTimeLiteral(writer, oracleTimeStampTZ.Value, OracleDbType.TimeStamp, noBackslashEscapes);
				return true;
			}
			return false;
		case OracleUdtComplexScalarType.IntervalYM:
			if (value is OracleIntervalYM oracleIntervalYM)
			{
				writer.WriteAscii(oracleIntervalYM.ToOracleLiteral());
				return true;
			}
			return false;
		case OracleUdtComplexScalarType.IntervalDS:
			if (value is OracleIntervalDS oracleIntervalDS)
			{
				writer.WriteAscii(oracleIntervalDS.ToOracleLiteral());
				return true;
			}
			return false;
		case OracleUdtComplexScalarType.BinaryFloat:
			writer.WriteAscii(FormatBinaryFloatLiteral(Convert.ToSingle(value, CultureInfo.InvariantCulture)));
			return true;
		case OracleUdtComplexScalarType.BinaryDouble:
			writer.WriteAscii(FormatBinaryDoubleLiteral(Convert.ToDouble(value, CultureInfo.InvariantCulture)));
			return true;
		default:
			return false;
		}
	}

	private static string FormatBinaryFloatLiteral(float value)
	{
		string text = value.ToString("R", CultureInfo.InvariantCulture);
		if (text.IndexOf('E') >= 0 || text.EndsWith("f", StringComparison.OrdinalIgnoreCase))
		{
			return text;
		}
		return text + "f";
	}

	private static string FormatBinaryDoubleLiteral(double value)
	{
		return value.ToString("R", CultureInfo.InvariantCulture);
	}

	private static void WriteStringLiteral(ByteBufferWriter writer, bool noBackslashEscapes, ReadOnlySpan<char> value)
	{
		writer.Write((byte)39);
		ReadOnlySpan<char> readOnlySpan = value;
		while (true)
		{
			int num = readOnlySpan.IndexOf('\'');
			if (num == -1)
			{
				break;
			}
			writer.Write(readOnlySpan.Slice(0, num));
			writer.Write("''"u8);
			int num2 = num + 1;
			readOnlySpan = readOnlySpan.Slice(num2, readOnlySpan.Length - num2);
		}
		writer.Write(readOnlySpan);
		writer.Write((byte)39);
	}

	private static void WriteHexLiteral(ByteBufferWriter writer, ReadOnlySpan<byte> bytes)
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

	private static void WriteDateTimeLiteral(ByteBufferWriter writer, DateTime dateTime, OracleDbType oracleDbType, bool noBackslashEscapes)
	{
		if (oracleDbType == OracleDbType.Date)
		{
			if (dateTime.TimeOfDay == TimeSpan.Zero)
			{
				writer.Write("date '"u8);
				writer.WriteAscii(dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
				writer.Write((byte)39);
			}
			else
			{
				writer.Write("TO_DATE('"u8);
				writer.WriteAscii(dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
				writer.Write("', 'YYYY-MM-DD HH24:MI:SS')"u8);
			}
		}
		else
		{
			writer.Write("TIMESTAMP "u8);
			WriteStringLiteral(writer, noBackslashEscapes, dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture).AsSpan());
		}
	}
}
