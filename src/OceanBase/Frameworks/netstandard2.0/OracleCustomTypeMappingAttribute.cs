using System;

namespace OceanBase;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class OracleCustomTypeMappingAttribute : Attribute
{
	public string UdtTypeName { get; }

	public OracleCustomTypeMappingAttribute(string udtTypeName)
	{
		UdtTypeName = udtTypeName ?? throw new ArgumentNullException("udtTypeName");
	}
}
