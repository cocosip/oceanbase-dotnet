using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using OceanBase.Utilities;

namespace OceanBase.Core;

internal static class MetricsReporter
{
	private static readonly UpDownCounter<int> s_connectionsUsageCounter;

	private static readonly UpDownCounter<int> s_pendingRequestsCounter;

	private static readonly Counter<int> s_connectionTimeouts;

	private static readonly Histogram<double> s_createTimeHistory;

	private static readonly Histogram<double> s_useTimeHistory;

	private static readonly Histogram<double> s_waitTimeHistory;

	public static void AddIdle(ConnectionPool pool)
	{
		s_connectionsUsageCounter.Add(1, pool.IdleStateTagList);
	}

	public static void RemoveIdle(ConnectionPool pool)
	{
		s_connectionsUsageCounter.Add(-1, pool.IdleStateTagList);
	}

	public static void AddUsed(ConnectionPool pool)
	{
		s_connectionsUsageCounter.Add(1, pool.UsedStateTagList);
	}

	public static void RemoveUsed(ConnectionPool pool)
	{
		s_connectionsUsageCounter.Add(-1, pool.UsedStateTagList);
	}

	public static void AddTimeout(ConnectionPool? pool, ConnectionSettings connectionSettings)
	{
		s_connectionTimeouts.Add(1, new KeyValuePair<string, object>("pool.name", pool?.Name ?? connectionSettings.ApplicationName ?? connectionSettings.ConnectionStringBuilder.GetConnectionString(includePassword: false)));
	}

	public static void RecordCreateTime(ConnectionPool pool, double seconds)
	{
		s_createTimeHistory.Record(seconds, pool.PoolNameTagList);
	}

	public static void RecordUseTime(ConnectionPool pool, double seconds)
	{
		s_useTimeHistory.Record(seconds, pool.PoolNameTagList);
	}

	public static void RecordWaitTime(ConnectionPool pool, double seconds)
	{
		s_waitTimeHistory.Record(seconds, pool.PoolNameTagList);
	}

	public static void AddPendingRequest(ConnectionPool? pool)
	{
		if (pool != null)
		{
			s_pendingRequestsCounter.Add(1, pool.PoolNameTagList);
		}
	}

	public static void RemovePendingRequest(ConnectionPool? pool)
	{
		if (pool != null)
		{
			s_pendingRequestsCounter.Add(-1, pool.PoolNameTagList);
		}
	}

	static MetricsReporter()
	{
		s_connectionsUsageCounter = ActivitySourceHelper.Meter.CreateUpDownCounter<int>("db.client.connections.usage", "{connection}", "The number of connections that are currently in the state described by the state tag.");
		s_pendingRequestsCounter = ActivitySourceHelper.Meter.CreateUpDownCounter<int>("db.client.connections.pending_requests", "{request}", "The number of pending requests for an open connection, cumulative for the entire pool.");
		s_connectionTimeouts = ActivitySourceHelper.Meter.CreateCounter<int>("db.client.connections.timeouts", "{timeout}", "The number of connection timeouts that have occurred trying to obtain a connection from the pool.");
		s_createTimeHistory = ActivitySourceHelper.Meter.CreateHistogram<double>("db.client.connections.create_time", "s", "The time it took to create a new connection.");
		s_useTimeHistory = ActivitySourceHelper.Meter.CreateHistogram<double>("db.client.connections.use_time", "s", "The time between borrowing a connection and returning it to the pool.");
		s_waitTimeHistory = ActivitySourceHelper.Meter.CreateHistogram<double>("db.client.connections.wait_time", "s", "The time it took to obtain an open connection from the pool.");
		ActivitySourceHelper.Meter.CreateObservableUpDownCounter<int>("db.client.connections.idle.max", (Func<IEnumerable<Measurement<int>>>)GetMaximumConnections, "{connection}", "The maximum number of idle open connections allowed; this corresponds to MaximumPoolSize in the connection string.");
		ActivitySourceHelper.Meter.CreateObservableUpDownCounter<int>("db.client.connections.idle.min", (Func<IEnumerable<Measurement<int>>>)GetMinimumConnections, "{connection}", "The minimum number of idle open connections allowed; this corresponds to MinimumPoolSize in the connection string.");
		ActivitySourceHelper.Meter.CreateObservableUpDownCounter<int>("db.client.connections.max", (Func<IEnumerable<Measurement<int>>>)GetMaximumConnections, "{connection}", "The maximum number of open connections allowed; this corresponds to MaximumPoolSize in the connection string.");
		static IEnumerable<Measurement<int>> GetMaximumConnections()
		{
			return from x in ConnectionPool.GetAllPools()
				select new Measurement<int>(x.ConnectionSettings.MaximumPoolSize, x.PoolNameTagList);
		}
		static IEnumerable<Measurement<int>> GetMinimumConnections()
		{
			return from x in ConnectionPool.GetAllPools()
				select new Measurement<int>(x.ConnectionSettings.MinimumPoolSize, x.PoolNameTagList);
		}
	}
}
