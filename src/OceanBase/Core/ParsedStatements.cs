using System;
using System.Collections.Generic;
using OceanBase.Protocol;

namespace OceanBase.Core;

/// <summary>
/// <see cref="T:OceanBase.Core.ParsedStatements" /> wraps a collection of <see cref="T:OceanBase.Core.ParsedStatement" /> objects.
/// It implements <see cref="T:System.IDisposable" /> to return the memory backing the statements to a shared pool.
/// </summary>
internal sealed class ParsedStatements(List<ParsedStatement> statements, PayloadData payloadData) : IDisposable
{
	public IReadOnlyList<ParsedStatement> Statements => statements;

	public void Dispose()
	{
		statements.Clear();
		payloadData.Dispose();
		payloadData = default(PayloadData);
	}
}
