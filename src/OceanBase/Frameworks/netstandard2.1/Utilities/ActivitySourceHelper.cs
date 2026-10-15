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
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		Activity activity = ActivitySource.StartActivity(name, (ActivityKind)2, default(ActivityContext), activityTags, (IEnumerable<ActivityLink>)null, default(DateTimeOffset));
		if (activity != null && activity.IsAllDataRequested)
		{
			activity.SetTag("thread.id", (object)Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture));
		}
		return activity;
	}

	public static void SetException(this Activity activity, Exception exception)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected O, but got Unknown
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		string text = ((exception is MySqlException { ErrorCode: var errorCode }) ? errorCode.ToString() : exception.Message);
		activity.SetStatus((ActivityStatusCode)2, text);
		ActivityTagsCollection val = new ActivityTagsCollection();
		val.Add("exception.type", (object)exception.GetType().FullName);
		val.Add("exception.message", (object)exception.Message);
		val.Add("exception.stacktrace", (object)exception.ToString());
		ActivityTagsCollection val2 = val;
		activity.AddEvent(new ActivityEvent("exception", default(DateTimeOffset), val2));
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
