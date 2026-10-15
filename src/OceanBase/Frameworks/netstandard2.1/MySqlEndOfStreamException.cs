using System;
using System.IO;

namespace OceanBase;

[Serializable]
public sealed class MySqlEndOfStreamException : EndOfStreamException
{
	public int ExpectedByteCount { get; }

	public int ReadByteCount { get; }

	internal MySqlEndOfStreamException(int expectedByteCount, int readByteCount)
		: base("An incomplete response was received from the server")
	{
		ExpectedByteCount = expectedByteCount;
		ReadByteCount = readByteCount;
	}
}
