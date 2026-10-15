namespace OceanBase.Core;

internal interface IServerCapabilities
{
	bool SupportsDeprecateEof { get; }

	bool SupportsSessionTrack { get; }

	bool SupportsProtocol41 { get; }

	bool SupportsTransactions { get; }

	bool UseOracleMode { get; set; }

	bool UseOceanBaseProtocolV2 { get; set; }

	bool EnableFullLinkTrace { get; set; }

	bool UseNewExtraInfo { get; set; }

	bool UseNewResultSetMetadata { get; set; }
}
