using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;

namespace OceanBase.Utilities;

internal static class Utility
{
	static Utility()
	{
		try
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}
		catch
		{
		}
	}

	public static void Dispose<T>(ref T? disposable) where T : class, IDisposable
	{
		if (disposable != null)
		{
			disposable.Dispose();
			disposable = null;
		}
	}

	/// <summary>
	/// Loads a RSA key from a PEM string.
	/// </summary>
	public static RSAParameters GetRsaParameters(string key)
	{
		string text;
		bool isPrivate;
		int num;
		if ((num = key.IndexOf("-----BEGIN RSA PRIVATE KEY-----", StringComparison.Ordinal)) > -1)
		{
			num += "-----BEGIN RSA PRIVATE KEY-----".Length;
			text = "-----END RSA PRIVATE KEY-----";
			isPrivate = true;
		}
		else
		{
			if ((num = key.IndexOf("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal)) <= -1)
			{
				throw new FormatException("Unrecognized PEM header: " + key.Substring(0, Math.Min(key.Length, 80)));
			}
			num += "-----BEGIN PUBLIC KEY-----".Length;
			text = "-----END PUBLIC KEY-----";
			isPrivate = false;
		}
		int num2 = key.IndexOf(text, num, StringComparison.Ordinal);
		int num3;
		if (num2 <= -1)
		{
			string text2 = text;
			num3 = Math.Max(key.Length - 80, 0);
			throw new FormatException("Missing expected '" + text2 + "' PEM footer: " + key.Substring(num3, key.Length - num3));
		}
		ReadOnlySpan<char> readOnlySpan = key.AsSpan();
		num3 = num;
		ReadOnlySpan<char> chars = readOnlySpan.Slice(num3, num2 - num3);
		int num4 = chars.Length / 4 * 3;
		byte[] array = null;
		Span<byte> span = ((num4 <= 1024) ? stackalloc byte[num4] : ((Span<byte>)(array = ArrayPool<byte>.Shared.Rent(num4))));
		Span<byte> bytes = span;
		try
		{
			if (!Convert.TryFromBase64Chars(chars, bytes, out var bytesWritten))
			{
				throw new FormatException("The input is not a valid Base-64 string.");
			}
			return GetRsaParameters(bytes.Slice(0, bytesWritten), isPrivate);
		}
		finally
		{
			if (array != null)
			{
				ArrayPool<byte>.Shared.Return(array);
			}
		}
	}

	private static RSAParameters GetRsaParameters(ReadOnlySpan<byte> data, bool isPrivate)
	{
		if (data[0] != 48)
		{
			throw new FormatException($"Expected 0x30 but read 0x{data[0]:X2}");
		}
		data = data.Slice(1, data.Length - 1);
		if (!TryReadAsnLength(data, out var length, out var bytesConsumed))
		{
			throw new FormatException("Couldn't read key length");
		}
		int num = bytesConsumed;
		data = data.Slice(num, data.Length - num);
		if (!isPrivate)
		{
			ReadOnlySpan<byte> other = new byte[15]
			{
				48, 13, 6, 9, 42, 134, 72, 134, 247, 13,
				1, 1, 1, 5, 0
			};
			if (!data.Slice(0, other.Length).SequenceEqual(other))
			{
				throw new FormatException("Expected RSA OID but read " + BitConverter.ToString(data.Slice(0, 15).ToArray()));
			}
			num = other.Length;
			data = data.Slice(num, data.Length - num);
			if (data[0] != 3)
			{
				throw new FormatException($"Expected 0x03 but read 0x{data[0]:X2}");
			}
			data = data.Slice(1, data.Length - 1);
			if (!TryReadAsnLength(data, out length, out bytesConsumed))
			{
				throw new FormatException("Couldn't read length");
			}
			num = bytesConsumed;
			data = data.Slice(num, data.Length - num);
			if (data[0] != 0)
			{
				throw new FormatException($"Expected 0x00 but read 0x{data[0]:X2}");
			}
			data = data.Slice(1, data.Length - 1);
			if (data[0] != 48)
			{
				throw new FormatException($"Expected 0x30 but read 0x{data[0]:X2}");
			}
			data = data.Slice(1, data.Length - 1);
			if (!TryReadAsnLength(data, out length, out bytesConsumed))
			{
				throw new FormatException("Couldn't read length");
			}
			num = bytesConsumed;
			data = data.Slice(num, data.Length - num);
		}
		else
		{
			if (!TryReadAsnInteger(data, out var number, out bytesConsumed) || number.Length != 1 || number[0] != 0)
			{
				throw new FormatException("Couldn't read zero.");
			}
			num = bytesConsumed;
			data = data.Slice(num, data.Length - num);
		}
		if (!TryReadAsnInteger(data, out var number2, out bytesConsumed))
		{
			throw new FormatException("Couldn't read modulus");
		}
		num = bytesConsumed;
		data = data.Slice(num, data.Length - num);
		if (!TryReadAsnInteger(data, out var number3, out bytesConsumed))
		{
			throw new FormatException("Couldn't read exponent");
		}
		num = bytesConsumed;
		data = data.Slice(num, data.Length - num);
		if (!isPrivate)
		{
			return new RSAParameters
			{
				Modulus = number2.ToArray(),
				Exponent = number3.ToArray()
			};
		}
		if (!TryReadAsnInteger(data, out var number4, out bytesConsumed))
		{
			throw new FormatException("Couldn't read D");
		}
		num = bytesConsumed;
		data = data.Slice(num, data.Length - num);
		if (!TryReadAsnInteger(data, out var number5, out bytesConsumed))
		{
			throw new FormatException("Couldn't read P");
		}
		num = bytesConsumed;
		data = data.Slice(num, data.Length - num);
		if (!TryReadAsnInteger(data, out var number6, out bytesConsumed))
		{
			throw new FormatException("Couldn't read Q");
		}
		num = bytesConsumed;
		data = data.Slice(num, data.Length - num);
		if (!TryReadAsnInteger(data, out var number7, out bytesConsumed))
		{
			throw new FormatException("Couldn't read DP");
		}
		num = bytesConsumed;
		data = data.Slice(num, data.Length - num);
		if (!TryReadAsnInteger(data, out var number8, out bytesConsumed))
		{
			throw new FormatException("Couldn't read DQ");
		}
		num = bytesConsumed;
		data = data.Slice(num, data.Length - num);
		if (!TryReadAsnInteger(data, out var number9, out bytesConsumed))
		{
			throw new FormatException("Couldn't read IQ");
		}
		num = bytesConsumed;
		data = data.Slice(num, data.Length - num);
		return new RSAParameters
		{
			Modulus = number2.ToArray(),
			Exponent = number3.ToArray(),
			D = number4.ToArray(),
			P = number5.ToArray(),
			Q = number6.ToArray(),
			DP = number7.ToArray(),
			DQ = number8.ToArray(),
			InverseQ = number9.ToArray()
		};
	}

	/// <summary>
	/// Returns a new <see cref="T:System.Byte" /> array that is a slice of <paramref name="input" /> starting at <paramref name="offset" />.
	/// </summary>
	/// <param name="input">The array to slice.</param>
	/// <param name="offset">The offset at which to slice.</param>
	/// <param name="length">The length of the slice.</param>
	/// <returns>A new <see cref="T:System.Byte" /> array that is a slice of <paramref name="input" /> from <paramref name="offset" /> to the end.</returns>
	public static byte[] ArraySlice(byte[] input, int offset, int length)
	{
		if (offset == 0 && length == input.Length)
		{
			return input;
		}
		byte[] array = new byte[length];
		Array.Copy(input, offset, array, 0, array.Length);
		return array;
	}

	/// <summary>
	/// Finds the next index of <paramref name="pattern" /> in <paramref name="data" />, starting at index <paramref name="offset" />.
	/// </summary>
	/// <param name="data">The array to search.</param>
	/// <param name="offset">The offset at which to start searching.</param>
	/// <param name="pattern">The pattern to find in <paramref name="data" />.</param>
	/// <returns>The offset of <paramref name="pattern" /> within <paramref name="data" />, or <c>-1</c> if <paramref name="pattern" /> was not found.</returns>
	public static int FindNextIndex(ReadOnlySpan<byte> data, int offset, ReadOnlySpan<byte> pattern)
	{
		int num = data.Slice(offset, data.Length - offset).IndexOf(pattern);
		if (num != -1)
		{
			return offset + num;
		}
		return -1;
	}

	/// <summary>
	/// Resizes <paramref name="resizableArray" /> to hold at least <paramref name="newLength" /> items.
	/// </summary>
	/// <remarks><paramref name="resizableArray" /> may be <c>null</c>, in which case a new <see cref="T:OceanBase.Utilities.ResizableArray`1" /> will be allocated.</remarks>
	public static void Resize<T>([NotNull] ref ResizableArray<T>? resizableArray, int newLength) where T : notnull
	{
		if (resizableArray == null)
		{
			resizableArray = new ResizableArray<T>();
		}
		resizableArray.DoResize(newLength);
	}

	public static bool TryParseRedirectionHeader(string redirectUrl, string initialUser, out string host, out int port, out string user)
	{
		host = "";
		port = 0;
		user = "";
		if (!redirectUrl.StartsWith("mysql://", StringComparison.Ordinal) && !redirectUrl.StartsWith("mariadb://", StringComparison.Ordinal))
		{
			return false;
		}
		try
		{
			Uri uri = new Uri(redirectUrl);
			host = uri.Host;
			if (string.IsNullOrEmpty(host))
			{
				return false;
			}
			if (host.StartsWith('[') && host.EndsWith("]", StringComparison.Ordinal))
			{
				host = host.Substring(1, host.Length - 2);
			}
			port = uri.Port;
			user = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]);
			if (string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(uri.Query))
			{
				string[] array = uri.Query.Substring(1).Split('&');
				foreach (string text in array)
				{
					if (text.StartsWith("user=", StringComparison.Ordinal))
					{
						user = Uri.UnescapeDataString(text.Substring(5));
					}
				}
			}
			if (string.IsNullOrEmpty(user))
			{
				user = initialUser;
			}
			return true;
		}
		catch (UriFormatException)
		{
			return false;
		}
	}

	public static TimeSpan ParseTimeSpan(ReadOnlySpan<byte> value)
	{
		ReadOnlySpan<byte> bytes = value;
		bool flag = false;
		if (value.Length >= 1 && value[0] == 45)
		{
			flag = true;
			value = value.Slice(1, value.Length - 1);
		}
		int value3;
		int value4;
		int value5;
		if (Utf8Parser.TryParse(value, out int value2, out int i, '\0') && value2 >= 0 && value2 <= 838 && value.Length != i && value[i] == 58)
		{
			int num = i + 1;
			value = value.Slice(num, value.Length - num);
			if (Utf8Parser.TryParse(value, out value3, out i, '\0') && i == 2 && value3 >= 0 && value3 <= 59 && value.Length >= 3 && value[2] == 58)
			{
				value = value.Slice(3, value.Length - 3);
				if (Utf8Parser.TryParse(value, out value4, out i, '\0') && i == 2 && value4 >= 0 && value4 <= 59)
				{
					if (value.Length == 2)
					{
						value5 = 0;
						goto IL_0181;
					}
					if (value[2] == 46)
					{
						value = value.Slice(3, value.Length - 3);
						if (Utf8Parser.TryParse(value, out value5, out i, '\0') && i == value.Length && value5 >= 0 && value5 <= 999999)
						{
							for (; i < 6; i++)
							{
								value5 *= 10;
							}
							goto IL_0181;
						}
					}
				}
			}
		}
		throw new FormatException("Couldn't interpret value as a valid TimeSpan: " + Encoding.UTF8.GetString(bytes));
		IL_0181:
		if (flag)
		{
			value2 = -value2;
			value3 = -value3;
			value4 = -value4;
			value5 = -value5;
		}
		return new TimeSpan(0, value2, value3, value4, value5 / 1000) + TimeSpan.FromTicks(value5 % 1000 * 10);
	}

	public static byte[] TrimZeroByte(byte[] value)
	{
		if (value != null)
		{
			int num = value.Length;
			if (num >= 1 && value[num - 1] == 0)
			{
				Array.Resize(ref value, value.Length - 1);
			}
		}
		return value;
	}

	public static ReadOnlySpan<byte> TrimZeroByte(ReadOnlySpan<byte> value)
	{
		int length = value.Length;
		if (length < 1 || value[length - 1] != 0)
		{
			return value;
		}
		return value.Slice(0, value.Length - 1);
	}

	public static int Read(this Stream stream, Memory<byte> buffer)
	{
		return stream.Read(buffer.Span);
	}

	public static void Write(this Stream stream, ReadOnlyMemory<byte> data)
	{
		stream.Write(data.Span);
	}

	public static void SwapBytes(Span<byte> bytes, int offset1, int offset2)
	{
		ref byte source = ref Unsafe.AsRef(in bytes[0]);
		ref byte reference = ref Unsafe.Add(ref source, offset2);
		ref byte reference2 = ref Unsafe.Add(ref source, offset1);
		byte b = Unsafe.Add(ref source, offset1);
		byte b2 = Unsafe.Add(ref source, offset2);
		reference = b;
		reference2 = b2;
	}

	public static bool IsWindows()
	{
		try
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
		}
		catch (PlatformNotSupportedException)
		{
			return false;
		}
	}

	public static void GetOSDetails(out string? os, out string osDescription, out string architecture)
	{
		os = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" : (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : null)));
		osDescription = RuntimeInformation.OSDescription;
		architecture = RuntimeInformation.ProcessArchitecture.ToString();
	}

	/// <summary>
	/// Gets the elapsed time (in milliseconds) since the specified <paramref name="startingTimestamp" /> (which must be a value returned from <see cref="M:System.Diagnostics.Stopwatch.GetTimestamp" />.
	/// </summary>
	public static int GetElapsedMilliseconds(long startingTimestamp)
	{
		return (int)((Stopwatch.GetTimestamp() - startingTimestamp) * 1000 / Stopwatch.Frequency);
	}

	/// <summary>
	/// Gets the elapsed time (in seconds) between the specified <paramref name="startingTimestamp" /> and <paramref name="endingTimestamp" />. (These must be values returned from <see cref="M:System.Diagnostics.Stopwatch.GetTimestamp" />.)
	/// </summary>
	public static double GetElapsedSeconds(long startingTimestamp, long endingTimestamp)
	{
		return (double)(endingTimestamp - startingTimestamp) / (double)Stopwatch.Frequency;
	}

	public static SslProtocols GetDefaultSslProtocols()
	{
		return SslProtocols.None;
	}

	private static bool TryReadAsnLength(ReadOnlySpan<byte> data, out int length, out int bytesConsumed)
	{
		byte b = data[0];
		if (b < 128)
		{
			length = b;
			bytesConsumed = 1;
			return true;
		}
		switch (b)
		{
		case 129:
			length = data[1];
			bytesConsumed = 2;
			return true;
		case 130:
			length = data[1] * 256 + data[2];
			bytesConsumed = 3;
			return true;
		default:
			length = 0;
			bytesConsumed = 0;
			return false;
		}
	}

	private static bool TryReadAsnInteger(ReadOnlySpan<byte> data, out ReadOnlySpan<byte> number, out int bytesConsumed)
	{
		if (data.Length < 1 || data[0] != 2)
		{
			number = default(ReadOnlySpan<byte>);
			bytesConsumed = 0;
			return false;
		}
		data = data.Slice(1, data.Length - 1);
		if (!TryReadAsnLength(data, out var length, out var bytesConsumed2))
		{
			number = default(ReadOnlySpan<byte>);
			bytesConsumed = 0;
			return false;
		}
		number = data.Slice(bytesConsumed2, length);
		bytesConsumed = bytesConsumed2 + length + 1;
		while (true)
		{
			ReadOnlySpan<byte> readOnlySpan = number;
			if (readOnlySpan.Length < 2 || readOnlySpan[0] != 0)
			{
				break;
			}
			number = number.Slice(1, number.Length - 1);
		}
		return true;
	}
}
