namespace OceanBase.Protocol.Payloads;

internal static class ResetConnectionPayload
{
	public static PayloadData Instance { get; } = new PayloadData(new byte[1] { 31 });
}
