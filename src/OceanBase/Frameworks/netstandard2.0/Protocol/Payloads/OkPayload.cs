using System;
using System.Buffers.Text;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase.Protocol.Payloads;

internal sealed class OkPayload
{
	public const byte Signature = 0;

	private static readonly OkPayload s_autoCommitOk = new OkPayload(0uL, 0uL, ServerStatus.AutoCommit, 0, null, null, CharacterSet.None, null, null);

	private static readonly OkPayload s_autoCommitSessionStateChangedOk = new OkPayload(0uL, 0uL, ServerStatus.AutoCommit | ServerStatus.SessionStateChanged, 0, null, null, CharacterSet.None, null, null);

	public ulong AffectedRowCount { get; }

	public ulong LastInsertId { get; }

	public ServerStatus ServerStatus { get; }

	public int WarningCount { get; }

	public byte[]? StatusInfo { get; }

	public string? NewSchema { get; }

	public CharacterSet? NewCharacterSet { get; }

	public int? NewConnectionId { get; }

	public string? RedirectionUrl { get; }

	public static bool IsOk(ReadOnlySpan<byte> span, IServerCapabilities serverCapabilities)
	{
		if (span.Length > 0)
		{
			if (span.Length <= 6 || span[0] != 0)
			{
				if (serverCapabilities.SupportsDeprecateEof && span.Length < 16777215)
				{
					return span[0] == 254;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	/// <summary>
	/// Creates an <see cref="T:OceanBase.Protocol.Payloads.OkPayload" /> from the given <paramref name="span" />, or throws <see cref="T:System.FormatException" />
	/// if the bytes do not represent a valid <see cref="T:OceanBase.Protocol.Payloads.OkPayload" />.
	/// </summary>
	/// <param name="span">The bytes from which to read an OK packet.</param>
	/// <param name="serverCapabilities">The server capabilities.</param>
	/// <returns>A <see cref="T:OceanBase.Protocol.Payloads.OkPayload" /> with the contents of the OK packet.</returns>
	/// <exception cref="T:System.FormatException">Thrown when the bytes are not a valid OK packet.</exception>
	public static OkPayload Create(ReadOnlySpan<byte> span, IServerCapabilities serverCapabilities)
	{
		return Read(span, serverCapabilities, createPayload: true);
	}

	/// <summary>
	/// Verifies that the bytes in the given <paramref name="span" /> form a valid <see cref="T:OceanBase.Protocol.Payloads.OkPayload" />, or throws
	/// <see cref="T:System.FormatException" /> if they do not.
	/// </summary>
	/// <param name="span">The bytes from which to read an OK packet.</param>
	/// <param name="serverCapabilities">The server capabilities.</param>
	/// <exception cref="T:System.FormatException">Thrown when the bytes are not a valid OK packet.</exception>
	public static void Verify(ReadOnlySpan<byte> span, IServerCapabilities serverCapabilities)
	{
		Read(span, serverCapabilities, createPayload: false);
	}

	private static OkPayload? Read(ReadOnlySpan<byte> span, IServerCapabilities serverCapabilities, bool createPayload)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(span);
		byte b = byteArrayReader.ReadByte();
		if (b != 0 && (!serverCapabilities.SupportsDeprecateEof || b != 254))
		{
			throw new FormatException($"Expected to read 0x00 or 0xFE but got 0x{b:X2}");
		}
		ulong num = byteArrayReader.ReadLengthEncodedInteger();
		ulong num2 = byteArrayReader.ReadLengthEncodedInteger();
		ServerStatus serverStatus = (ServerStatus)0;
		int num3 = 0;
		if (serverCapabilities.SupportsProtocol41)
		{
			serverStatus = (ServerStatus)byteArrayReader.ReadUInt16();
			num3 = byteArrayReader.ReadUInt16();
		}
		else
		{
			serverStatus = (ServerStatus)byteArrayReader.ReadUInt16();
		}
		if ((serverStatus & ServerStatus.ObserverUseOracleMode) != 0)
		{
			serverCapabilities.UseOracleMode = true;
		}
		string text = null;
		CharacterSet characterSet = CharacterSet.None;
		CharacterSet characterSet2 = CharacterSet.None;
		CharacterSet characterSet3 = CharacterSet.None;
		string text2 = null;
		int? connectionId = null;
		ReadOnlySpan<byte> readOnlySpan;
		if (serverCapabilities.SupportsSessionTrack)
		{
			if (byteArrayReader.BytesRemaining > 0 && createPayload)
			{
				readOnlySpan = byteArrayReader.ReadLengthEncodedByteString();
				while (byteArrayReader.BytesRemaining > 0)
				{
					int num4 = checked((int)byteArrayReader.ReadLengthEncodedInteger());
					int num5 = byteArrayReader.Offset + num4;
					while (byteArrayReader.Offset < num5)
					{
						SessionTrackKind sessionTrackKind = (SessionTrackKind)byteArrayReader.ReadByte();
						int num6 = (int)byteArrayReader.ReadLengthEncodedInteger();
						switch (sessionTrackKind)
						{
						case SessionTrackKind.Schema:
							text = Utility.GetString(Encoding.UTF8, byteArrayReader.ReadLengthEncodedByteString());
							break;
						case SessionTrackKind.SystemVariables:
						{
							int num7 = byteArrayReader.Offset + num6;
							do
							{
								ReadOnlySpan<byte> span2 = byteArrayReader.ReadLengthEncodedByteString();
								int num8 = byteArrayReader.ReadLengthEncodedIntegerOrNull();
								ReadOnlySpan<byte> readOnlySpan2 = ((num8 == -1) ? default(ReadOnlySpan<byte>) : byteArrayReader.ReadByteString(num8));
								if (span2.SequenceEqual("character_set_client"u8) && num8 != 0)
								{
									characterSet = (readOnlySpan2.SequenceEqual("utf8mb4"u8) ? CharacterSet.Utf8Mb4Binary : (readOnlySpan2.SequenceEqual("utf8"u8) ? CharacterSet.Utf8Mb3Binary : CharacterSet.None));
								}
								else if (span2.SequenceEqual("character_set_connection"u8) && num8 != 0)
								{
									characterSet2 = (readOnlySpan2.SequenceEqual("utf8mb4"u8) ? CharacterSet.Utf8Mb4Binary : (readOnlySpan2.SequenceEqual("utf8"u8) ? CharacterSet.Utf8Mb3Binary : CharacterSet.None));
								}
								else if (span2.SequenceEqual("character_set_results"u8) && num8 != 0)
								{
									characterSet3 = (readOnlySpan2.SequenceEqual("utf8mb4"u8) ? CharacterSet.Utf8Mb4Binary : (readOnlySpan2.SequenceEqual("utf8"u8) ? CharacterSet.Utf8Mb3Binary : CharacterSet.None));
								}
								else if (span2.SequenceEqual("connection_id"u8))
								{
									connectionId = ((Utf8Parser.TryParse(readOnlySpan2, out int value, out int bytesConsumed, '\0') && bytesConsumed == readOnlySpan2.Length) ? new int?(value) : ((int?)null));
								}
								else if (span2.SequenceEqual("redirect_url"u8))
								{
									if (readOnlySpan2.Length > 0)
									{
										text2 = Utility.GetString(Encoding.UTF8, readOnlySpan2);
									}
								}
								else if (span2.SequenceEqual("ob_capability_flag"u8) && num8 != 0)
								{
									if (Utf8Parser.TryParse(readOnlySpan2, out ulong value2, out int bytesConsumed2, '\0') && bytesConsumed2 == readOnlySpan2.Length)
									{
										serverCapabilities.UseOceanBaseProtocolV2 = (value2 & 0x100) != 0;
										serverCapabilities.EnableFullLinkTrace = (value2 & 0x4000) != 0;
										serverCapabilities.UseNewExtraInfo = (value2 & 0x8000) != 0;
									}
									else
									{
										serverCapabilities.UseOceanBaseProtocolV2 = false;
										serverCapabilities.EnableFullLinkTrace = false;
										serverCapabilities.UseNewExtraInfo = false;
									}
								}
								else if (span2.SequenceEqual("__ob_client_capability_flag"u8) && num8 != 0)
								{
									if (Utf8Parser.TryParse(readOnlySpan2, out ulong value3, out int bytesConsumed3, '\0') && bytesConsumed3 == readOnlySpan2.Length)
									{
										serverCapabilities.UseNewResultSetMetadata = (value3 & 2) != 0;
									}
									else
									{
										serverCapabilities.UseNewResultSetMetadata = false;
									}
								}
							}
							while (byteArrayReader.Offset < num7);
							break;
						}
						default:
							byteArrayReader.Offset += num6;
							break;
						}
					}
				}
			}
			else
			{
				readOnlySpan = default(ReadOnlySpan<byte>);
			}
		}
		else
		{
			readOnlySpan = byteArrayReader.ReadByteString(byteArrayReader.BytesRemaining);
			if (readOnlySpan.Length != 0 && readOnlySpan[0] == readOnlySpan.Length - 1)
			{
				readOnlySpan = readOnlySpan.Slice(1, readOnlySpan.Length - 1);
			}
		}
		if (createPayload)
		{
			byte[] array = ((readOnlySpan.Length == 0) ? null : readOnlySpan.ToArray());
			CharacterSet newCharacterSet = ((characterSet == CharacterSet.Utf8Mb4Binary && characterSet2 == CharacterSet.Utf8Mb4Binary && characterSet3 == CharacterSet.Utf8Mb4Binary) ? CharacterSet.Utf8Mb4Binary : ((characterSet == CharacterSet.Utf8Mb3Binary && characterSet2 == CharacterSet.Utf8Mb3Binary && characterSet3 == CharacterSet.Utf8Mb3Binary) ? CharacterSet.Utf8Mb3Binary : CharacterSet.None));
			if (num == 0L && num2 == 0L && num3 == 0 && array == null && text == null && characterSet == CharacterSet.None && !connectionId.HasValue && text2 == null)
			{
				switch (serverStatus)
				{
				case ServerStatus.AutoCommit:
					return s_autoCommitOk;
				case ServerStatus.AutoCommit | ServerStatus.SessionStateChanged:
					return s_autoCommitSessionStateChangedOk;
				}
			}
			return new OkPayload(num, num2, serverStatus, num3, array, text, newCharacterSet, connectionId, text2);
		}
		return null;
	}

	private OkPayload(ulong affectedRowCount, ulong lastInsertId, ServerStatus serverStatus, int warningCount, byte[]? statusInfo, string? newSchema, CharacterSet newCharacterSet, int? connectionId, string? redirectionUrl)
	{
		AffectedRowCount = affectedRowCount;
		LastInsertId = lastInsertId;
		ServerStatus = serverStatus;
		WarningCount = warningCount;
		StatusInfo = statusInfo;
		NewSchema = newSchema;
		NewCharacterSet = newCharacterSet;
		NewConnectionId = connectionId;
		RedirectionUrl = redirectionUrl;
	}
}
