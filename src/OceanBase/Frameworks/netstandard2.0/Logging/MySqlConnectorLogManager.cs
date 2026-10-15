using System;
using Microsoft.Extensions.Logging;

namespace OceanBase.Logging;

/// <summary>
/// Controls logging for OceanBase.
/// </summary>
public static class MySqlConnectorLogManager
{
	private sealed class MySqlConnectorLoggerFactory(IMySqlConnectorLoggerProvider loggerProvider) : ILoggerFactory, IDisposable
	{
		public void AddProvider(ILoggerProvider provider)
		{
			throw new NotSupportedException();
		}

		public ILogger CreateLogger(string categoryName)
		{
			return new MySqlConnectorLogger(loggerProvider.CreateLogger(categoryName.Substring(15, categoryName.Length - 15)));
		}

		public void Dispose()
		{
		}
	}

	private sealed class MySqlConnectorLogger(IMySqlConnectorLogger logger) : ILogger
	{
		public IDisposable BeginScope<TState>(TState state) where TState : notnull
		{
			throw new NotSupportedException();
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return logger.IsEnabled(ConvertLogLevel(logLevel));
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			logger.Log(ConvertLogLevel(logLevel), formatter(state, exception), null, exception);
		}

		private static MySqlConnectorLogLevel ConvertLogLevel(LogLevel logLevel)
		{
			return logLevel switch
			{
				LogLevel.Trace => MySqlConnectorLogLevel.Trace, 
				LogLevel.Debug => MySqlConnectorLogLevel.Debug, 
				LogLevel.Information => MySqlConnectorLogLevel.Info, 
				LogLevel.Warning => MySqlConnectorLogLevel.Warn, 
				LogLevel.Error => MySqlConnectorLogLevel.Error, 
				LogLevel.Critical => MySqlConnectorLogLevel.Fatal, 
				_ => MySqlConnectorLogLevel.Info, 
			};
		}
	}

	/// <summary>
	/// Allows the <see cref="T:OceanBase.Logging.IMySqlConnectorLoggerProvider" /> to be set for this library. <see cref="P:OceanBase.Logging.MySqlConnectorLogManager.Provider" /> can
	/// be set once, and must be set before any other library methods are used.
	/// </summary>
	[Obsolete("Use UseLoggerFactory or AddMySqlDataSource instead. See https://mysqlconnector.net/diagnostics/logging/.")]
	public static IMySqlConnectorLoggerProvider Provider
	{
		set
		{
			MySqlConnectorLoggingConfiguration.GlobalConfiguration = new MySqlConnectorLoggingConfiguration(new MySqlConnectorLoggerFactory(value));
		}
	}
}
