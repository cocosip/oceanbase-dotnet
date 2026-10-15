using System;
using System.Threading;

namespace OceanBase.Core;

internal static class ICancellableCommandExtensions
{
	private static int s_id = 1;

	/// <summary>
	/// Returns a unique ID for all implementations of <see cref="T:OceanBase.Core.ICancellableCommand" />.
	/// </summary>
	public static int GetNextId()
	{
		return Interlocked.Increment(ref s_id);
	}

	/// <summary>
	/// Causes the effective command timeout to be reset back to the value specified by <see cref="P:OceanBase.Core.ICancellableCommand.CommandTimeout" />
	/// plus <see cref="P:OceanBase.MySqlConnectionStringBuilder.CancellationTimeout" />. This allows for the command to time out, a cancellation to attempt
	/// to happen, then the "hard" timeout to occur.
	/// </summary>
	/// <remarks>As per the <a href="https://msdn.microsoft.com/en-us/library/system.data.sqlclient.sqlcommand.commandtimeout.aspx">MSDN documentation</a>,
	/// "This property is the cumulative time-out (for all network packets that are read during the invocation of a method) for all network reads during command
	/// execution or processing of the results. A time-out can still occur after the first row is returned, and does not include user processing time, only network
	/// read time. For example, with a 30 second time out, if Read requires two network packets, then it has 30 seconds to read both network packets. If you call
	/// Read again, it will have another 30 seconds to read any data that it requires."
	/// The <see cref="M:OceanBase.Core.ICancellableCommandExtensions.ResetCommandTimeout(OceanBase.Core.ICancellableCommand)" /> method is called by public ADO.NET API methods to reset the effective time remaining at the beginning of a new
	/// method call.</remarks>
	public static void ResetCommandTimeout(this ICancellableCommand command)
	{
		int? num = command.EffectiveCommandTimeout;
		if (num == int.MaxValue)
		{
			return;
		}
		ServerSession session = command.Session;
		if (session != null)
		{
			if (!num.HasValue)
			{
				int commandTimeout = command.CommandTimeout;
				int cancellationTimeout = session.CancellationTimeout;
				num = (command.EffectiveCommandTimeout = ((commandTimeout != 0 && cancellationTimeout != 0) ? new int?(Math.Min(commandTimeout, Math.Max(1, 2147483 - Math.Max(0, session.CancellationTimeout))) * 1000) : new int?(int.MaxValue)));
			}
			if (num == int.MaxValue)
			{
				session.SetTimeout(int.MaxValue);
			}
			else if (session.CancellationTimeout > 0)
			{
				command.SetTimeout(num.Value);
				session.SetTimeout(num.Value + session.CancellationTimeout * 1000);
			}
			else
			{
				session.SetTimeout(num.Value);
			}
		}
	}
}
