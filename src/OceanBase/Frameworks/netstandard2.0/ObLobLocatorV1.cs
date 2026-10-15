using System;

namespace OceanBase;

internal sealed class ObLobLocatorV1 : ObLobLocator
{
	public const int OB_LOG_LOCATOR_HEADER = 40;

	internal long MagicCode { get; set; }

	internal long Version { get; set; }

	internal long SnapshotVersion { get; set; }

	internal ReadOnlyMemory<byte> TableId { get; set; }

	internal long ColumnId { get; set; }

	internal int Flags { get; set; }

	internal int Option { get; set; }

	internal long PayloadOffset { get; set; }

	internal ReadOnlyMemory<byte> RowId { get; set; }
}
