using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Reflection;
using OceanBase.Protocol;

namespace OceanBase;

public sealed class OracleUdt
{
	private readonly OracleUdtValue m_value;

	internal OracleUdtValue Value => m_value;

	internal OracleUdt(OracleUdtValue value)
	{
		m_value = value;
	}

	public static void SetValue(OracleConnection con, object udt, string attrName, object? value)
	{
		if (attrName == null)
		{
			throw new ArgumentNullException("attrName");
		}
		GetContext(udt).SetValue(attrName, value);
	}

	public static void SetValue(OracleConnection con, object udt, int index, object? value)
	{
		GetContext(udt).SetValue(index, value);
	}

	public static object? GetValue(OracleConnection con, object udt, string attrName)
	{
		if (attrName == null)
		{
			throw new ArgumentNullException("attrName");
		}
		OracleUdtContext context = GetContext(udt);
		object value = context.GetValue(attrName);
		Type customMappingType = context.CustomMappingType;
		if ((object)customMappingType != null)
		{
			return ApplyAttributeMappingCoercion(customMappingType, attrName, value);
		}
		if (IsLikelyRawAttributeName(attrName) && OracleUdtHexBytes.TryCoerceToRawBytes(value, out byte[] bytes))
		{
			return bytes;
		}
		return value;
	}

	public static object? GetValue(OracleConnection con, object udt, int index)
	{
		return GetContext(udt).GetValue(index);
	}

	public static bool IsDBNull(OracleConnection con, object udt, string attrName)
	{
		if (attrName == null)
		{
			throw new ArgumentNullException("attrName");
		}
		return GetContext(udt).IsDBNull(attrName);
	}

	public static bool IsDBNull(OracleConnection con, object udt, int index)
	{
		return GetContext(udt).IsDBNull(index);
	}

	public static void SetDBNull(OracleConnection con, object udt, string attrName)
	{
		if (attrName == null)
		{
			throw new ArgumentNullException("attrName");
		}
		GetContext(udt).SetValue(attrName, DBNull.Value);
	}

	public static void SetDBNull(OracleConnection con, object udt, int index)
	{
		GetContext(udt).SetValue(index, DBNull.Value);
	}

	/// <summary>
	/// Returns whether a parameter or UDT attribute value should be treated as database NULL.
	/// </summary>
	internal static bool IsNullParameterValue(object? value)
	{
		if (value == null || value == DBNull.Value)
		{
			return true;
		}
		if (value is IOracleNullable { IsNull: not false })
		{
			return true;
		}
		if (value is INullable nullable)
		{
			return nullable.IsNull;
		}
		return false;
	}

	internal static bool TryCreateValue(OracleConnection? connection, object value, string? udtTypeName, bool treatAsArray, out OracleUdtValue udtValue)
	{
		if (IsNullParameterValue(value))
		{
			udtValue = null;
			return false;
		}
		if (value is IOracleCustomType oracleCustomType)
		{
			string text = ResolveUdtTypeName(oracleCustomType.GetType(), udtTypeName);
			if (string.IsNullOrWhiteSpace(text))
			{
				udtValue = null;
				return false;
			}
			OracleUdtContext oracleUdtContext = new OracleUdtContext(treatAsArray);
			oracleCustomType.FromCustomObject(connection, oracleUdtContext);
			oracleUdtContext.FillFromMappedMembers(oracleCustomType);
			udtValue = oracleUdtContext.ToValue(text);
			return true;
		}
		if (treatAsArray && value is IEnumerable enumerable && !(enumerable is string))
		{
			string text2 = ResolveUdtTypeName(value.GetType(), udtTypeName);
			if (string.IsNullOrWhiteSpace(text2))
			{
				udtValue = null;
				return false;
			}
			List<object> list = new List<object>();
			foreach (object? item in enumerable)
			{
				list.Add(item);
			}
			udtValue = new OracleUdtValue(text2, isArray: true, list);
			return true;
		}
		udtValue = null;
		return false;
	}

	internal static bool TryCreateArrayValue(IEnumerable enumerable, Type? elementType, out OracleUdtValue udtValue)
	{
		udtValue = null;
		if (enumerable is string)
		{
			return false;
		}
		if ((object)elementType == null)
		{
			elementType = GetElementType(enumerable.GetType());
		}
		if ((object)elementType == null)
		{
			return false;
		}
		if (!OracleUdtTypeRegistry.TryGetArrayTypeNameForElementType(elementType, out string typeName))
		{
			return false;
		}
		List<object> list = new List<object>();
		foreach (object? item in enumerable)
		{
			list.Add(item);
		}
		udtValue = new OracleUdtValue(typeName, isArray: true, list);
		return true;
	}

	internal static bool TryCreateArrayValue(IEnumerable enumerable, out OracleUdtValue udtValue)
	{
		return TryCreateArrayValue(enumerable, null, out udtValue);
	}

	private static OracleUdtContext GetContext(object udt)
	{
		if (udt == null)
		{
			throw new ArgumentNullException("udt");
		}
		if (udt is OracleUdt oracleUdt)
		{
			return GetContext(oracleUdt.Value);
		}
		if (udt is OracleUdtContext result)
		{
			return result;
		}
		throw new InvalidOperationException("Invalid Oracle UDT context.");
	}

	private static OracleUdtContext GetContext(OracleUdtValue udtValue)
	{
		OracleUdtContext oracleUdtContext = new OracleUdtContext(udtValue.IsArray);
		if (udtValue.IsArray)
		{
			object[] value = udtValue.Values.ToArray();
			oracleUdtContext.SetValue(0, value);
		}
		else
		{
			for (int i = 0; i < udtValue.Values.Count; i++)
			{
				oracleUdtContext.SetValue(i, udtValue.Values[i]);
			}
		}
		return oracleUdtContext;
	}

	private static string? ResolveUdtTypeName(Type customType, string? udtTypeName)
	{
		if (!string.IsNullOrWhiteSpace(udtTypeName))
		{
			return udtTypeName;
		}
		OracleCustomTypeMappingAttribute customAttribute = customType.GetTypeInfo().GetCustomAttribute<OracleCustomTypeMappingAttribute>();
		if (customAttribute != null)
		{
			return customAttribute.UdtTypeName;
		}
		return GetElementType(customType)?.GetTypeInfo().GetCustomAttribute<OracleCustomTypeMappingAttribute>()?.UdtTypeName;
	}

	private static Type? GetElementType(Type type)
	{
		if (type.IsArray)
		{
			return type.GetElementType();
		}
		Type type2 = type.GetInterfaces().FirstOrDefault((Type i) => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
		if ((object)type2 != null)
		{
			return type2.GetGenericArguments()[0];
		}
		return null;
	}

	/// <summary>
	/// OceanBase often reports VARRAY columns as <see cref="F:OceanBase.Protocol.ColumnType.OracleStruct" />.
	/// Use client-side registration (<see cref="T:OceanBase.IOracleArrayTypeFactory" /> or
	/// <see cref="T:OceanBase.OracleArrayMappingAttribute" />) to choose the array decode/materialize path.
	/// </summary>
	internal static bool ShouldDecodeAsArray(string typeName, bool wireSaysArray)
	{
		if (wireSaysArray)
		{
			return true;
		}
		if (string.IsNullOrEmpty(typeName))
		{
			return false;
		}
		if (OracleUdtTypeRegistry.TryGetArrayFactoryType(typeName, out Type _))
		{
			return true;
		}
		if (OracleUdtTypeRegistry.TryGetCustomType(typeName, out Type type2))
		{
			return GetMappedMembers(type2).Any((OracleUdtContext.MappedMember m) => m.ArrayMapping != null);
		}
		return false;
	}

	internal static object? CreateObject(OracleConnection? connection, OracleUdtValue value)
	{
		if (ShouldDecodeAsArray(value.TypeName, value.IsArray))
		{
			return CreateArrayObject(connection, value);
		}
		return CreateCustomObject(connection, value);
	}

	private static object? CreateCustomObject(OracleConnection? connection, OracleUdtValue value)
	{
		if (!OracleUdtTypeRegistry.TryGetCustomType(value.TypeName, out Type type))
		{
			return value;
		}
		if (!typeof(IOracleCustomType).IsAssignableFrom(type))
		{
			return value;
		}
		IOracleCustomType oracleCustomType = CreateCustomTypeInstance(type, value.TypeName);
		if (oracleCustomType == null)
		{
			return value;
		}
		OracleUdtContext oracleUdtContext = new OracleUdtContext(value.IsArray);
		PopulateContext(connection, oracleUdtContext, type, value);
		oracleCustomType.ToCustomObject(connection, oracleUdtContext);
		return oracleCustomType;
	}

	private static object? CreateArrayObject(OracleConnection? connection, OracleUdtValue value)
	{
		int count = value.Values.Count;
		Array array = null;
		Array array2;
		if (OracleUdtTypeRegistry.TryGetArrayFactoryType(value.TypeName, out Type type))
		{
			IOracleArrayTypeFactory oracleArrayTypeFactory = (IOracleArrayTypeFactory)Activator.CreateInstance(type);
			if (oracleArrayTypeFactory != null)
			{
				array2 = oracleArrayTypeFactory.CreateArray(count);
				array = oracleArrayTypeFactory.CreateStatusArray(count);
			}
			else
			{
				array2 = new object[count];
			}
		}
		else
		{
			array2 = new object[count];
		}
		for (int i = 0; i < count; i++)
		{
			object obj = value.Values[i];
			if (obj is OracleUdtValue value2)
			{
				obj = CreateObject(connection, value2);
			}
			if (obj == DBNull.Value)
			{
				array2.SetValue(null, i);
				array?.SetValue(OracleUdtStatus.Null, i);
				continue;
			}
			Type elementType = array2.GetType().GetElementType();
			if ((object)elementType != null && obj != null && !elementType.IsInstanceOfType(obj))
			{
				obj = Convert.ChangeType(obj, elementType, CultureInfo.InvariantCulture);
			}
			array2.SetValue(obj, i);
			array?.SetValue(OracleUdtStatus.NotNull, i);
		}
		return array2;
	}

	private static IOracleCustomType? CreateCustomTypeInstance(Type customType, string typeName)
	{
		if (OracleUdtTypeRegistry.TryGetFactoryType(typeName, out Type type))
		{
			IOracleCustomTypeFactory oracleCustomTypeFactory = (IOracleCustomTypeFactory)Activator.CreateInstance(type);
			if (oracleCustomTypeFactory != null)
			{
				return oracleCustomTypeFactory.CreateObject();
			}
		}
		try
		{
			return (IOracleCustomType)Activator.CreateInstance(customType);
		}
		catch
		{
			return null;
		}
	}

	private static void PopulateContext(OracleConnection? connection, OracleUdtContext context, Type customType, OracleUdtValue value)
	{
		context.CustomMappingType = customType;
		if (value.IsArray)
		{
			for (int i = 0; i < value.Values.Count; i++)
			{
				object obj = value.Values[i];
				if (obj is OracleUdtValue value2)
				{
					obj = CreateObject(connection, value2);
				}
				context.SetValue(i, obj);
			}
			return;
		}
		OracleUdtContext.MappedMember[] mappedMembers = GetMappedMembers(customType);
		for (int j = 0; j < Math.Min(mappedMembers.Length, value.Values.Count); j++)
		{
			OracleUdtContext.MappedMember mappedMember = mappedMembers[j];
			object obj2 = value.Values[j];
			if (obj2 is OracleUdtValue value3)
			{
				obj2 = CreateObject(connection, value3);
			}
			obj2 = CoerceAttributeValue(GetMemberClrType(mappedMember.Member), obj2);
			if (mappedMember.ObjectMapping != null)
			{
				context.SetValue(mappedMember.ObjectMapping.AttributeName, obj2);
			}
			else
			{
				context.SetValue(j, obj2);
			}
		}
	}

	private static OracleUdtContext.MappedMember[] GetMappedMembers(Type type)
	{
		List<OracleUdtContext.MappedMember> list = new List<OracleUdtContext.MappedMember>();
		foreach (MemberInfo declaredMember in type.GetTypeInfo().DeclaredMembers)
		{
			if ((declaredMember is FieldInfo || declaredMember is PropertyInfo) && !(declaredMember is PropertyInfo { GetMethod: null }))
			{
				OracleObjectMappingAttribute customAttribute = declaredMember.GetCustomAttribute<OracleObjectMappingAttribute>();
				OracleArrayMappingAttribute customAttribute2 = declaredMember.GetCustomAttribute<OracleArrayMappingAttribute>();
				if (customAttribute != null || customAttribute2 != null)
				{
					list.Add(new OracleUdtContext.MappedMember(declaredMember, customAttribute, customAttribute2));
				}
			}
		}
		return list.OrderBy((OracleUdtContext.MappedMember x) => x.MetadataToken).ToArray();
	}

	/// <summary>
	/// Returns the number of mapped STRUCT attributes for a registered custom type, when known.
	/// Used by the text-protocol UDT literal parser to reconcile over-split constructor bodies.
	/// </summary>
	internal static int? TryGetRegisteredAttributeCount(string typeName)
	{
		if (!OracleUdtTypeRegistry.TryGetCustomType(typeName, out Type type))
		{
			return null;
		}
		return GetMappedMembers(type).Length;
	}

	private static Type? GetMemberClrType(MemberInfo member)
	{
		if (!(member is PropertyInfo propertyInfo))
		{
			if (member is FieldInfo fieldInfo)
			{
				return fieldInfo.FieldType;
			}
			return null;
		}
		return propertyInfo.PropertyType;
	}

	private static object? CoerceAttributeValue(Type? memberType, object? attrValue)
	{
		if ((object)memberType == null)
		{
			return attrValue;
		}
		if (attrValue is DBNull)
		{
			if (memberType == typeof(OracleIntervalYM))
			{
				return OracleIntervalYM.Null;
			}
			if (memberType == typeof(OracleIntervalDS))
			{
				return OracleIntervalDS.Null;
			}
			return attrValue;
		}
		if (attrValue == null)
		{
			return attrValue;
		}
		OracleUdtComplexScalarType oracleUdtComplexScalarType = OracleUdtComplexScalarInference.InferMemberScalarType(memberType);
		bool flag;
		switch (oracleUdtComplexScalarType)
		{
		case OracleUdtComplexScalarType.Raw:
		case OracleUdtComplexScalarType.Timestamp:
		case OracleUdtComplexScalarType.IntervalYM:
		case OracleUdtComplexScalarType.IntervalDS:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return OracleUdtTextScalars.CoerceStructScalarValue(oracleUdtComplexScalarType, attrValue);
		}
		return attrValue;
	}

	internal static object? ApplyAttributeMappingCoercion(Type customType, string attributeName, object? value)
	{
		if (TryGetMemberClrTypeForAttribute(customType, attributeName, out Type memberType))
		{
			return CoerceAttributeValue(memberType, value);
		}
		if (IsLikelyRawAttributeName(attributeName) && OracleUdtHexBytes.TryCoerceToRawBytes(value, out byte[] bytes))
		{
			return bytes;
		}
		return value;
	}

	private static bool IsLikelyRawAttributeName(string attributeName)
	{
		string text = attributeName.Trim().Trim('"');
		if (!text.Equals("RAW_VAL", StringComparison.OrdinalIgnoreCase))
		{
			return text.EndsWith("_RAW", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool TryGetMemberClrTypeForAttribute(Type customType, string attributeName, out Type? memberType)
	{
		memberType = null;
		string b = attributeName.Trim().Trim('"').ToUpperInvariant();
		OracleUdtContext.MappedMember[] mappedMembers = GetMappedMembers(customType);
		for (int i = 0; i < mappedMembers.Length; i++)
		{
			OracleUdtContext.MappedMember mappedMember = mappedMembers[i];
			if (mappedMember.ObjectMapping != null && string.Equals(mappedMember.ObjectMapping.AttributeName.Trim().Trim('"').ToUpperInvariant(), b, StringComparison.OrdinalIgnoreCase))
			{
				memberType = GetMemberClrType(mappedMember.Member);
				return (object)memberType != null;
			}
		}
		return false;
	}
}
