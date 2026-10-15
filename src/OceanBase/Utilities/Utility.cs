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
	/// Loads a RSA key from PEM bytes.
	/// </summary>
	public static void LoadRsaParameters(byte[] key, RSA rsa)
	{
#if NET10_0_OR_GREATER
		if (!PemEncoding.TryFindUtf8(key, out var fields))
		{
			throw new FormatException("Unrecognized PEM data: " + Encoding.ASCII.GetString(key.AsSpan(0, Math.Min(key.Length, 80))));
		}
		Span<byte> span = key.AsSpan();
		Range label = fields.Label;
		int length = span.Length;
		bool flag = ((ReadOnlySpan<byte>)span[label.Start.GetOffset(length)..label.End.GetOffset(length)]).SequenceEqual("RSA PRIVATE KEY"u8);
		Span<byte> span2 = key.AsSpan();
		label = fields.Base64Data;
		int length2 = span2.Length;
		int offset = label.Start.GetOffset(length2);
		length = label.End.GetOffset(length2) - offset;
		Span<byte> span3 = span2.Slice(offset, length);
		int num = span3.Length / 4 * 3;
		byte[] array = null;
		Span<byte> bytes = num > 1024 ? (array = ArrayPool<byte>.Shared.Rent(num)) : stackalloc byte[num];
		try
		{
			if (Base64.DecodeFromUtf8(span3, bytes, out length, out var bytesWritten) != OperationStatus.Done)
			{
				throw new FormatException("The input is not a valid Base-64 string.");
			}
			if (flag)
			{
				rsa.ImportRSAPrivateKey(bytes.Slice(0, bytesWritten), out length);
			}
			else
			{
				rsa.ImportSubjectPublicKeyInfo(bytes.Slice(0, bytesWritten), out length);
			}
		}
		finally
		{
			if (array != null)
			{
				ArrayPool<byte>.Shared.Return(array);
			}
		}
#else
		// net8.0 / net9.0 无 PemEncoding.TryFindUtf8，改用基于 char 的等价实现。
		LoadRsaParameters(Encoding.ASCII.GetString(key), rsa);
#endif
	}

#if !NET10_0_OR_GREATER
	/// <summary>
	/// Loads a RSA key from PEM text.
	/// </summary>
	public static void LoadRsaParameters(string key, RSA rsa)
	{
		if (!PemEncoding.TryFind(key.AsSpan(), out var fields))
		{
			throw new FormatException(string.Concat("Unrecognized PEM data: ".AsSpan(), key.AsSpan(0, Math.Min(key.Length, 80))));
		}
		ReadOnlySpan<char> readOnlySpan = key.AsSpan();
		Range label = fields.Label;
		int length = readOnlySpan.Length;
		bool flag = readOnlySpan[label.Start.GetOffset(length)..label.End.GetOffset(length)].SequenceEqual("RSA PRIVATE KEY".AsSpan());
		readOnlySpan = key.AsSpan();
		label = fields.Base64Data;
		int length2 = readOnlySpan.Length;
		int offset = label.Start.GetOffset(length2);
		length = label.End.GetOffset(length2) - offset;
		ReadOnlySpan<char> chars = readOnlySpan.Slice(offset, length);
		int num = chars.Length / 4 * 3;
		byte[] array = null;
		Span<byte> bytes = num > 1024 ? (array = ArrayPool<byte>.Shared.Rent(num)) : stackalloc byte[num];
		try
		{
			if (!Convert.TryFromBase64Chars(chars, bytes, out var bytesWritten))
			{
				throw new FormatException("The input is not a valid Base-64 string.");
			}
			if (flag)
			{
				rsa.ImportRSAPrivateKey(bytes.Slice(0, bytesWritten), out length);
			}
			else
			{
				rsa.ImportSubjectPublicKeyInfo(bytes.Slice(0, bytesWritten), out length);
			}
		}
		finally
		{
			if (array != null)
			{
				ArrayPool<byte>.Shared.Return(array);
			}
		}
	}
#endif

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
						goto IL_017e;
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
							goto IL_017e;
						}
					}
				}
			}
		}
		throw new FormatException("Couldn't interpret value as a valid TimeSpan: " + Encoding.UTF8.GetString(bytes));
		IL_017e:
		if (flag)
		{
			value2 = -value2;
			value3 = -value3;
			value4 = -value4;
			value5 = -value5;
		}
		return new TimeSpan(0, value2, value3, value4, value5 / 1000, value5 % 1000);
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
		return (int)Stopwatch.GetElapsedTime(startingTimestamp).TotalMilliseconds;
	}

	/// <summary>
	/// Gets the elapsed time (in seconds) between the specified <paramref name="startingTimestamp" /> and <paramref name="endingTimestamp" />. (These must be values returned from <see cref="M:System.Diagnostics.Stopwatch.GetTimestamp" />.)
	/// </summary>
	public static double GetElapsedSeconds(long startingTimestamp, long endingTimestamp)
	{
		return Stopwatch.GetElapsedTime(startingTimestamp, endingTimestamp).TotalSeconds;
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
