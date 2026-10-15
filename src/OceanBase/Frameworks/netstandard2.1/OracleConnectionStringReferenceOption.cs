using System;
using System.Collections.Generic;
using System.Globalization;

namespace OceanBase;

internal sealed class OracleConnectionStringReferenceOption<T> : OracleConnectionStringOption where T : class
{
	private readonly T m_defaultValue;

	private readonly Func<T?, T>? m_coerce;

	public OracleConnectionStringReferenceOption(IReadOnlyList<string> keys, T defaultValue, Func<T?, T>? coerce = null)
		: base(keys)
	{
		m_defaultValue = defaultValue;
		m_coerce = coerce;
	}

	public T GetValue(OracleConnectionStringBuilder builder)
	{
		if (!builder.TryGetValue(base.Key, out var value))
		{
			return m_defaultValue;
		}
		return ChangeType(value);
	}

	public void SetValue(OracleConnectionStringBuilder builder, T? value)
	{
		builder.DoSetValue(base.Key, (m_coerce == null) ? ((object)value) : ((object)m_coerce(value)));
	}

	public override object GetObject(OracleConnectionStringBuilder builder)
	{
		return GetValue(builder);
	}

	public override void SetObject(OracleConnectionStringBuilder builder, object value)
	{
		SetValue(builder, ChangeType(value));
	}

	private static T ChangeType(object objectValue)
	{
		return (T)Convert.ChangeType(objectValue, typeof(T), CultureInfo.InvariantCulture);
	}
}
