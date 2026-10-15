using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OceanBase;

internal sealed class OracleUdtContext
{
	internal readonly struct MappedMember(MemberInfo member, OracleObjectMappingAttribute? objectMapping, OracleArrayMappingAttribute? arrayMapping)
	{
		public MemberInfo Member { get; } = member;

		public OracleObjectMappingAttribute? ObjectMapping { get; } = objectMapping;

		public OracleArrayMappingAttribute? ArrayMapping { get; } = arrayMapping;

		public int MetadataToken => Member.MetadataToken;
	}

	private readonly Dictionary<string, int> m_namedIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	private readonly List<KeyValuePair<string, object?>> m_namedValues = new List<KeyValuePair<string, object>>();

	private readonly SortedDictionary<int, object?> m_indexedValues = new SortedDictionary<int, object>();

	private MappedMember[]? m_mappedMembers;

	private MappedMember[] m_mappedObjectOrder = Array.Empty<MappedMember>();

	public bool IsArray { get; }

	internal Type? CustomMappingType { get; set; }

	public OracleUdtContext(bool isArray)
	{
		IsArray = isArray;
	}

	public void SetValue(string attributeName, object? value)
	{
		Type customMappingType = CustomMappingType;
		if ((object)customMappingType != null)
		{
			value = OracleUdt.ApplyAttributeMappingCoercion(customMappingType, attributeName, value);
		}
		string key = NormalizeAttributeName(attributeName);
		if (!m_namedIndex.TryGetValue(key, out var value2))
		{
			value2 = m_namedValues.Count;
			m_namedIndex.Add(key, value2);
			m_namedValues.Add(new KeyValuePair<string, object>(key, value));
		}
		else
		{
			m_namedValues[value2] = new KeyValuePair<string, object>(key, value);
		}
	}

	public void SetValue(int index, object? value)
	{
		if (index < 0)
		{
			throw new ArgumentOutOfRangeException("index");
		}
		m_indexedValues[index] = value;
	}

	public object? GetValue(string attributeName)
	{
		string key = NormalizeAttributeName(attributeName);
		if (!m_namedIndex.TryGetValue(key, out var value))
		{
			return null;
		}
		return m_namedValues[value].Value;
	}

	public object? GetValue(int index)
	{
		if (index < 0)
		{
			throw new ArgumentOutOfRangeException("index");
		}
		if (!m_indexedValues.TryGetValue(index, out object value))
		{
			return null;
		}
		return value;
	}

	public bool IsDBNull(string attributeName)
	{
		return IsDbNullValue(GetValue(attributeName));
	}

	public bool IsDBNull(int index)
	{
		return IsDbNullValue(GetValue(index));
	}

	public void FillFromMappedMembers(IOracleCustomType customType)
	{
		MappedMember[] mappedMembers = GetMappedMembers(customType.GetType());
		for (int i = 0; i < mappedMembers.Length; i++)
		{
			MappedMember mappedMember = mappedMembers[i];
			if (mappedMember.ArrayMapping != null)
			{
				if (m_indexedValues.Count == 0)
				{
					m_indexedValues[0] = GetMemberValue(mappedMember.Member, customType);
				}
			}
			else if (mappedMember.ObjectMapping != null)
			{
				string text = NormalizeAttributeName(mappedMember.ObjectMapping.AttributeName);
				if (!m_namedIndex.ContainsKey(text))
				{
					SetValue(text, GetMemberValue(mappedMember.Member, customType));
				}
			}
		}
	}

	public OracleUdtValue ToValue(string typeName)
	{
		if (IsArray)
		{
			return new OracleUdtValue(typeName, isArray: true, GetArrayValues());
		}
		List<object> list = new List<object>();
		MappedMember[] mappedObjectOrder = m_mappedObjectOrder;
		for (int i = 0; i < mappedObjectOrder.Length; i++)
		{
			MappedMember mappedMember = mappedObjectOrder[i];
			string key = NormalizeAttributeName(mappedMember.ObjectMapping.AttributeName);
			if (m_namedIndex.TryGetValue(key, out var value))
			{
				list.Add(NormalizeStructAttributeValue(m_namedValues[value].Value, mappedMember.Member));
			}
		}
		if (list.Count == 0)
		{
			foreach (KeyValuePair<string, object> namedValue in m_namedValues)
			{
				list.Add(namedValue.Value);
			}
		}
		return new OracleUdtValue(typeName, isArray: false, list);
	}

	private static object? NormalizeStructAttributeValue(object? value, MemberInfo member)
	{
		if (OracleUdt.IsNullParameterValue(value))
		{
			return value;
		}
		if ((value is OracleUdtValue || value is IOracleCustomType) ? true : false)
		{
			return value;
		}
		if (value is IEnumerable enumerable && !(enumerable is string))
		{
			Type memberType = GetMemberType(member);
			Type elementType = (((object)memberType != null) ? GetElementType(memberType) : null);
			if (OracleUdt.TryCreateArrayValue(enumerable, elementType, out OracleUdtValue udtValue))
			{
				return udtValue;
			}
		}
		return value;
	}

	private static Type? GetMemberType(MemberInfo member)
	{
		if (!(member is FieldInfo fieldInfo))
		{
			if (member is PropertyInfo propertyInfo)
			{
				return propertyInfo.PropertyType;
			}
			return null;
		}
		return fieldInfo.FieldType;
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

	private List<object?> GetArrayValues()
	{
		if (m_indexedValues.Count == 1 && m_indexedValues.TryGetValue(0, out object value) && value is IEnumerable enumerable && !(enumerable is string))
		{
			List<object> list = new List<object>();
			{
				foreach (object? item in enumerable)
				{
					list.Add(item);
				}
				return list;
			}
		}
		List<object> list2 = new List<object>();
		foreach (KeyValuePair<int, object> item2 in m_indexedValues.OrderBy<KeyValuePair<int, object>, int>((KeyValuePair<int, object> x) => x.Key))
		{
			list2.Add(item2.Value);
		}
		return list2;
	}

	private MappedMember[] GetMappedMembers(Type type)
	{
		if (m_mappedMembers != null)
		{
			return m_mappedMembers;
		}
		List<MappedMember> list = new List<MappedMember>();
		foreach (MemberInfo declaredMember in type.GetTypeInfo().DeclaredMembers)
		{
			if ((declaredMember is FieldInfo || declaredMember is PropertyInfo) && !(declaredMember is PropertyInfo { GetMethod: null }))
			{
				OracleObjectMappingAttribute customAttribute = declaredMember.GetCustomAttribute<OracleObjectMappingAttribute>();
				OracleArrayMappingAttribute customAttribute2 = declaredMember.GetCustomAttribute<OracleArrayMappingAttribute>();
				if (customAttribute != null || customAttribute2 != null)
				{
					list.Add(new MappedMember(declaredMember, customAttribute, customAttribute2));
				}
			}
		}
		m_mappedMembers = list.OrderBy((MappedMember x) => x.MetadataToken).ToArray();
		m_mappedObjectOrder = m_mappedMembers.Where((MappedMember x) => x.ObjectMapping != null).ToArray();
		return m_mappedMembers;
	}

	private static object? GetMemberValue(MemberInfo member, object instance)
	{
		if (!(member is FieldInfo fieldInfo))
		{
			if (member is PropertyInfo propertyInfo)
			{
				return propertyInfo.GetValue(instance, null);
			}
			return null;
		}
		return fieldInfo.GetValue(instance);
	}

	private static bool IsDbNullValue(object? value)
	{
		return OracleUdt.IsNullParameterValue(value);
	}

	private static string NormalizeAttributeName(string attributeName)
	{
		return attributeName.Trim().Trim('"').ToUpperInvariant();
	}
}
