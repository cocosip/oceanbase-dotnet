using System.Threading;

namespace OceanBase.Core;

/// <summary>
/// <see cref="T:OceanBase.Core.IMySqlCommand" /> provides an internal abstraction over operations that can be cancelled: <see cref="T:OceanBase.MySqlCommand" /> and <see cref="T:OceanBase.MySqlBatch" />.
/// </summary>
internal interface ICancellableCommand
{
	int CommandId { get; }

	int CommandTimeout { get; }

	int? EffectiveCommandTimeout { get; set; }

	int CancelAttemptCount { get; set; }

	ServerSession? Session { get; }

	bool IsTimedOut { get; }

	CancellationTokenRegistration RegisterCancel(CancellationToken cancellationToken);

	void SetTimeout(int milliseconds);
}
