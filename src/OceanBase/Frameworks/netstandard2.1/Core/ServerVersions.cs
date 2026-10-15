using System;

namespace OceanBase.Core;

internal static class ServerVersions
{
	public static readonly Version SupportsUtf8Mb4 = new Version(5, 5, 3);

	public static readonly Version SupportsResetConnection = new Version(5, 7, 3);

	public static readonly Version MariaDbSupportsResetConnection = new Version(10, 2, 4);

	public static readonly Version SupportsProcedureCache = new Version(5, 5, 3);

	public static readonly Version RemovesMySqlProcTable = new Version(8, 0, 0);

	public static readonly Version MariaDbSupportsPerQueryVariables = new Version(10, 1, 2);

	public static readonly Version OceanbaseSupportPsStmtChecksum = new Version(4, 1, 2);

	public static readonly Version OceanbaseSupportsPrepareExecute = new Version(2, 2, 7, 6);

	public static readonly Version OceanbaseSupportsAccurateParameterInfo_4_2 = new Version(4, 2, 5, 6);

	public static readonly Version OceanbaseSupportsAccurateParameterInfo_4_3 = new Version(4, 3, 5, 5);

	public static readonly Version OceanbaseSupportsAccurateParameterInfo_4_4 = new Version(4, 4, 2);

	public static readonly Version OceanbaseSupportsAccurateParameterInfo_4_5 = new Version(4, 5, 1);
}
