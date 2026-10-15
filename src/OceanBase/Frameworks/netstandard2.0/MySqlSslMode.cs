namespace OceanBase;

/// <summary>
/// SSL connection options.
/// </summary>
public enum MySqlSslMode
{
	/// <summary>
	/// Do not use SSL.
	/// </summary>
	None = 0,
	/// <summary>
	/// Do not use SSL. This is the same as <see cref="F:OceanBase.MySqlSslMode.None" />.
	/// </summary>
	Disabled = 0,
	/// <summary>
	/// Use SSL if the server supports it.
	/// </summary>
	Preferred = 1,
	/// <summary>
	/// Always use SSL. Deny connection if server does not support SSL.
	/// </summary>
	Required = 2,
	/// <summary>
	///  Always use SSL. Validate the Certificate Authority but tolerate name mismatch.
	/// </summary>
	VerifyCA = 3,
	/// <summary>
	/// Always use SSL. Fail if the host name is not correct.
	/// </summary>
	VerifyFull = 4
}
