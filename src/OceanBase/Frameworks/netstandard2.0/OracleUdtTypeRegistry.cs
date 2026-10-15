using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace OceanBase;

internal static class OracleUdtTypeRegistry
{
	private static readonly ConcurrentDictionary<string, Type> s_typeMap = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

	private static readonly ConcurrentDictionary<string, Type> s_factoryMap = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

	private static readonly ConcurrentDictionary<string, Type> s_arrayFactoryMap = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

	private static readonly ConcurrentDictionary<Type, string> s_elementTypeToArrayTypeName = new ConcurrentDictionary<Type, string>();

	public static bool TryGetCustomType(string udtTypeName, [NotNullWhen(true)] out Type? type)
	{
		if (s_typeMap.TryGetValue(NormalizeUdtTypeName(udtTypeName), out type))
		{
			return true;
		}
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			Type[] array;
			try
			{
				array = assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				array = ex.Types.Where((Type t) => (object)t != null).ToArray();
			}
			Type[] array2 = array;
			foreach (Type type2 in array2)
			{
				if (typeof(IOracleCustomType).IsAssignableFrom(type2) && !type2.IsAbstract && !type2.IsInterface)
				{
					OracleCustomTypeMappingAttribute customAttribute = type2.GetCustomAttribute<OracleCustomTypeMappingAttribute>();
					if (customAttribute != null && TypeNameMatches(NormalizeUdtTypeName(customAttribute.UdtTypeName), NormalizeUdtTypeName(udtTypeName)))
					{
						s_typeMap[NormalizeUdtTypeName(udtTypeName)] = type2;
						s_typeMap[NormalizeUdtTypeName(customAttribute.UdtTypeName)] = type2;
						type = type2;
						return true;
					}
				}
			}
		}
		if (TryGetCustomTypeFromFactory(udtTypeName, out type))
		{
			return true;
		}
		type = null;
		return false;
	}

	private static bool TryGetCustomTypeFromFactory(string udtTypeName, [NotNullWhen(true)] out Type? type)
	{
		if (!TryGetFactoryType(udtTypeName, out Type type2))
		{
			type = null;
			return false;
		}
		IOracleCustomType oracleCustomType = ((IOracleCustomTypeFactory)Activator.CreateInstance(type2))?.CreateObject();
		if (oracleCustomType == null)
		{
			type = null;
			return false;
		}
		type = oracleCustomType.GetType();
		string key = NormalizeUdtTypeName(udtTypeName);
		s_typeMap[key] = type;
		return true;
	}

	public static bool TryGetFactoryType(string udtTypeName, [NotNullWhen(true)] out Type? type)
	{
		if (s_factoryMap.TryGetValue(NormalizeUdtTypeName(udtTypeName), out type))
		{
			return true;
		}
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			Type[] array;
			try
			{
				array = assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				array = ex.Types.Where((Type t) => (object)t != null).ToArray();
			}
			Type[] array2 = array;
			foreach (Type type2 in array2)
			{
				if (typeof(IOracleCustomTypeFactory).IsAssignableFrom(type2) && !type2.IsAbstract && !type2.IsInterface)
				{
					OracleCustomTypeMappingAttribute customAttribute = type2.GetCustomAttribute<OracleCustomTypeMappingAttribute>();
					if (customAttribute != null && TypeNameMatches(NormalizeUdtTypeName(customAttribute.UdtTypeName), NormalizeUdtTypeName(udtTypeName)))
					{
						s_factoryMap[NormalizeUdtTypeName(udtTypeName)] = type2;
						s_factoryMap[NormalizeUdtTypeName(customAttribute.UdtTypeName)] = type2;
						type = type2;
						return true;
					}
				}
			}
		}
		type = null;
		return false;
	}

	public static bool TryGetArrayFactoryType(string udtTypeName, [NotNullWhen(true)] out Type? type)
	{
		if (s_arrayFactoryMap.TryGetValue(NormalizeUdtTypeName(udtTypeName), out type))
		{
			return true;
		}
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			Type[] array;
			try
			{
				array = assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				array = ex.Types.Where((Type t) => (object)t != null).ToArray();
			}
			Type[] array2 = array;
			foreach (Type type2 in array2)
			{
				if (typeof(IOracleArrayTypeFactory).IsAssignableFrom(type2) && !type2.IsAbstract && !type2.IsInterface)
				{
					OracleCustomTypeMappingAttribute customAttribute = type2.GetCustomAttribute<OracleCustomTypeMappingAttribute>();
					if (customAttribute != null && TypeNameMatches(NormalizeUdtTypeName(customAttribute.UdtTypeName), NormalizeUdtTypeName(udtTypeName)))
					{
						s_arrayFactoryMap[NormalizeUdtTypeName(udtTypeName)] = type2;
						s_arrayFactoryMap[NormalizeUdtTypeName(customAttribute.UdtTypeName)] = type2;
						type = type2;
						return true;
					}
				}
			}
		}
		type = null;
		return false;
	}

	public static bool TryGetArrayTypeNameForElementType(Type elementType, [NotNullWhen(true)] out string? typeName)
	{
		if (TryGetArrayTypeNameForElementTypeFromMap(elementType, out typeName))
		{
			return true;
		}
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			Type[] array;
			try
			{
				array = assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				array = ex.Types.Where((Type t) => (object)t != null).ToArray();
			}
			Type[] array2 = array;
			foreach (Type type in array2)
			{
				if (!typeof(IOracleArrayTypeFactory).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
				{
					continue;
				}
				OracleCustomTypeMappingAttribute customAttribute = type.GetCustomAttribute<OracleCustomTypeMappingAttribute>();
				if (customAttribute == null)
				{
					continue;
				}
				IOracleArrayTypeFactory oracleArrayTypeFactory = (IOracleArrayTypeFactory)Activator.CreateInstance(type);
				if (oracleArrayTypeFactory != null)
				{
					Type elementType2 = oracleArrayTypeFactory.CreateArray(0).GetType().GetElementType();
					if ((object)elementType2 != null && elementType2.IsAssignableFrom(elementType))
					{
						s_arrayFactoryMap[NormalizeUdtTypeName(customAttribute.UdtTypeName)] = type;
						s_elementTypeToArrayTypeName[elementType2] = NormalizeUdtTypeName(customAttribute.UdtTypeName);
						typeName = customAttribute.UdtTypeName;
						return true;
					}
				}
			}
		}
		typeName = null;
		return false;
	}

	private static bool TryGetArrayTypeNameForElementTypeFromMap(Type elementType, [NotNullWhen(true)] out string? typeName)
	{
		if (s_elementTypeToArrayTypeName.TryGetValue(elementType, out typeName))
		{
			return true;
		}
		foreach (KeyValuePair<string, Type> item in s_arrayFactoryMap)
		{
			IOracleArrayTypeFactory oracleArrayTypeFactory = (IOracleArrayTypeFactory)Activator.CreateInstance(item.Value);
			if (oracleArrayTypeFactory != null)
			{
				Type elementType2 = oracleArrayTypeFactory.CreateArray(0).GetType().GetElementType();
				if ((object)elementType2 != null && elementType2.IsAssignableFrom(elementType))
				{
					s_elementTypeToArrayTypeName[elementType2] = item.Key;
					typeName = item.Key;
					return true;
				}
			}
		}
		typeName = null;
		return false;
	}

	private static bool TypeNameMatches(string registeredName, string requestedName)
	{
		if (string.Equals(registeredName, requestedName, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (requestedName.Length > registeredName.Length + 1)
		{
			int length = registeredName.Length;
			int length2 = requestedName.Length;
			int num = length2 - length;
			if (requestedName.Substring(num, length2 - num) == registeredName && requestedName[requestedName.Length - registeredName.Length - 1] == '.')
			{
				return true;
			}
		}
		return false;
	}

	private static string NormalizeUdtTypeName(string udtTypeName)
	{
		return udtTypeName.Trim().Trim(new char[1] { '"' }).ToUpperInvariant();
	}
}
