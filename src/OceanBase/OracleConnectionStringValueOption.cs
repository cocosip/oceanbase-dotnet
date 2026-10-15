using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

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
		if (!builder.TryGetValue(base.Key, out object value))
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
				IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
				IFormatProvider provider = invariantCulture;
				DefaultInterpolatedStringHandler handler = new DefaultInterpolatedStringHandler(37, 2, invariantCulture);
				handler.AppendLiteral("Value '");
				handler.AppendFormatted<object>(objectValue);
				handler.AppendLiteral("' not supported for option '");
				handler.AppendFormatted(typeof(T).Name);
				handler.AppendLiteral("'.");
				throw new ArgumentException(string.Create(provider, ref handler), ex);
			}
		}
		try
		{
			return (T)Convert.ChangeType(objectValue, typeof(T), CultureInfo.InvariantCulture);
		}
		catch (Exception innerException)
		{
			IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
			IFormatProvider provider2 = invariantCulture;
			DefaultInterpolatedStringHandler handler2 = new DefaultInterpolatedStringHandler(49, 2, invariantCulture);
			handler2.AppendLiteral("Invalid value '");
			handler2.AppendFormatted<object>(objectValue);
			handler2.AppendLiteral("' for '");
			handler2.AppendFormatted(base.Key);
			handler2.AppendLiteral("' connection string option.");
			throw new ArgumentException(string.Create(provider2, ref handler2), innerException);
		}
	}
}
