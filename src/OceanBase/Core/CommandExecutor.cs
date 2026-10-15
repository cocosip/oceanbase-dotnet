using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Logging;
using OceanBase.Protocol;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase.Core;

internal static class CommandExecutor
{
	public static async ValueTask<MySqlDataReader> ExecuteMysqlReaderAsync(CommandListPosition commandListPosition, ICommandPayloadCreator payloadCreator, CommandBehavior behavior, Activity? activity, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		return await ExecuteReaderCoreAsync<MySqlDataReader>(commandListPosition, payloadCreator, behavior, activity, ioBehavior, useOracleReader: false, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public static async ValueTask<OracleDataReader> ExecuteOracleReaderAsync(CommandListPosition commandListPosition, ICommandPayloadCreator payloadCreator, CommandBehavior behavior, Activity? activity, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		return await ExecuteReaderCoreAsync<OracleDataReader>(commandListPosition, payloadCreator, behavior, activity, ioBehavior, useOracleReader: true, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static async ValueTask<TReader> ExecuteReaderCoreAsync<TReader>(CommandListPosition commandListPosition, ICommandPayloadCreator payloadCreator, CommandBehavior behavior, Activity? activity, IOBehavior ioBehavior, bool useOracleReader, CancellationToken cancellationToken) where TReader : DbDataReader
	{
		_ = 4;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			CommandFacade command = commandListPosition.CommandAt(0);
			ServerSession session = command.Session;
			Log.CommandExecutorExecuteReader(command.Logger, session.Id, ioBehavior, commandListPosition.CommandCount);
			Dictionary<string, CachedProcedure?> cachedProcedures = null;
			for (int commandIndex = 0; commandIndex < commandListPosition.CommandCount; commandIndex++)
			{
				CommandFacade curCommand = commandListPosition.CommandAt(commandIndex);
				if (curCommand.CommandType == CommandType.StoredProcedure)
				{
					if (cachedProcedures == null)
					{
						cachedProcedures = new Dictionary<string, CachedProcedure>();
					}
					string commandText = curCommand.CommandText;
					if (!cachedProcedures.ContainsKey(commandText))
					{
						Dictionary<string, CachedProcedure?> dictionary = cachedProcedures;
						string key = commandText;
						dictionary.Add(key, await curCommand.GetCachedProcedureAsync(commandText, revalidateMissing: false, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
						command.CancellableCommand.ResetCommandTimeout();
					}
					if (curCommand.IsOracleCommand && cachedProcedures.TryGetValue(commandText, out CachedProcedure value))
					{
						value?.AlignOracleStoredProcedureParameters(curCommand.OracleRawParameters);
					}
				}
				SingleCommandPayloadCreator.ValidateOracleArrayBindingParameters(curCommand);
			}
			await payloadCreator.SendCommandPrologueAsync(commandListPosition, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ByteBufferWriter byteBufferWriter = (session.UseOracleMode ? new ByteBufferWriter(0, session.CharacterEncoding) : new ByteBufferWriter());
			if (!payloadCreator.WriteQueryCommand(ref commandListPosition, cachedProcedures, byteBufferWriter, appendSemicolon: false))
			{
				throw new InvalidOperationException("ICommandPayloadCreator failed to write query payload");
			}
			cancellationToken.ThrowIfCancellationRequested();
			using PayloadData payload = byteBufferWriter.ToPayloadData();
			session.StartQuerying(command.CancellableCommand);
			command.SetLastInsertedId(0L);
			try
			{
				await session.SendAsync(payload, ioBehavior, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
				if (useOracleReader)
				{
					OracleDataReader oracleReader = session.OracleDataReader;
					await oracleReader.InitAsync(commandListPosition, payloadCreator, cachedProcedures, command, behavior, activity, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					return (TReader)(DbDataReader)oracleReader;
				}
				MySqlDataReader mysqlReader = session.DataReader;
				await mysqlReader.InitAsync(commandListPosition, payloadCreator, cachedProcedures, command, behavior, activity, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				return (TReader)(DbDataReader)mysqlReader;
			}
			catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.QueryInterrupted && cancellationToken.IsCancellationRequested)
			{
				Log.QueryWasInterrupted(command.Logger, session.Id);
				throw new OperationCanceledException(ex.Message, ex, cancellationToken);
			}
			catch (Exception ex2) when (((Func<bool>)delegate
			{
				// Could not convert BlockContainer to single expression
				bool flag = payload.Span.Length > 4194304;
				if (flag)
				{
					flag = ((ex2 is SocketException || ex2 is IOException || ex2 is MySqlProtocolException) ? true : false);
				}
				return flag;
			}).Invoke())
			{
				int value2 = payload.Span.Length / 1000000;
				throw new MySqlException($"Error submitting {value2}MB packet; ensure 'max_allowed_packet' is greater than {value2}MB.", ex2);
			}
		}
		catch (Exception exception) when (activity?.IsAllDataRequested ?? false)
		{
			activity.SetException(exception);
			activity.Stop();
			throw;
		}
	}
}
