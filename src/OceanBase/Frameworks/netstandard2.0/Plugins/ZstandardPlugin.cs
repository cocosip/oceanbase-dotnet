using OceanBase.Protocol.Serialization;

namespace OceanBase.Plugins;

internal abstract class ZstandardPlugin
{
	public abstract int CompressionLevel { get; }

	public abstract IPayloadHandler CreatePayloadHandler(IByteHandler byteHandler);
}
