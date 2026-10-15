using System;
using System.Threading.Tasks;

namespace OceanBase.Protocol.Serialization;

internal interface IPayloadHandler : IDisposable
{
	/// <summary>
	/// Gets or sets the underlying <see cref="T:OceanBase.Protocol.Serialization.IByteHandler" /> that data is read from and written to.
	/// </summary>
	IByteHandler ByteHandler { get; set; }

	/// <summary>
	/// Starts a new "conversation" with the MySQL Server. This resets the "<a href="https://dev.mysql.com/doc/internals/en/sequence-id.html">sequence id</a>"
	/// and should be called when a new command begins.
	/// </summary>
	void StartNewConversation();

	/// <summary>
	/// Forces the next sequence number to be the specified value.
	/// </summary>
	/// <param name="sequenceNumber">The next sequence number.</param>
	/// <remarks>This should only be used in advanced scenarios.</remarks>
	void SetNextSequenceNumber(int sequenceNumber);

	/// <summary>
	/// Reads the next payload.
	/// </summary>
	/// <param name="cache">An <see cref="T:OceanBase.Protocol.Serialization.ArraySegmentHolder`1" /> that will cache any buffers allocated during this
	/// read. (To disable caching, pass <code>new ArraySegmentHolder&lt;byte&gt;</code> so the cache will be garbage-collected
	/// when this method returns.)</param>
	/// <param name="protocolErrorBehavior">The <see cref="T:OceanBase.Protocol.Serialization.ProtocolErrorBehavior" /> to use if there is a protocol error.</param>
	/// <param name="ioBehavior">The <see cref="T:OceanBase.Protocol.Serialization.IOBehavior" /> to use when reading data.</param>
	/// <returns>An <see cref="T:System.ArraySegment`1" /> containing the data that was read. This
	/// <see cref="T:System.ArraySegment`1" /> will be valid to read from until the next time <see cref="M:OceanBase.Protocol.Serialization.IPayloadHandler.ReadPayloadAsync(OceanBase.Protocol.Serialization.ArraySegmentHolder{System.Byte},OceanBase.Protocol.Serialization.ProtocolErrorBehavior,OceanBase.Protocol.Serialization.IOBehavior)" /> or
	/// <see cref="M:OceanBase.Protocol.Serialization.IPayloadHandler.WritePayloadAsync(System.ReadOnlyMemory{System.Byte},OceanBase.Protocol.Serialization.IOBehavior)" /> is called.</returns>
	ValueTask<ArraySegment<byte>> ReadPayloadAsync(ArraySegmentHolder<byte> cache, ProtocolErrorBehavior protocolErrorBehavior, IOBehavior ioBehavior);

	/// <summary>
	/// Writes a payload.
	/// </summary>
	/// <param name="payload">The data to write.</param>
	/// <param name="ioBehavior">The <see cref="T:OceanBase.Protocol.Serialization.IOBehavior" /> to use when writing.</param>
	ValueTask WritePayloadAsync(ReadOnlyMemory<byte> payload, IOBehavior ioBehavior);
}
