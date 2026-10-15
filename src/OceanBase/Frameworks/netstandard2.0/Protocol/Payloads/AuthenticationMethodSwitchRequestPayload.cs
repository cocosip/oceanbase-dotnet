using System;
using System.Text;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase.Protocol.Payloads;

internal readonly struct AuthenticationMethodSwitchRequestPayload
{
	public const byte Signature = 254;

	public string Name { get; }

	public byte[] Data { get; }

	public static AuthenticationMethodSwitchRequestPayload Create(ReadOnlySpan<byte> span)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(span);
		byteArrayReader.ReadByte(254);
		string name;
		byte[] data;
		if (span.Length == 1)
		{
			name = "mysql_old_password";
			data = Array.Empty<byte>();
		}
		else
		{
			name = Utility.GetString(Encoding.UTF8, byteArrayReader.ReadNullTerminatedByteString());
			data = byteArrayReader.ReadByteString(byteArrayReader.BytesRemaining).ToArray();
		}
		return new AuthenticationMethodSwitchRequestPayload(name, data);
	}

	private AuthenticationMethodSwitchRequestPayload(string name, byte[] data)
	{
		Name = name;
		Data = data;
	}
}
