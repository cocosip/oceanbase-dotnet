using System;
using System.Runtime.Serialization;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.MySqlProtocolException" /> is thrown when there is an internal protocol error communicating with MySQL Server.
/// </summary>
[Serializable]
public sealed class MySqlProtocolException : InvalidOperationException
{
	/// <summary>
	/// Creates a new <see cref="T:OceanBase.MySqlProtocolException" /> for an out-of-order packet.
	/// </summary>
	/// <param name="expectedSequenceNumber">The expected packet sequence number.</param>
	/// <param name="packetSequenceNumber">The actual packet sequence number.</param>
	/// <returns>A new <see cref="T:OceanBase.MySqlProtocolException" />.</returns>
	internal static MySqlProtocolException CreateForPacketOutOfOrder(int expectedSequenceNumber, int packetSequenceNumber)
	{
		return new MySqlProtocolException($"Packet received out-of-order. Expected {expectedSequenceNumber:d}; got {packetSequenceNumber:d}.");
	}

	[Obsolete(DiagnosticId = "SYSLIB0051")]
	private MySqlProtocolException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}

	private MySqlProtocolException(string message)
		: base(message)
	{
	}
}
