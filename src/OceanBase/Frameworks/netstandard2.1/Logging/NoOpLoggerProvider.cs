namespace OceanBase.Logging;

/// <summary>
/// Creates loggers that do nothing.
/// </summary>
public sealed class NoOpLoggerProvider : IMySqlConnectorLoggerProvider
{
	/// <summary>
	/// Returns a <see cref="T:OceanBase.Logging.NoOpLogger" />.
	/// </summary>
	public IMySqlConnectorLogger CreateLogger(string name)
	{
		return NoOpLogger.Instance;
	}
}
