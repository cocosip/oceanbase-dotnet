using System.Threading;
using System.Threading.Tasks;

namespace OceanBase;

/// <summary>
/// A callback that is invoked when a new <see cref="T:OceanBase.MySqlConnection" /> is opened.
/// </summary>
/// <param name="context">A <see cref="T:OceanBase.ConnectionOpenedContext" /> giving information about the connection being opened.</param>
/// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> that can be used to cancel the asynchronous operation.</param>
/// <returns>A <see cref="T:System.Threading.Tasks.ValueTask" /> representing the result of the possibly-asynchronous operation.</returns>
public delegate ValueTask ConnectionOpenedCallback(ConnectionOpenedContext context, CancellationToken cancellationToken);
