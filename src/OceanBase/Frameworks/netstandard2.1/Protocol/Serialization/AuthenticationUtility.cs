using System;
using System.Security.Cryptography;
using System.Text;

namespace OceanBase.Protocol.Serialization;

internal static class AuthenticationUtility
{
	/// <summary>
	/// Returns the UTF-8 bytes for <paramref name="password" />, followed by a null byte.
	/// </summary>
	public static byte[] GetNullTerminatedPasswordBytes(string password)
	{
		byte[] array = new byte[Encoding.UTF8.GetByteCount(password) + 1];
		Encoding.UTF8.GetBytes(password.AsSpan(), array);
		return array;
	}

	/// <summary>
	/// Hashes a password with the "Secure Password Authentication" method.
	/// </summary>
	/// <param name="password">The password to hash.</param>
	/// <param name="authenticationData">The 20-byte random challenge (from the "auth-plugin-data" in the initial handshake).</param>
	/// <param name="authenticationResponse">The authentication response.</param>
	/// <param name="passwordHash">The twice-hashed password.</param>
	/// <remarks>See <a href="https://dev.mysql.com/doc/internals/en/secure-password-authentication.html">Secure Password Authentication</a>.</remarks>
	public static void CreateResponseAndPasswordHash(string password, ReadOnlySpan<byte> authenticationData, out byte[] authenticationResponse, out byte[] passwordHash)
	{
		if (string.IsNullOrEmpty(password))
		{
			authenticationResponse = Array.Empty<byte>();
			passwordHash = Array.Empty<byte>();
			return;
		}
		using SHA1 sHA = SHA1.Create();
		Span<byte> span = stackalloc byte[40];
		int bytesWritten = Encoding.UTF8.GetByteCount(password);
		Span<byte> span2 = stackalloc byte[bytesWritten];
		Encoding.UTF8.GetBytes(password.AsSpan(), span2);
		Span<byte> span3 = stackalloc byte[20];
		sHA.TryComputeHash(span2, span3, out bytesWritten);
		sHA.TryComputeHash(span3, span.Slice(20, span.Length - 20), out bytesWritten);
		passwordHash = span.Slice(20, span.Length - 20).ToArray();
		authenticationData.Slice(0, 20).CopyTo(span);
		Span<byte> destination = stackalloc byte[20];
		sHA.TryComputeHash(span, destination, out bytesWritten);
		for (int i = 0; i < span3.Length; i++)
		{
			span3[i] ^= destination[i];
		}
		authenticationResponse = span3.ToArray();
	}

	public static byte[] CreateScrambleResponse(ReadOnlySpan<byte> nonce, string password)
	{
		if (!string.IsNullOrEmpty(password))
		{
			return HashPasswordWithNonce(nonce, password);
		}
		return Array.Empty<byte>();
	}

	private static byte[] HashPasswordWithNonce(ReadOnlySpan<byte> nonce, string password)
	{
		using SHA256 sHA = SHA256.Create();
		int bytesWritten = Encoding.UTF8.GetByteCount(password);
		Span<byte> span = stackalloc byte[bytesWritten];
		Encoding.UTF8.GetBytes(password.AsSpan(), span);
		Span<byte> span2 = stackalloc byte[32];
		sHA.TryComputeHash(span, span2, out bytesWritten);
		bytesWritten = 32 + nonce.Length;
		Span<byte> span3 = stackalloc byte[bytesWritten];
		sHA.TryComputeHash(span2, span3, out bytesWritten);
		nonce.CopyTo(span3.Slice(32, span3.Length - 32));
		Span<byte> destination = stackalloc byte[32];
		sHA.TryComputeHash(span3, destination, out bytesWritten);
		for (int i = 0; i < span2.Length; i++)
		{
			span2[i] ^= destination[i];
		}
		return span2.ToArray();
	}
}
