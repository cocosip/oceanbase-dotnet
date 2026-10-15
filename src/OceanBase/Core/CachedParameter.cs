using System;
using System.Data;

namespace OceanBase.Core;

internal sealed class CachedParameter
{
	public int Position { get; }

	public ParameterDirection Direction { get; }

	public string Name { get; }

	public MySqlDbType MySqlDbType { get; }

	public OracleDbType OracleDbType { get; }

	public int Length { get; }

	/// <summary>
	/// Oracle 模式下，存储过程同名重载的标识（来源 ALL_ARGUMENTS.OVERLOAD）。
	/// 同一重载内所有参数共享同一个 Overload 值；无重载时为 null。
	/// 仅用于 Oracle 存储过程参数 align，按重载分组。
	/// </summary>
	public string? Overload { get; }

	/// <summary>
	/// Oracle 模式下，UDT 参数的类型名（来源 ALL_ARGUMENTS.TYPE_NAME）。
	/// 仅当参数为 OBJECT/ARRAY 类型时有值；否则为 null。
	/// </summary>
	public string? UdtTypeName { get; }

	public CachedParameter(int ordinalPosition, string? mode, string name, string dataType, bool unsigned, int length, MySqlGuidFormat guidFormat)
	{
		Position = ordinalPosition;
		if (Position == 0)
		{
			Direction = ParameterDirection.ReturnValue;
		}
		else if (string.Equals(mode, "in", StringComparison.OrdinalIgnoreCase))
		{
			Direction = ParameterDirection.Input;
		}
		else if (string.Equals(mode, "inout", StringComparison.OrdinalIgnoreCase))
		{
			Direction = ParameterDirection.InputOutput;
		}
		else if (string.Equals(mode, "out", StringComparison.OrdinalIgnoreCase))
		{
			Direction = ParameterDirection.Output;
		}
		Name = name;
		MySqlDbType = TypeMapper.Instance.GetMySqlDbType(dataType, unsigned, length, guidFormat);
		Length = length;
	}

	public CachedParameter(int ordinalPosition, string? direction, string name, string dataType, bool unsigned, int length)
		: this(ordinalPosition, direction, name, dataType, unsigned, length, null)
	{
	}

	public CachedParameter(int ordinalPosition, string? direction, string name, string dataType, bool unsigned, int length, string? overload)
		: this(ordinalPosition, direction, name, dataType, unsigned, length, overload, null)
	{
	}

	public CachedParameter(int ordinalPosition, string? direction, string name, string dataType, bool unsigned, int length, string? overload, string? udtTypeName)
	{
		Position = ordinalPosition;
		if (Position == 0)
		{
			Direction = ParameterDirection.ReturnValue;
		}
		else if (string.Equals(direction, "in", StringComparison.OrdinalIgnoreCase))
		{
			Direction = ParameterDirection.Input;
		}
		else if (string.Equals(direction, "inout", StringComparison.OrdinalIgnoreCase))
		{
			Direction = ParameterDirection.InputOutput;
		}
		else if (string.Equals(direction, "out", StringComparison.OrdinalIgnoreCase))
		{
			Direction = ParameterDirection.Output;
		}
		Name = name;
		OracleDbType = TypeMapper.Instance.GetOracleDbType(dataType, unsigned, length);
		Length = length;
		Overload = overload;
		UdtTypeName = udtTypeName;
	}
}
