using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

/// <summary>
/// Payload creator for OceanBase Oracle-mode REF CURSOR fetch using COM_STMT_FETCH (0x1C).
/// </summary>
internal sealed class RefCursorFetchPayloadCreator(int cursorId, int rowsToFetch) : ICommandPayloadCreator
{
	internal static void WriteFetch(ByteBufferWriter writer, int cursorId, int rowsToFetch)
	{
		writer.Write((byte)28);
		writer.Write(cursorId);
		writer.Write(rowsToFetch);
		writer.Write((ushort)0);
		writer.Write(0);
		writer.Write(0);
	}

	public ValueTask SendCommandPrologueAsync(CommandListPosition commandListPosition, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		return default(ValueTask);
	}

	public bool WriteQueryCommand(ref CommandListPosition commandListPosition, IDictionary<string, CachedProcedure?> cachedProcedures, ByteBufferWriter writer, bool appendSemicolon)
	{
		if (commandListPosition.CommandIndex >= commandListPosition.CommandCount)
		{
			return false;
		}
		commandListPosition.ExpectOraclePrepareExecuteResponse = false;
		commandListPosition.LastUsedPreparedStatement = new PreparedStatement(cursorId, new ParsedStatement
		{
			StatementBytes = default(ArraySegment<byte>)
		}, null, null);
		WriteFetch(writer, cursorId, rowsToFetch);
		commandListPosition.CommandIndex++;
		return true;
	}
}
