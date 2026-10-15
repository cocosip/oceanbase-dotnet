using System;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.MySqlAttribute" /> represents an attribute that can be sent with a MySQL query.
/// </summary>
/// <remarks>See <a href="https://dev.mysql.com/doc/refman/8.0/en/query-attributes.html">Query Attributes</a> for information on using query attributes.</remarks>
public sealed class MySqlAttribute(string attributeName, object? value) : ICloneable
{
	/// <summary>
	/// Gets or sets the attribute name.
	/// </summary>
	public string AttributeName { get; set; } = attributeName ?? "";

	/// <summary>
	/// Gets or sets the attribute value.
	/// </summary>
	public object? Value { get; set; } = value;

	/// <summary>
	/// Initializes a new <see cref="T:OceanBase.MySqlAttribute" />.
	/// </summary>
	public MySqlAttribute()
		: this("", null)
	{
	}

	/// <summary>
	/// Returns a new <see cref="T:OceanBase.MySqlAttribute" /> with the same property values as this instance.
	/// </summary>
	public MySqlAttribute Clone()
	{
		return new MySqlAttribute(AttributeName, Value);
	}

	object ICloneable.Clone()
	{
		return Clone();
	}

	internal MySqlParameter ToParameter()
	{
		if (string.IsNullOrEmpty(AttributeName))
		{
			throw new InvalidOperationException("AttributeName must not be null or empty");
		}
		return new MySqlParameter(AttributeName, Value);
	}

	internal OracleParameter ToOracleParameter()
	{
		if (string.IsNullOrEmpty(AttributeName))
		{
			throw new InvalidOperationException("AttributeName must not be null or empty");
		}
		return new OracleParameter(AttributeName, Value);
	}
}
