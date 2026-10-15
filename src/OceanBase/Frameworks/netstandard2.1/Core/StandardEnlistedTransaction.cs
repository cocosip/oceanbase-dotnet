using System;
using System.Data.Common;
using System.Transactions;

namespace OceanBase.Core;

internal sealed class StandardEnlistedTransaction : EnlistedTransactionBase
{
	public StandardEnlistedTransaction(Transaction transaction, DbConnection connection, Action unenlistAction, bool usesXaTransactions)
		: base(transaction, connection, unenlistAction, usesXaTransactions)
	{
	}

	protected override void OnStart()
	{
		string text = base.Transaction.IsolationLevel switch
		{
			IsolationLevel.Serializable => "serializable", 
			IsolationLevel.ReadCommitted => "read committed", 
			IsolationLevel.ReadUncommitted => "read uncommitted", 
			IsolationLevel.RepeatableRead => "repeatable read", 
			IsolationLevel.Snapshot => "repeatable read", 
			IsolationLevel.Chaos => throw new NotSupportedException($"IsolationLevel.{base.Transaction.IsolationLevel} is not supported."), 
			IsolationLevel.Unspecified => "repeatable read", 
			_ => "repeatable read", 
		};
		using DbCommand dbCommand = base.Connection.CreateCommand();
		dbCommand.CommandText = "set session transaction isolation level " + text + ";";
		dbCommand.ExecuteNonQuery();
		string text2 = ((base.Transaction.IsolationLevel == IsolationLevel.Snapshot) ? " with consistent snapshot" : "");
		dbCommand.CommandText = "start transaction" + text2 + ";";
		dbCommand.ExecuteNonQuery();
	}

	protected override void OnPrepare(PreparingEnlistment enlistment)
	{
	}

	protected override void OnCommit(Enlistment enlistment)
	{
		using DbCommand dbCommand = base.Connection.CreateCommand();
		dbCommand.CommandText = "commit;";
		dbCommand.ExecuteNonQuery();
	}

	protected override void OnRollback(Enlistment enlistment)
	{
		using DbCommand dbCommand = base.Connection.CreateCommand();
		dbCommand.CommandText = "rollback;";
		dbCommand.ExecuteNonQuery();
	}
}
