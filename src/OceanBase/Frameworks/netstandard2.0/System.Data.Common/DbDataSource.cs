using System.Threading;
using System.Threading.Tasks;

namespace System.Data.Common;

public abstract class DbDataSource : IDisposable
{
	public abstract string ConnectionString { get; }

	public DbConnection CreateConnection()
	{
		return CreateDbConnection();
	}

	public DbConnection OpenConnection()
	{
		return OpenDbConnection();
	}

	public ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return OpenDbConnectionAsync(cancellationToken);
	}

	public DbCommand CreateCommand(string? commandText = null)
	{
		return CreateDbCommand(commandText);
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(continueOnCapturedContext: false);
		Dispose(disposing: false);
		GC.SuppressFinalize(this);
	}

	protected abstract DbConnection CreateDbConnection();

	protected virtual DbConnection OpenDbConnection()
	{
		DbConnection dbConnection = CreateDbConnection();
		try
		{
			dbConnection.Open();
			return dbConnection;
		}
		catch
		{
			dbConnection.Dispose();
			throw;
		}
	}

	protected virtual async ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		DbConnection connection = CreateDbConnection();
		try
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			return connection;
		}
		catch
		{
			connection.Dispose();
			throw;
		}
	}

	protected virtual DbCommand CreateDbCommand(string? commandText = null)
	{
		throw new NotSupportedException();
	}

	protected virtual void Dispose(bool disposing)
	{
	}

	protected virtual ValueTask DisposeAsyncCore()
	{
		return default(ValueTask);
	}
}
