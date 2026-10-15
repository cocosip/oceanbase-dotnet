using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OceanBase.Protocol;

internal static class OracleUdtComplexScalarInference
{
	public static IReadOnlyList<OracleUdtComplexScalarType> InferStructAttributeTypes(string typeName, int valueCount)
	{
		if (string.IsNullOrEmpty(typeName) || !OracleUdtTypeRegistry.TryGetCustomType(typeName, out Type type))
		{
			return Enumerable.Repeat(OracleUdtComplexScalarType.Unknown, valueCount).ToArray();
		}
		MemberInfo[] array = (from m in type.GetTypeInfo().DeclaredMembers
			where (m is FieldInfo || m is PropertyInfo) ? true : false
			where m.GetCustomAttribute<OracleObjectMappingAttribute>() != null || m.GetCustomAttribute<OracleArrayMappingAttribute>() != null
			orderby m.MetadataToken
			select m).ToArray();
		List<OracleUdtComplexScalarType> list = new List<OracleUdtComplexScalarType>(valueCount);
		MemberInfo[] array2 = array;
		foreach (MemberInfo memberInfo in array2)
		{
			if (memberInfo.GetCustomAttribute<OracleArrayMappingAttribute>() == null)
			{
				Type type2 = ((memberInfo is PropertyInfo propertyInfo) ? propertyInfo.PropertyType : ((memberInfo is FieldInfo fieldInfo) ? fieldInfo.FieldType : null));
				if ((object)type2 == null)
				{
					list.Add(OracleUdtComplexScalarType.Unknown);
				}
				else if (typeof(IOracleCustomType).IsAssignableFrom(type2))
				{
					list.Add(OracleUdtComplexScalarType.Unknown);
				}
				else
				{
					list.Add(InferMemberScalarType(type2));
				}
			}
		}
		while (list.Count < valueCount)
		{
			list.Add(OracleUdtComplexScalarType.Unknown);
		}
		return list;
	}

	/// <summary>
	/// Infers wire scalar type from a mapped member type (handles <see cref="T:System.Byte" />[] RAW vs array UDT).
	/// </summary>
	public static OracleUdtComplexScalarType InferMemberScalarType(Type memberType)
	{
		if (memberType == typeof(byte[]))
		{
			return OracleUdtComplexScalarType.Raw;
		}
		if (memberType.IsArray)
		{
			Type elementType = memberType.GetElementType();
			if ((object)elementType != null && OracleUdtTypeRegistry.TryGetArrayTypeNameForElementType(elementType, out string _))
			{
				return OracleUdtComplexScalarType.Unknown;
			}
		}
		return InferFromClrType(memberType);
	}

	public static OracleUdtComplexScalarType InferFromClrType(Type memberType)
	{
		if (memberType == typeof(byte[]))
		{
			return OracleUdtComplexScalarType.Raw;
		}
		if (memberType == typeof(string))
		{
			return OracleUdtComplexScalarType.Varchar2;
		}
		if (memberType == typeof(char))
		{
			return OracleUdtComplexScalarType.Char;
		}
		if (memberType == typeof(DateTime) || memberType == typeof(DateTimeOffset) || memberType == typeof(OracleTimeStamp) || memberType == typeof(OracleTimeStampLTZ) || memberType == typeof(OracleTimeStampTZ))
		{
			return OracleUdtComplexScalarType.Timestamp;
		}
		if (memberType == typeof(OracleIntervalYM))
		{
			return OracleUdtComplexScalarType.IntervalYM;
		}
		if (memberType == typeof(OracleIntervalDS))
		{
			return OracleUdtComplexScalarType.IntervalDS;
		}
		if (memberType == typeof(float))
		{
			return OracleUdtComplexScalarType.BinaryFloat;
		}
		if (memberType == typeof(double))
		{
			return OracleUdtComplexScalarType.BinaryDouble;
		}
		if (memberType == typeof(bool))
		{
			return OracleUdtComplexScalarType.Varchar2;
		}
		if (memberType == typeof(decimal) || memberType.IsEnum || memberType == typeof(byte) || memberType == typeof(sbyte) || memberType == typeof(short) || memberType == typeof(ushort) || memberType == typeof(int) || memberType == typeof(uint) || memberType == typeof(long) || memberType == typeof(ulong))
		{
			return OracleUdtComplexScalarType.Number;
		}
		return OracleUdtComplexScalarType.Unknown;
	}
}
