using System.Collections.Generic;

namespace OceanBase;

internal sealed class OracleUdtValue
{
	public string TypeName { get; }

	public bool IsArray { get; }

	public IReadOnlyList<object?> Values { get; }

	public OracleUdtValue(string typeName, bool isArray, IReadOnlyList<object?> values)
	{
		TypeName = typeName;
		IsArray = isArray;
		Values = values;
	}
}
