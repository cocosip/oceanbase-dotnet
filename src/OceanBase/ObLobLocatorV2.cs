namespace OceanBase;

internal sealed class ObLobLocatorV2 : ObLobLocator
{
	internal const int MEM_LOB_COMMON_LENGTH = 8;

	internal const int LOB_COMMON = 4;

	internal const int MEM_LOB_EXTERN_HEADER_LENGTH = 32;

	internal ObMemLobCommon? Common { get; set; }
}
