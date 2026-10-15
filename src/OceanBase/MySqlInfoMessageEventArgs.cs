using System;
using System.Collections.Generic;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.MySqlInfoMessageEventArgs" /> contains the data supplied to the <see cref="T:OceanBase.MySqlInfoMessageEventHandler" /> event handler.
/// </summary>
public sealed class MySqlInfoMessageEventArgs : EventArgs
{
	/// <summary>
	/// The list of errors being reported.
	/// </summary>
	public IReadOnlyList<MySqlError> Errors { get; }

	internal MySqlInfoMessageEventArgs(IReadOnlyList<MySqlError> errors)
	{
		Errors = errors;
	}
}
