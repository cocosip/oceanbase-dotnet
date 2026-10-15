using System;

namespace OceanBase;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class OracleArrayMappingAttribute : Attribute
{
}
