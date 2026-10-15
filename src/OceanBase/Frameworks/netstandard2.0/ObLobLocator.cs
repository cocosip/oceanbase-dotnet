using System;
using System.Data;
using System.Threading;
using OceanBase.Core;

namespace OceanBase;

internal class ObLobLocator : IDisposable
{
	private readonly object m_auxiliaryConnectionLock = new object();

	private ReadOnlyMemory<byte> m_binaryData;

	private long m_payloadSize;

	private object? m_connection;

	private OracleConnection? m_auxiliaryConnection;

	private bool m_isDisposed;

	internal ReadOnlyMemory<byte> BinaryData
	{
		get
		{
			return m_binaryData;
		}
		set
		{
			m_binaryData = value;
		}
	}

	internal long PayloadSize
	{
		get
		{
			return m_payloadSize;
		}
		set
		{
			m_payloadSize = value;
		}
	}

	internal ConnectionFacade? GetConnection()
	{
		object obj = Volatile.Read(ref m_connection);
		if (obj != null)
		{
			return (ConnectionFacade)obj;
		}
		return null;
	}

	internal void SetConnection(ConnectionFacade? connection)
	{
		Volatile.Write(ref m_connection, connection);
	}

	internal ObLobLocator Clone()
	{
		ObLobLocator obLobLocator;
		if (!(this is ObLobLocatorV1 obLobLocatorV))
		{
			if (!(this is ObLobLocatorV2 obLobLocatorV2))
			{
				throw new InvalidOperationException("Unknown LOB locator version.");
			}
			obLobLocator = new ObLobLocatorV2
			{
				Common = obLobLocatorV2.Common
			};
		}
		else
		{
			obLobLocator = new ObLobLocatorV1
			{
				MagicCode = obLobLocatorV.MagicCode,
				Version = obLobLocatorV.Version,
				SnapshotVersion = obLobLocatorV.SnapshotVersion,
				TableId = obLobLocatorV.TableId,
				ColumnId = obLobLocatorV.ColumnId,
				Flags = obLobLocatorV.Flags,
				Option = obLobLocatorV.Option,
				PayloadOffset = obLobLocatorV.PayloadOffset,
				RowId = obLobLocatorV.RowId
			};
		}
		ObLobLocator obLobLocator2 = obLobLocator;
		obLobLocator2.BinaryData = BinaryData;
		obLobLocator2.PayloadSize = PayloadSize;
		obLobLocator2.SetConnection(GetConnection());
		return obLobLocator2;
	}

	internal OracleConnection GetReadConnection(OracleConnection primaryConnection)
	{
		if (!primaryConnection.HasActiveReader)
		{
			return primaryConnection;
		}
		lock (m_auxiliaryConnectionLock)
		{
			OracleConnection auxiliaryConnection = m_auxiliaryConnection;
			if (auxiliaryConnection != null && auxiliaryConnection.State == ConnectionState.Open)
			{
				return auxiliaryConnection;
			}
			m_auxiliaryConnection?.Dispose();
			OracleConnection oracleConnection = primaryConnection.Clone();
			oracleConnection.Open();
			m_auxiliaryConnection = oracleConnection;
			return oracleConnection;
		}
	}

	protected ObLobLocator()
	{
	}

	protected ObLobLocator(ReadOnlyMemory<byte> binaryData, long payloadSize, ConnectionFacade? connection)
	{
		m_binaryData = binaryData;
		m_payloadSize = payloadSize;
		if (connection.HasValue)
		{
			ConnectionFacade valueOrDefault = connection.GetValueOrDefault();
			SetConnection(valueOrDefault);
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (m_isDisposed)
		{
			return;
		}
		if (disposing)
		{
			lock (m_auxiliaryConnectionLock)
			{
				m_auxiliaryConnection?.Dispose();
				m_auxiliaryConnection = null;
			}
			m_connection = null;
			m_binaryData = default(ReadOnlyMemory<byte>);
		}
		m_isDisposed = true;
	}
}
