using System;
using System.Globalization;
using System.Text;

namespace OceanBase.Logging;

public class ConsoleLoggerProvider : IMySqlConnectorLoggerProvider
{
	private sealed class ConsoleLogger(ConsoleLoggerProvider provider, string name) : IMySqlConnectorLogger
	{
		private static readonly string[] s_levels = new string[7] { "", "[TRACE]", "[DEBUG]", "[INFO]", "[WARN]", "[ERROR]", "[FATAL]" };

		private static readonly ConsoleColor[] s_colors = new ConsoleColor[7]
		{
			ConsoleColor.Black,
			ConsoleColor.DarkGray,
			ConsoleColor.Gray,
			ConsoleColor.White,
			ConsoleColor.Yellow,
			ConsoleColor.Red,
			ConsoleColor.Red
		};

		private ConsoleLoggerProvider Provider { get; } = provider;

		private string Name { get; } = name;

		public bool IsEnabled(MySqlConnectorLogLevel level)
		{
			if (level >= Provider.m_minimumLevel)
			{
				return level <= MySqlConnectorLogLevel.Fatal;
			}
			return false;
		}

		public void Log(MySqlConnectorLogLevel level, string message, object?[]? args = null, Exception? exception = null)
		{
			if (!IsEnabled(level))
			{
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(s_levels[(int)level]);
			stringBuilder.Append('\t');
			stringBuilder.Append(Name);
			stringBuilder.Append('\t');
			if (args == null || args.Length == 0)
			{
				stringBuilder.Append(message);
			}
			else
			{
				stringBuilder.AppendFormat(CultureInfo.InvariantCulture, message, args);
			}
			stringBuilder.AppendLine();
			if (exception != null)
			{
				stringBuilder.AppendLine(exception.ToString());
			}
			if (Provider.m_isColored)
			{
				lock (Provider)
				{
					ConsoleColor foregroundColor = Console.ForegroundColor;
					Console.ForegroundColor = s_colors[(int)level];
					Console.Error.Write(stringBuilder.ToString());
					Console.ForegroundColor = foregroundColor;
					return;
				}
			}
			Console.Error.Write(stringBuilder.ToString());
		}
	}

	private readonly MySqlConnectorLogLevel m_minimumLevel;

	private readonly bool m_isColored;

	public ConsoleLoggerProvider(MySqlConnectorLogLevel minimumLevel = MySqlConnectorLogLevel.Info, bool isColored = true)
	{
		if ((minimumLevel < MySqlConnectorLogLevel.Trace || minimumLevel > MySqlConnectorLogLevel.Fatal) ? true : false)
		{
			throw new ArgumentOutOfRangeException("minimumLevel", "minimumLevel must be between Trace and Fatal");
		}
		m_minimumLevel = minimumLevel;
		m_isColored = isColored;
	}

	public IMySqlConnectorLogger CreateLogger(string name)
	{
		return new ConsoleLogger(this, name);
	}
}
