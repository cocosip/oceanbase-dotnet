using System;
using System.Collections.Generic;

namespace OceanBase.Core;

internal sealed class PreparedStatements(IReadOnlyList<PreparedStatement> preparedStatements, ParsedStatements parsedStatements) : IDisposable
{
	public IReadOnlyList<PreparedStatement> Statements { get; } = preparedStatements;

	public void Dispose()
	{
		parsedStatements?.Dispose();
		parsedStatements = null;
	}
}
