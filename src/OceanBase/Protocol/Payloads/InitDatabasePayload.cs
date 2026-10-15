using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

internal static class InitDatabasePayload
{
	public static PayloadData Create(string databaseName)
	{
		ByteBufferWriter byteBufferWriter = new ByteBufferWriter();
		byteBufferWriter.Write((byte)2);
		byteBufferWriter.Write(databaseName);
		return byteBufferWriter.ToPayloadData();
	}
}
