using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Core;

/// <summary>
/// <see cref="T:OceanBase.Core.ConnectionFacade" /> is an internal adapter that exposes a common view over <see cref="P:OceanBase.Core.ConnectionFacade.MySqlConnection" /> and
/// <see cref="P:OceanBase.Core.ConnectionFacade.OracleConnection" />, as well as <see cref="T:OceanBase.Core.IMySqlCommand" /> and <see cref="T:OceanBase.Core.IOracleCommand" /> for shared execution/payload/datareader logic.
/// </summary>
internal readonly struct ConnectionFacade
{
	private readonly MySqlConnection? m_mySqlConnection;

	private readonly OracleConnection? m_oracleConnection;

	private readonly IMySqlCommand? m_mySqlCommand;

	private readonly IOracleCommand? m_oracleCommand;

	public bool IsMySqlConnection => m_mySqlConnection != null;

	public bool IsOracleConnection => m_oracleConnection != null;

	public bool IsMySqlCommand => m_mySqlCommand != null;

	public bool IsOracleCommand => m_oracleCommand != null;

	private MySqlConnection? MySqlConnection => m_mySqlConnection;

	private OracleConnection? OracleConnection => m_oracleConnection;

	public OracleConnection? AsOracleConnection
	{
		get
		{
			OracleConnection? oracleConnection = m_oracleConnection;
			if (oracleConnection == null)
			{
				IOracleCommand? oracleCommand = m_oracleCommand;
				if (oracleCommand == null)
				{
					return null;
				}
				oracleConnection = oracleCommand.Connection;
			}
			return oracleConnection;
		}
	}

	public ServerSession? Session
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.Session;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.Session;
			}
			throw CreateUninitializedException();
		}
	}

	public IOBehavior AsyncIOBehavior
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.AsyncIOBehavior;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.AsyncIOBehavior;
			}
			throw CreateUninitializedException();
		}
	}

	public ConnectionState State
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.State;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.State;
			}
			throw CreateUninitializedException();
		}
	}

	public bool AllowZeroDateTime
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.AllowZeroDateTime;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.AllowZeroDateTime;
			}
			throw CreateUninitializedException();
		}
	}

	public bool TreatTinyAsBoolean
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.TreatTinyAsBoolean;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.TreatTinyAsBoolean;
			}
			throw CreateUninitializedException();
		}
	}

	public bool ConvertZeroDateTime
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.ConvertZeroDateTime;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.ConvertZeroDateTime;
			}
			throw CreateUninitializedException();
		}
	}

	public DateTimeKind DateTimeKind
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.DateTimeKind;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.DateTimeKind;
			}
			throw CreateUninitializedException();
		}
	}

	public bool AllowLoadLocalInfile
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.AllowLoadLocalInfile;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.AllowLoadLocalInfile;
			}
			throw CreateUninitializedException();
		}
	}

	public MySqlSslMode SslMode
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.SslMode;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.SslMode;
			}
			throw CreateUninitializedException();
		}
	}

	public bool SupportsPerQueryVariables
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.SupportsPerQueryVariables;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.SupportsPerQueryVariables;
			}
			throw CreateUninitializedException();
		}
	}

	public MySqlGuidFormat GuidFormat
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.GuidFormat;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.GuidFormat;
			}
			throw CreateUninitializedException();
		}
	}

	public IMySqlConnectorDataSource? DataSource
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.MySqlDataSource;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.OracleDataSource;
			}
			throw CreateUninitializedException();
		}
	}

	public string Database
	{
		get
		{
			MySqlConnection mySqlConnection = MySqlConnection;
			if (mySqlConnection != null)
			{
				return mySqlConnection.Database;
			}
			OracleConnection oracleConnection = OracleConnection;
			if (oracleConnection != null)
			{
				return oracleConnection.Database;
			}
			throw CreateUninitializedException();
		}
	}

	public string SessionId => Session?.Id ?? "";

	public ConnectionState ConnectionState => State;

	public object InnerCommand => ((object)m_mySqlCommand) ?? ((object)(m_oracleCommand ?? throw CreateUninitializedException()));

	public ConnectionFacade(MySqlConnection connection)
	{
		if (connection == null)
		{
			throw new ArgumentNullException("connection");
		}
		m_mySqlConnection = connection;
		m_oracleConnection = null;
		m_mySqlCommand = null;
		m_oracleCommand = null;
	}

	public ConnectionFacade(OracleConnection connection)
	{
		if (connection == null)
		{
			throw new ArgumentNullException("connection");
		}
		m_mySqlConnection = null;
		m_oracleConnection = connection;
		m_mySqlCommand = null;
		m_oracleCommand = null;
	}

	public ConnectionFacade(IMySqlCommand command)
	{
		if (command == null)
		{
			throw new ArgumentNullException("command");
		}
		m_mySqlCommand = command;
		m_oracleCommand = null;
		m_mySqlConnection = command.Connection;
		m_oracleConnection = null;
	}

	public ConnectionFacade(IOracleCommand command)
	{
		if (command == null)
		{
			throw new ArgumentNullException("command");
		}
		m_mySqlCommand = null;
		m_oracleCommand = command;
		m_mySqlConnection = null;
		m_oracleConnection = command.Connection;
	}

	public Task CloseAsync(IOBehavior ioBehavior)
	{
		MySqlConnection mySqlConnection = MySqlConnection;
		if (mySqlConnection != null)
		{
			return mySqlConnection.CloseAsync(ioBehavior);
		}
		OracleConnection oracleConnection = OracleConnection;
		if (oracleConnection != null)
		{
			return oracleConnection.CloseAsync(ioBehavior);
		}
		throw CreateUninitializedException();
	}

	public ValueTask<CachedProcedure?> GetCachedProcedure(string name, bool revalidateMissing, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		MySqlConnection mySqlConnection = MySqlConnection;
		if (mySqlConnection != null)
		{
			return mySqlConnection.GetCachedProcedure(name, revalidateMissing, ioBehavior, cancellationToken).AsValueTask();
		}
		OracleConnection oracleConnection = OracleConnection;
		if (oracleConnection != null)
		{
			return oracleConnection.GetCachedProcedure(name, revalidateMissing, ioBehavior, cancellationToken).AsValueTask();
		}
		throw CreateUninitializedException();
	}

	public Task CloseConnectionAsync(IOBehavior ioBehavior)
	{
		return CloseAsync(ioBehavior);
	}

	public ValueTask<CachedProcedure?> GetCachedProcedureAsync(string name, bool revalidateMissing, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		return GetCachedProcedure(name, revalidateMissing, ioBehavior, cancellationToken);
	}

	private static InvalidOperationException CreateUninitializedException()
	{
		return new InvalidOperationException("ConnectionFacade must be constructed with a MySqlConnection, OracleConnection, IMySqlCommand, or IOracleCommand.");
	}
}
