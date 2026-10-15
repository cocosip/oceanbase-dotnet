using System;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.MySqlError" /> represents an error or warning that occurred during the execution of a SQL statement.
/// </summary>
public sealed class MySqlError
{
	/// <summary>
	/// The error level. This comes from the MySQL Server. Possible values include <c>Note</c>, <c>Warning</c>, and <c>Error</c>.
	/// </summary>
	public string Level { get; }

	/// <summary>
	/// The numeric error code. Prefer to use <see cref="P:OceanBase.MySqlError.ErrorCode" />.
	/// </summary>
	[Obsolete("Use ErrorCode")]
	public int Code { get; }

	/// <summary>
	/// The <see cref="T:OceanBase.MySqlErrorCode" /> for the error or warning.
	/// </summary>
	public MySqlErrorCode ErrorCode { get; }

	/// <summary>
	/// A human-readable description of the error or warning.
	/// </summary>
	public string Message { get; }

	internal MySqlError(string level, int code, string message)
	{
		Level = level;
		Code = code;
		ErrorCode = (MySqlErrorCode)code;
		Message = message;
	}
}
