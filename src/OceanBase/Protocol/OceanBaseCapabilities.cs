using System;

namespace OceanBase.Protocol;

[Flags]
internal enum OceanBaseCapabilities : ulong
{
	None = 0uL,
	ObCapPartitionTable = 1uL,
	ObCapChangeUser = 2uL,
	ObCapReadWeak = 4uL,
	ObCapCheckSum = 8uL,
	ObCapSafeWeakRead = 0x10uL,
	ObCapPriorityHit = 0x20uL,
	ObCapCheckSumSwitch = 0x40uL,
	ObCapOcjEnableExtraOKPacket = 0x80uL,
	ObCapObProtocolV2 = 0x100uL,
	ObCapExtraOKPacketForStatistics = 0x200uL,
	ObCapAbundantFeedback = 0x400uL,
	ObCapPLRoute = 0x800uL,
	ObCapProxyReroute = 0x1000uL,
	ObCapProxySessionSync = 0x2000uL,
	ObCapFullLinkTrace = 0x4000uL,
	ObCapNewExtraInfo = 0x8000uL,
	ObCapProxySessionVarSync = 0x10000uL,
	ObCapProxyWeakStaleFeedback = 0x20000uL,
	ObCapFullLinkTraceShowTrace = 0x40000uL,
	ObCapLocalFiles = 0x100000uL
}
