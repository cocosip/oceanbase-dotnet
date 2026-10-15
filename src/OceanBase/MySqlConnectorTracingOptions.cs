namespace OceanBase;

internal sealed class MySqlConnectorTracingOptions
{
	public bool EnableResultSetHeaderEvent { get; set; }

	public static MySqlConnectorTracingOptions Default { get; } = new MySqlConnectorTracingOptions
	{
		EnableResultSetHeaderEvent = true
	};
}
