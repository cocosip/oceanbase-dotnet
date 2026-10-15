using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Reflection;

namespace OceanBase.Utilities;

internal static class ActivitySourceHelper
{
	public const string DatabaseConnectionIdTagName = "db.connection_id";

	public const string DatabaseConnectionStringTagName = "db.connection_string";

	public const string DatabaseNameTagName = "db.name";

	public const string DatabaseStatementTagName = "db.statement";

	public const string DatabaseSystemTagName = "db.system";

	public const string DatabaseUserTagName = "db.user";

	public const string NetPeerIpTagName = "net.peer.ip";

	public const string NetPeerNameTagName = "net.peer.name";

	public const string NetPeerPortTagName = "net.peer.port";

	public const string NetTransportTagName = "net.transport";

	public const string ThreadIdTagName = "thread.id";

	public const string DatabaseSystemValue = "mysql";

	public const string NetTransportNamedPipeValue = "pipe";

	public const string NetTransportTcpIpValue = "ip_tcp";

	public const string NetTransportUnixValue = "unix";

	public const string ExecuteActivityName = "Execute";

	public const string OpenActivityName = "Open";

	public static Meter Meter { get; } = new Meter("MySqlConnector", GetVersion());

	private static ActivitySource ActivitySource { get; } = new ActivitySource("MySqlConnector", GetVersion());

	public static Activity? StartActivity(string name, IEnumerable<KeyValuePair<string, object?>>? activityTags = null)
	{
		Activity activity = ActivitySource.StartActivity(name, ActivityKind.Client, default(ActivityContext), activityTags);
		if (activity != null && activity.IsAllDataRequested)
		{
			activity.SetTag("thread.id", Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture));
		}
		return activity;
	}

	public static void SetException(this Activity activity, Exception exception)
	{
		string description = ((exception is MySqlException { ErrorCode: var errorCode }) ? errorCode.ToString() : exception.Message);
		activity.SetStatus(ActivityStatusCode.Error, description);
		ActivityTagsCollection tags = new ActivityTagsCollection
		{
			{
				"exception.type",
				exception.GetType().FullName
			},
			{ "exception.message", exception.Message },
			{
				"exception.stacktrace",
				exception.ToString()
			}
		};
		activity.AddEvent(new ActivityEvent("exception", default(DateTimeOffset), tags));
	}

	public static void CopyTags(IEnumerable<KeyValuePair<string, object?>> tags, Activity? activity)
	{
		if (activity == null || !activity.IsAllDataRequested)
		{
			return;
		}
		foreach (KeyValuePair<string, object> tag in tags)
		{
			activity.SetTag(tag.Key, tag.Value);
		}
	}

	private static string GetVersion()
	{
		return typeof(ActivitySourceHelper).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
	}
}
