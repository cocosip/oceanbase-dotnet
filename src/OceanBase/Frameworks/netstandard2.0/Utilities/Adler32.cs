using System;
using System.Runtime.CompilerServices;

namespace OceanBase.Utilities;

/// <summary>
/// Calculates the 32 bit Adler checksum of a given buffer according to
/// RFC 1950. ZLIB Compressed Data Format Specification version 3.3)
/// </summary>
internal static class Adler32
{
	/// <summary>
	/// The default initial seed value of a Adler32 checksum calculation.
	/// </summary>
	public const uint SeedValue = 1u;

	private const uint BASE = 65521u;

	private const uint NMAX = 5552u;

	/// <summary>
	/// Calculates the Adler32 checksum with the bytes taken from the span.
	/// </summary>
	/// <param name="buffer">The readonly span of bytes.</param>
	/// <returns>The <see cref="T:System.UInt32" />.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint Calculate(ReadOnlySpan<byte> buffer)
	{
		if (buffer.IsEmpty)
		{
			return 1u;
		}
		return CalculateScalar(buffer);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe static uint CalculateScalar(ReadOnlySpan<byte> buffer)
	{
		uint num = 1u;
		uint num2 = 0u;
		fixed (byte* ptr = buffer)
		{
			byte* ptr2 = ptr;
			uint num3 = (uint)buffer.Length;
			while (num3 != 0)
			{
				uint num4 = ((num3 < 5552) ? num3 : 5552u);
				num3 -= num4;
				while (num4 >= 16)
				{
					num2 += (num += *ptr2);
					num2 += (num += ptr2[1]);
					num2 += (num += ptr2[2]);
					num2 += (num += ptr2[3]);
					num2 += (num += ptr2[4]);
					num2 += (num += ptr2[5]);
					num2 += (num += ptr2[6]);
					num2 += (num += ptr2[7]);
					num2 += (num += ptr2[8]);
					num2 += (num += ptr2[9]);
					num2 += (num += ptr2[10]);
					num2 += (num += ptr2[11]);
					num2 += (num += ptr2[12]);
					num2 += (num += ptr2[13]);
					num2 += (num += ptr2[14]);
					num2 += (num += ptr2[15]);
					ptr2 += 16;
					num4 -= 16;
				}
				while (num4-- != 0)
				{
					num2 += (num += *(ptr2++));
				}
				num %= 65521;
				num2 %= 65521;
			}
			return (num2 << 16) | num;
		}
	}
}
