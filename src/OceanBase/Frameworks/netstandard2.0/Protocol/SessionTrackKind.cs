namespace OceanBase.Protocol;

internal enum SessionTrackKind : byte
{
	/// <summary>
	/// SESSION_TRACK_SYSTEM_VARIABLES: one or more system variables changed
	/// </summary>
	SystemVariables,
	/// <summary>
	/// SESSION_TRACK_SCHEMA: schema changed
	/// </summary>
	Schema,
	/// <summary>
	/// SESSION_TRACK_STATE_CHANGE: "track state change" changed
	/// </summary>
	StateChange,
	/// <summary>
	/// SESSION_TRACK_GTIDS: "track GTIDs" changed
	/// </summary>
	Gtids
}
