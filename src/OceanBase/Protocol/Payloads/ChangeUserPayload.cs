using System;
using OceanBase.Protocol.Serialization;

namespace OceanBase.Protocol.Payloads;

internal static class ChangeUserPayload
{
	public static PayloadData Create(string user, ReadOnlySpan<byte> authResponse, string? schemaName, CharacterSet characterSet, byte[]? connectionAttributes, bool isOracle = false)
	{
		ByteBufferWriter byteBufferWriter = new ByteBufferWriter();
		if (isOracle)
		{
			user = GetOracleUserName(user);
			schemaName = GetOracleSchemaName(user, schemaName);
		}
		byteBufferWriter.Write((byte)17);
		byteBufferWriter.WriteNullTerminatedString(user);
		byteBufferWriter.Write(checked((byte)authResponse.Length));
		byteBufferWriter.Write(authResponse);
		byteBufferWriter.WriteNullTerminatedString(schemaName ?? "");
		byteBufferWriter.Write((byte)characterSet);
		byteBufferWriter.Write((byte)0);
		byteBufferWriter.Write("mysql_native_password\0"u8);
		if (connectionAttributes != null)
		{
			byteBufferWriter.Write(connectionAttributes);
		}
		return byteBufferWriter.ToPayloadData();
	}

	private static string GetOracleSchemaName(string user, string? schemaName)
	{
		string text = schemaName;
		if (string.IsNullOrWhiteSpace(text))
		{
			text = ExtractPrefixBeforeAt(user);
		}
		if (text == null)
		{
			text = "";
		}
		return ToUpperIfNeeded(text);
	}

	private static string GetOracleUserName(string user)
	{
		if (string.IsNullOrEmpty(user))
		{
			return "";
		}
		int num = user.IndexOf('@');
		if (num <= 0)
		{
			return ToUpperIfNeeded(user);
		}
		string value = user.Substring(0, num);
		return string.Concat(str1: user.Substring(num), str0: ToUpperIfNeeded(value));
	}

	/// <summary>
	/// Extracts the prefix before '@' from a string, or returns the full string if '@' is not found.
	/// </summary>
	private static string ExtractPrefixBeforeAt(string value)
	{
		int num = value.IndexOf('@');
		if (num <= 0)
		{
			return value;
		}
		return value.Substring(0, num);
	}

	/// <summary>
	/// Converts a string to uppercase if it contains any lowercase letters, otherwise returns it unchanged.
	/// Avoids allocation if the string is already uppercase.
	/// </summary>
	private static string ToUpperIfNeeded(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}
		foreach (char c in value)
		{
			if (c >= 'a' && c <= 'z')
			{
				return value.ToUpperInvariant();
			}
		}
		return value;
	}
}
