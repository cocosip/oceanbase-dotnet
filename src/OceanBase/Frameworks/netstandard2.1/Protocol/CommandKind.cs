namespace OceanBase.Protocol;

internal enum CommandKind
{
	Quit = 1,
	InitDatabase = 2,
	Query = 3,
	Ping = 14,
	ChangeUser = 17,
	StatementPrepare = 22,
	StatementExecute = 23,
	StatementSendLongData = 24,
	StatementClose = 25,
	StatementFetch = 28,
	StatementPrepareExecute = 161,
	ResetConnection = 31
}
