using System;
using System.Collections.Generic;
using System.Globalization;

namespace OceanBase;

internal sealed class OracleConnectionStringValueOption<T> : OracleConnectionStringOption where T : struct
{
	private readonly T m_defaultValue;

	private readonly Func<T, T>? m_coerce;

	public OracleConnectionStringValueOption(IReadOnlyList<string> keys, T defaultValue, Func<T, T>? coerce = null)
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

	public void SetValue(OracleConnectionStringBuilder builder, T value)
	{
		builder.DoSetValue(base.Key, (m_coerce == null) ? value : m_coerce(value));
	}

	public override object GetObject(OracleConnectionStringBuilder builder)
	{
		return GetValue(builder);
	}

	public override void SetObject(OracleConnectionStringBuilder builder, object value)
	{
		SetValue(builder, ChangeType(value));
	}

	private T ChangeType(object objectValue)
	{
		if (typeof(T) == typeof(bool) && objectValue is string a)
		{
			if (string.Equals(a, "yes", StringComparison.OrdinalIgnoreCase))
			{
				return (T)(object)true;
			}
			if (string.Equals(a, "no", StringComparison.OrdinalIgnoreCase))
			{
				return (T)(object)false;
			}
		}
		if ((typeof(T) == typeof(MySqlLoadBalance) || typeof(T) == typeof(MySqlSslMode) || typeof(T) == typeof(MySqlServerRedirectionMode) || typeof(T) == typeof(MySqlDateTimeKind) || typeof(T) == typeof(MySqlGuidFormat) || typeof(T) == typeof(MySqlConnectionProtocol) || typeof(T) == typeof(MySqlCertificateStoreLocation)) && objectValue is string value)
		{
			try
			{
				return Enum.Parse<T>(value, ignoreCase: true);
			}
			catch (Exception ex) when (!(ex is ArgumentException))
			{
				throw new ArgumentException(FormattableString.Invariant($"Value '{objectValue}' not supported for option '{typeof(T).Name}'."), ex);
			}
		}
		try
		{
			return (T)Convert.ChangeType(objectValue, typeof(T), CultureInfo.InvariantCulture);
		}
		catch (Exception innerException)
		{
			throw new ArgumentException(FormattableString.Invariant($"Invalid value '{objectValue}' for '{base.Key}' connection string option."), innerException);
		}
	}
}
