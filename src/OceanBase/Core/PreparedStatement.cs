using OceanBase.Protocol.Payloads;

namespace OceanBase.Core;

/// <summary>
/// <see cref="T:OceanBase.Core.PreparedStatement" /> is a statement that has been prepared on the MySQL Server.
/// </summary>
internal sealed class PreparedStatement(int statementId, ParsedStatement statement, ColumnDefinitionPayload[]? columns, ColumnDefinitionPayload[]? parameters)
{
	public int StatementId { get; set; } = statementId;

	public ParsedStatement Statement { get; } = statement;

	public ColumnDefinitionPayload[]? Columns { get; set; } = columns;

	public ColumnDefinitionPayload[]? Parameters { get; set; } = parameters;
}
