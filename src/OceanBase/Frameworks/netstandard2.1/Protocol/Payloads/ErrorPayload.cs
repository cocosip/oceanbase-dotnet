using System;
using System.Text;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

internal readonly struct ErrorPayload
{
	public const byte Signature = byte.MaxValue;

	public int ErrorCode { get; }

	public string State { get; }

	public string Message { get; }

	public MySqlException ToException()
	{
		return new MySqlException((MySqlErrorCode)ErrorCode, State, Message);
	}

	public static ErrorPayload Create(ReadOnlySpan<byte> span)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(span);
		byteArrayReader.ReadByte(byte.MaxValue);
		ushort errorCode = byteArrayReader.ReadUInt16();
		string text = Encoding.ASCII.GetString(byteArrayReader.ReadByteString(1));
		string state;
		string message;
		if (text == "#")
		{
			state = Encoding.ASCII.GetString(byteArrayReader.ReadByteString(5));
			message = Encoding.UTF8.GetString(byteArrayReader.ReadByteString(span.Length - 9));
		}
		else
		{
			state = "HY000";
			message = text + Encoding.UTF8.GetString(byteArrayReader.ReadByteString(span.Length - 4));
		}
		return new ErrorPayload(errorCode, state, message);
	}

	private ErrorPayload(int errorCode, string state, string message)
	{
		ErrorCode = errorCode;
		State = state;
		Message = message;
	}
}
