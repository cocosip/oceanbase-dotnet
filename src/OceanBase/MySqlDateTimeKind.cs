namespace OceanBase;

/// <summary>
/// The <see cref="T:System.DateTimeKind" /> used when reading <see cref="T:System.DateTime" /> from the database.
/// </summary>
public enum MySqlDateTimeKind
{
	/// <summary>
	/// Use <see cref="F:System.DateTimeKind.Unspecified" /> when reading; allow any <see cref="T:System.DateTimeKind" /> in command parameters.
	/// </summary>
	Unspecified,
	/// <summary>
	/// Use <see cref="F:System.DateTimeKind.Utc" /> when reading; reject <see cref="F:System.DateTimeKind.Local" /> in command parameters.
	/// </summary>
	Utc,
	/// <summary>
	/// Use <see cref="F:System.DateTimeKind.Local" /> when reading; reject <see cref="F:System.DateTimeKind.Utc" /> in command parameters.
	/// </summary>
	Local
}
