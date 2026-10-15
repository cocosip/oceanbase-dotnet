using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Logging;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Core;

internal sealed class ConcatenatedCommandPayloadCreator : ICommandPayloadCreator
{
	public static ICommandPayloadCreator Instance { get; } = new ConcatenatedCommandPayloadCreator();

	public ValueTask SendCommandPrologueAsync(CommandListPosition commandListPosition, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		return default(ValueTask);
	}

	public bool WriteQueryCommand(ref CommandListPosition commandListPosition, IDictionary<string, CachedProcedure?> cachedProcedures, ByteBufferWriter writer, bool appendSemicolon)
	{
		if (commandListPosition.CommandIndex == commandListPosition.CommandCount)
		{
			return false;
		}
		writer.Write((byte)3);
		ServerSession? session = commandListPosition.CommandAt(commandListPosition.CommandIndex).Session;
		if (session != null && session.SupportsQueryAttributes)
		{
			writer.WriteLengthEncodedInteger(0uL);
			writer.Write((byte)1);
		}
		bool flag;
		do
		{
			CommandFacade command = commandListPosition.CommandAt(commandListPosition.CommandIndex);
			Log.PreparingCommandPayload(command.Logger, command.SessionId, command.CommandText);
			flag = SingleCommandPayloadCreator.WriteQueryPayload(command, cachedProcedures, writer, commandListPosition.CommandIndex < commandListPosition.CommandCount - 1 || appendSemicolon, commandListPosition.CommandIndex == 0, commandListPosition.CommandIndex == commandListPosition.CommandCount - 1);
			commandListPosition.CommandIndex++;
		}
		while (commandListPosition.CommandIndex < commandListPosition.CommandCount && flag);
		return true;
	}
}
