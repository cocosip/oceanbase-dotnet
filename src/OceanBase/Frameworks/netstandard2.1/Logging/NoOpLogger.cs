using System;

namespace OceanBase.Logging;

/// <summary>
/// <see cref="T:OceanBase.Logging.NoOpLogger" /> is an implementation of <see cref="T:OceanBase.Logging.IMySqlConnectorLogger" /> that does nothing.
/// </summary>
/// <remarks>This is the default logging implementation unless <see cref="P:OceanBase.Logging.MySqlConnectorLogManager.Provider" /> is set.</remarks>
public sealed class NoOpLogger : IMySqlConnectorLogger
{
	/// <summary>
	/// Returns a singleton instance of <see cref="T:OceanBase.Logging.NoOpLogger" />.
	/// </summary>
	public static IMySqlConnectorLogger Instance { get; } = new NoOpLogger();

	/// <summary>
	/// Returns <c>false</c>.
	/// </summary>
	public bool IsEnabled(MySqlConnectorLogLevel level)
	{
		return false;
	}

	/// <summary>
	/// Ignores the specified log message.
	/// </summary>
	public void Log(MySqlConnectorLogLevel level, string message, object?[]? args = null, Exception? exception = null)
	{
	}
}
