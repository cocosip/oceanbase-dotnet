namespace OceanBase.Protocol.Serialization;

internal static class NegotiateStreamConstants
{
	public const int HeaderLength = 5;

	public const byte MajorVersion = 1;

	public const byte MinorVersion = 0;

	public const byte HandshakeDone = 20;

	public const byte HandshakeError = 21;

	public const byte HandshakeInProgress = 22;

	public const ushort MaxPayloadLength = ushort.MaxValue;
}
