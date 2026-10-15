using System;
using System.Data;
using System.Data.SqlTypes;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Core;
using OceanBase.Protocol.Payloads;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase;

public sealed class OracleRefCursor : MarshalByRefObject, IDisposable, INullable, IOracleNullable
{
	private readonly OracleConnection? m_connection;

	private readonly int m_cursorId;

	private readonly bool m_isNotNull;

	private OracleDataReader? m_cachedReader;

	private OracleCommand? m_fetchCommand;

	private int m_readerGiven;

	private int m_disposed;

	private int m_rowsToFetchPerRoundTrip = 1024;

	public static OracleRefCursor Null { get; } = new OracleRefCursor();

	public bool IsNull => !m_isNotNull;

	internal int CursorId => m_cursorId;

	/// <summary>
	/// Controls how many rows the server should return per roundtrip for COM_STMT_FETCH.
	/// </summary>
	public int RowsToFetchPerRoundTrip
	{
		get
		{
			return m_rowsToFetchPerRoundTrip;
		}
		set
		{
			if (value <= 0)
			{
				throw new ArgumentOutOfRangeException("value");
			}
			m_rowsToFetchPerRoundTrip = value;
		}
	}

	/// <summary>
	/// Creates a non-null <see cref="T:OceanBase.OracleRefCursor" />. Instances are typically returned from REF CURSOR OUT parameters.
	/// </summary>
	internal OracleRefCursor(OracleConnection connection, int cursorId)
	{
		m_connection = connection ?? throw new ArgumentNullException("connection");
		m_cursorId = cursorId;
		m_isNotNull = true;
	}

	private OracleRefCursor()
	{
		m_isNotNull = false;
		m_cursorId = 0;
		m_connection = null;
	}

	/// <summary>
	/// Returns an <see cref="T:OceanBase.OracleDataReader" /> for this cursor. This may only be called once.
	/// </summary>
	public OracleDataReader GetDataReader()
	{
		if (IsNull)
		{
			throw new InvalidOperationException("Cannot get a data reader for a NULL OracleRefCursor.");
		}
		ThrowIfDisposed();
		if (Interlocked.Exchange(ref m_readerGiven, 1) != 0)
		{
			throw new InvalidOperationException("This OracleRefCursor has already created a DataReader.");
		}
		OracleConnection connection = m_connection;
		if (connection.State != ConnectionState.Open)
		{
			throw new InvalidOperationException("The connection must be open to read from an OracleRefCursor.");
		}
		m_fetchCommand = new OracleCommand("/* REF CURSOR FETCH */", connection)
		{
			CommandType = CommandType.Text
		};
		m_fetchCommand.IsRefCursorFetchCommand = true;
		m_fetchCommand.RefCursorRowsToFetchPerRoundTrip = m_rowsToFetchPerRoundTrip;
		CommandListPosition commandListPosition = new CommandListPosition(m_fetchCommand);
		RefCursorFetchPayloadCreator payloadCreator = new RefCursorFetchPayloadCreator(m_cursorId, m_rowsToFetchPerRoundTrip);
		return m_cachedReader = CommandExecutor.ExecuteOracleReaderAsync(commandListPosition, payloadCreator, CommandBehavior.Default, null, connection.AsyncIOBehavior, CancellationToken.None).AsTask().GetAwaiter()
			.GetResult();
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (Interlocked.Exchange(ref m_disposed, 1) == 0 && disposing)
		{
			Utility.Dispose(ref m_cachedReader);
			TryCloseCursorNoThrow();
			Utility.Dispose(ref m_fetchCommand);
		}
	}

	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref m_disposed) != 0)
		{
			throw new ObjectDisposedException("OracleRefCursor");
		}
	}

	private void TryCloseCursorNoThrow()
	{
		if (m_readerGiven == 0)
		{
			TryCloseCursorNoThrow(m_connection, m_cursorId);
		}
	}

	internal static void TryCloseCursorNoThrow(OracleConnection? connection, int cursorId)
	{
		try
		{
			TryCloseCursorNoThrowAsync(connection, cursorId, IOBehavior.Synchronous, CancellationToken.None).AsTask().GetAwaiter().GetResult();
		}
		catch
		{
		}
	}

	internal static async ValueTask TryCloseCursorNoThrowAsync(OracleConnection? connection, int cursorId, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		if (cursorId != 0 && connection != null && connection.State == ConnectionState.Open && !connection.HasActiveReader)
		{
			ServerSession session = connection.Session;
			if (session != null)
			{
				await session.TryCloseStatementNoThrowAsync(cursorId, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}
}
