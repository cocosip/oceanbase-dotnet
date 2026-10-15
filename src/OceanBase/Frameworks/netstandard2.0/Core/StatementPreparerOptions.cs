using System;

namespace OceanBase.Core;

[Flags]
internal enum StatementPreparerOptions
{
	None = 0,
	AllowUserVariables = 1,
	AllowOutputParameters = 4,
	DateTimeUtc = 8,
	DateTimeLocal = 0x10,
	GuidFormatChar36 = 0x20,
	GuidFormatChar32 = 0x40,
	GuidFormatBinary16 = 0x60,
	GuidFormatTimeSwapBinary16 = 0x80,
	GuidFormatLittleEndianBinary16 = 0xA0,
	GuidFormatMask = 0xE0,
	NoBackslashEscapes = 0x100,
	AppendSemicolon = 0x200
}
