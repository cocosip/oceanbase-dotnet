using System;

namespace OceanBase;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class OracleObjectMappingAttribute : Attribute
{
	public string AttributeName { get; }

	public OracleObjectMappingAttribute(string attributeName)
	{
		AttributeName = attributeName ?? throw new ArgumentNullException("attributeName");
	}
}
