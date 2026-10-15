using System;

namespace OceanBase.Protocol.Payloads;

internal static class QueryPayload
{
	public static PayloadData Create(bool supportsQueryAttributes, ReadOnlySpan<byte> query)
	{
		byte[] array = new byte[query.Length + 1 + (supportsQueryAttributes ? 2 : 0)];
		array[0] = 3;
		if (supportsQueryAttributes)
		{
			array[2] = 1;
		}
		query.CopyTo(array.AsSpan((!supportsQueryAttributes) ? 1 : 3));
		return new PayloadData(array);
	}
}
