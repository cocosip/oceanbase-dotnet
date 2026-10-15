namespace OceanBase.Core;

/// <summary>
/// <see cref="T:OceanBase.Core.IOracleCommand" /> provides an internal abstraction over <see cref="T:OceanBase.OracleCommand" />.
/// </summary>
internal interface IOracleCommand : ICommand
{
	new OracleParameterCollection? RawParameters { get; }

	new OracleConnection? Connection { get; }

	new OracleParameterCollection? OutParameters { get; set; }

	new OracleParameter? ReturnParameter { get; set; }
}
