using System;
using System.Data.Common;
using System.Transactions;

namespace OceanBase.Core;

internal abstract class EnlistedTransactionBase(Transaction transaction, DbConnection connection, Action unenlistAction, bool usesXaTransactions) : IEnlistmentNotification
{
	private readonly Action m_unenlistAction = unenlistAction;

	public DbConnection Connection { get; set; } = connection;

	public bool IsIdle { get; set; }

	public bool UsesXaTransactions { get; } = usesXaTransactions;

	public bool IsPrepared { get; private set; }

	public string ConnectionString { get; } = connection.ConnectionString;

	public Transaction Transaction { get; private set; } = transaction;

	public void Start()
	{
		OnStart();
		Transaction.EnlistVolatile(this, EnlistmentOptions.None);
	}

	void IEnlistmentNotification.Prepare(PreparingEnlistment preparingEnlistment)
	{
		try
		{
			OnPrepare(preparingEnlistment);
			IsPrepared = true;
			preparingEnlistment.Prepared();
		}
		catch (Exception e)
		{
			preparingEnlistment.ForceRollback(e);
		}
	}

	void IEnlistmentNotification.Commit(Enlistment enlistment)
	{
		OnCommit(enlistment);
		enlistment.Done();
		m_unenlistAction();
	}

	void IEnlistmentNotification.Rollback(Enlistment enlistment)
	{
		OnRollback(enlistment);
		enlistment.Done();
		m_unenlistAction();
	}

	public void InDoubt(Enlistment enlistment)
	{
		throw new NotImplementedException();
	}

	protected abstract void OnStart();

	protected abstract void OnPrepare(PreparingEnlistment enlistment);

	protected abstract void OnCommit(Enlistment enlistment);

	protected abstract void OnRollback(Enlistment enlistment);
}
