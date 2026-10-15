namespace OceanBase.Core;

/// <summary>
/// <see cref="T:OceanBase.Core.IMySqlCommand" /> provides an internal abstraction over <see cref="T:OceanBase.MySqlCommand" /> and <see cref="T:OceanBase.MySqlBatchCommand" />.
/// </summary>
internal interface IMySqlCommand : ICommand
{
	new MySqlParameterCollection? RawParameters { get; }

	new MySqlConnection? Connection { get; }

	new MySqlParameterCollection? OutParameters { get; set; }

	new MySqlParameter? ReturnParameter { get; set; }
}
