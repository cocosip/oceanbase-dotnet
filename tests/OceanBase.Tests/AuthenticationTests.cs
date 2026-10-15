using System;
using System.Security.Cryptography;
using System.Text;
using OceanBase.Protocol;
using OceanBase.Protocol.Serialization;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// 认证相关测试（internal，无需服务端）。
///
/// <see cref="AuthenticationUtility"/> 实现 MySQL 的 Secure Password Authentication，
/// 是**安全关键**路径：哈希或 scramble 错一位，认证就会失败（或产生错误凭证）。
/// 这里用独立实现的 MySQL 标准算法作为交叉验证基准。
/// </summary>
public sealed class AuthenticationTests
{
	/// <summary>
	/// MySQL Secure Password Authentication 的独立实现（作为测试基准）。
	/// 见 https://dev.mysql.com/doc/internals/en/secure-password-authentication.html
	/// </summary>
	private static byte[] ReferenceScramble(byte[] nonce20, string password)
	{
		if (string.IsNullOrEmpty(password))
			return Array.Empty<byte>();

		using var sha1 = SHA1.Create();
		var stage1 = sha1.ComputeHash(Encoding.UTF8.GetBytes(password));   // SHA1(password)
		var stage2 = sha1.ComputeHash(stage1);                            // SHA1(SHA1(password))

		var concat = new byte[40];
		nonce20.AsSpan(0, 20).CopyTo(concat);
		stage2.CopyTo(concat, 20);

		var stage3 = sha1.ComputeHash(concat);                            // SHA1(nonce + stage2)
		var result = new byte[20];
		for (var i = 0; i < 20; i++)
			result[i] = (byte)(stage1[i] ^ stage3[i]);
		return result;
	}

	/// <summary>
	/// OceanBase 自有 scramble 变体（与 MySQL 的 SHA-1 版不同）：
	///   h1 = SHA256(password)
	///   h2 = SHA256(h1)
	///   h3 = SHA256(h2 + nonce)
	///   result = h1 XOR h3        （32 字节）
	/// 依据源码 AuthenticationUtility.HashPasswordWithNonce。
	/// </summary>
	private static byte[] ReferenceSha256Scramble(byte[] nonce, string password)
	{
		if (string.IsNullOrEmpty(password))
			return Array.Empty<byte>();

		using var sha = SHA256.Create();
		var h1 = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
		var h2 = sha.ComputeHash(h1);
		var concat = new byte[32 + nonce.Length];
		h2.CopyTo(concat, 0);
		nonce.CopyTo(concat, 32);
		var h3 = sha.ComputeHash(concat);
		var result = new byte[32];
		for (var i = 0; i < 32; i++)
			result[i] = (byte)(h1[i] ^ h3[i]);
		return result;
	}

	private static byte[] Nonce() => new byte[]
	{
		0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A,
		0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14,
	};

	// ---------- 空终止密码字节 ----------

	[Fact]
	public void GetNullTerminatedPasswordBytes_empty_password_is_single_nul()
	{
		Assert.Equal(new byte[] { 0x00 }, AuthenticationUtility.GetNullTerminatedPasswordBytes(""));
	}

	[Fact]
	public void GetNullTerminatedPasswordBytes_appends_nul_to_utf8()
	{
		Assert.Equal(new byte[] { 0x70, 0x61, 0x73, 0x73, 0x00 },
			AuthenticationUtility.GetNullTerminatedPasswordBytes("pass"));
	}

	[Fact]
	public void GetNullTerminatedPasswordBytes_uses_utf8_for_multibyte()
	{
		// "汉字" 的 UTF-8 是 E6B189 E5AD97，再加 NUL
		Assert.Equal(new byte[] { 0xE6, 0xB1, 0x89, 0xE5, 0xAD, 0x97, 0x00 },
			AuthenticationUtility.GetNullTerminatedPasswordBytes("汉字"));
	}

	[Fact]
	public void GetNullTerminatedPasswordBytes_length_is_utf8_count_plus_one()
	{
		foreach (var pw in new[] { "", "a", "pass", "汉字", "emoji😀" })
		{
			var bytes = AuthenticationUtility.GetNullTerminatedPasswordBytes(pw);
			Assert.Equal(Encoding.UTF8.GetByteCount(pw) + 1, bytes.Length);
			Assert.Equal(0x00, bytes[^1]);
		}
	}

	// ---------- 认证响应（scramble）----------

	[Fact]
	public void CreateResponseAndPasswordHash_empty_password_yields_empty_arrays()
	{
		AuthenticationUtility.CreateResponseAndPasswordHash("", Nonce(), out var response, out var hash);
		Assert.Empty(response);
		Assert.Empty(hash);
	}

	[Fact]
	public void CreateResponseAndPasswordHash_response_matches_mysql_standard_algorithm()
	{
		// 关键断言：与独立实现的标准算法逐字节一致
		foreach (var pw in new[] { "pass", "secret", "p@ssw0rd", "汉字密码" })
		{
			AuthenticationUtility.CreateResponseAndPasswordHash(pw, Nonce(), out var response, out _);
			Assert.Equal(ReferenceScramble(Nonce(), pw), response);
		}
	}

	[Fact]
	public void CreateResponseAndPasswordHash_passwordHash_is_double_sha1()
	{
		// passwordHash 应是 SHA1(SHA1(password))
		using var sha1 = SHA1.Create();
		var expected = sha1.ComputeHash(sha1.ComputeHash(Encoding.UTF8.GetBytes("pass")));

		AuthenticationUtility.CreateResponseAndPasswordHash("pass", Nonce(), out _, out var hash);
		Assert.Equal(expected, hash);
	}

	[Fact]
	public void CreateResponseAndPasswordHash_is_deterministic()
	{
		AuthenticationUtility.CreateResponseAndPasswordHash("pass", Nonce(), out var r1, out var h1);
		AuthenticationUtility.CreateResponseAndPasswordHash("pass", Nonce(), out var r2, out var h2);
		Assert.Equal(r1, r2);
		Assert.Equal(h1, h2);
	}

	[Fact]
	public void CreateResponseAndPasswordHash_changes_with_nonce()
	{
		// 不同 challenge 必须产生不同响应（防重放）
		var other = new byte[20];
		for (var i = 0; i < 20; i++) other[i] = (byte)(0xFF - i);

		AuthenticationUtility.CreateResponseAndPasswordHash("pass", Nonce(), out var r1, out _);
		AuthenticationUtility.CreateResponseAndPasswordHash("pass", other, out var r2, out _);
		Assert.NotEqual(r1, r2);
	}

	[Fact]
	public void CreateResponseAndPasswordHash_response_is_20_bytes()
	{
		// SHA1 输出长度是协议固定值
		AuthenticationUtility.CreateResponseAndPasswordHash("pass", Nonce(), out var response, out var hash);
		Assert.Equal(20, response.Length);
		Assert.Equal(20, hash.Length);
	}

	// ---------- Scramble 便捷方法 ----------

	[Fact]
	public void CreateScrambleResponse_empty_password_returns_empty()
	{
		Assert.Empty(AuthenticationUtility.CreateScrambleResponse(Nonce(), ""));
	}

	[Fact]
	public void CreateScrambleResponse_matches_oceanbase_sha256_variant()
	{
		// 注意：CreateScrambleResponse 不是 MySQL 的 SHA-1 scramble，
		// 而是 OceanBase 自己的 SHA-256 变体（见上方参考实现）。
		foreach (var pw in new[] { "pass", "secret", "汉字密码" })
			Assert.Equal(ReferenceSha256Scramble(Nonce(), pw), AuthenticationUtility.CreateScrambleResponse(Nonce(), pw));
	}

	[Fact]
	public void CreateScrambleResponse_is_32_bytes_sha256_width()
	{
		// SHA-256 宽度，区别于 MySQL SHA-1 的 20 字节
		Assert.Equal(32, AuthenticationUtility.CreateScrambleResponse(Nonce(), "pass").Length);
	}

	// ---------- 每个字符字节数（协议层编码宽度）----------

	[Theory]
	[InlineData(CharacterSet.Latin1SwedishCaseInsensitive, 1)]
	[InlineData(CharacterSet.Binary, 1)]
	[InlineData(CharacterSet.AsciiGeneralCaseInsensitive, 1)]
	[InlineData(CharacterSet.GbkChineseCaseInsensitive, 2)]
	internal void GetBytesPerCharacter_single_and_double_byte_sets(CharacterSet cs, int expected)
	{
		Assert.Equal(expected, ProtocolUtility.GetBytesPerCharacter(cs));
	}

	[Theory]
	[InlineData(CharacterSet.Utf8Mb4General1400AccentSensitiveCaseInsensitive, 4)]
	[InlineData(CharacterSet.Utf8Mb3GeneralCaseInsensitive, 3)]
	internal void GetBytesPerCharacter_utf8_variants(CharacterSet cs, int expected)
	{
		Assert.Equal(expected, ProtocolUtility.GetBytesPerCharacter(cs));
	}

	[Fact]
	public void GetBytesPerCharacter_never_returns_zero_or_negative()
	{
		// 覆盖全部枚举值，保证不出现 0/负数（会导致缓冲区计算错误）。
		// 例外：CharacterSet.None 抛 NotSupportedException —— 这是有意的防御，
		// 无字符集时无法确定编码宽度（与原始程序集行为一致）。
		foreach (CharacterSet cs in Enum.GetValues<CharacterSet>())
		{
			if (cs == CharacterSet.None)
			{
				Assert.Throws<NotSupportedException>(() => ProtocolUtility.GetBytesPerCharacter(cs));
				continue;
			}

			var n = ProtocolUtility.GetBytesPerCharacter(cs);
			Assert.True(n > 0, $"CharacterSet.{cs} 返回 {n}");
			Assert.True(n <= 4, $"CharacterSet.{cs} 返回 {n}（超过 UTF-32 上限）");
		}
	}
}
