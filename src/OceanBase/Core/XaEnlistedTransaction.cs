using System;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Transactions;

namespace OceanBase.Core;

internal sealed class XaEnlistedTransaction : EnlistedTransactionBase
{
	private static int s_currentId;

	private string? m_xid;

	public XaEnlistedTransaction(Transaction transaction, DbConnection connection, Action unenlistAction, bool usesXaTransactions)
		: base(transaction, connection, unenlistAction, usesXaTransactions)
	{
	}

	protected override void OnStart()
	{
		int num = Interlocked.Increment(ref s_currentId);
		m_xid = "'" + base.Transaction.TransactionInformation.LocalIdentifier + "', '" + num.ToString(CultureInfo.InvariantCulture) + "'";
		ExecuteXaCommand("START");
	}

	protected override void OnPrepare(PreparingEnlistment enlistment)
	{
		ExecuteXaCommand("END");
		ExecuteXaCommand("PREPARE");
	}

	protected override void OnCommit(Enlistment enlistment)
	{
		ExecuteXaCommand("COMMIT");
	}

	protected override void OnRollback(Enlistment enlistment)
	{
		if (!base.IsPrepared)
		{
			try
			{
				ExecuteXaCommand("END");
			}
			catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.XARBDeadlock || (ex.ErrorCode == MySqlErrorCode.XAERRemoveFail && ex.Message.Contains("ROLLBACK ONLY")))
			{
			}
		}
		try
		{
			ExecuteXaCommand("ROLLBACK");
		}
		catch (MySqlException ex2) when (ex2.ErrorCode == MySqlErrorCode.XARBDeadlock)
		{
		}
	}

	private void ExecuteXaCommand(string statement)
	{
		using DbCommand dbCommand = base.Connection.CreateCommand();
		dbCommand.CommandText = "XA " + statement + " " + m_xid;
		dbCommand.ExecuteNonQuery();
	}
}
