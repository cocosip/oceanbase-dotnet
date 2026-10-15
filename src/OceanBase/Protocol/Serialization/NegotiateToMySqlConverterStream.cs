using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Core;

namespace OceanBase.Protocol.Serialization;

/// <summary>
/// Helper class to translate NegotiateStream framing for SPNEGO token
/// into MySQL protocol packets.
///
/// Serves as underlying stream for System.Net.NegotiateStream
/// to perform MariaDB's auth_gssapi_client authentication.
///
/// NegotiateStream protocol is described in e.g here
/// https://winprotocoldoc.blob.core.windows.net/productionwindowsarchives/MS-NNS/[MS-NNS].pdf
/// We only use Handshake Messages for authentication.
/// </summary>
internal class NegotiateToMySqlConverterStream : Stream
{
	private readonly MemoryStream m_writeBuffer;

	private readonly ServerSession m_serverSession;

	private readonly IOBehavior m_ioBehavior;

	private readonly CancellationToken m_cancellationToken;

	private MemoryStream m_readBuffer;

	private int m_writePayloadLength;

	private bool m_clientHandshakeDone;

	public PayloadData? MySQLProtocolPayload { get; private set; }

	public override bool CanRead => true;

	public override bool CanSeek => false;

	public override bool CanWrite => true;

	public override long Length
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public override long Position
	{
		get
		{
			throw new NotImplementedException();
		}
		set
		{
			throw new NotImplementedException();
		}
	}

	public NegotiateToMySqlConverterStream(ServerSession serverSession, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		m_serverSession = serverSession;
		m_readBuffer = new MemoryStream();
		m_writeBuffer = new MemoryStream();
		m_ioBehavior = ioBehavior;
		m_cancellationToken = cancellationToken;
	}

	private static void CreateNegotiateStreamMessageHeader(byte[] buffer, int offset, byte messageId, long payloadLength)
	{
		buffer[offset] = messageId;
		buffer[offset + 1] = 1;
		buffer[offset + 2] = 0;
		buffer[offset + 3] = (byte)(payloadLength >> 8);
		buffer[offset + 4] = (byte)(payloadLength & 0xFF);
	}

	public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		int num = 0;
		if (m_readBuffer.Length == m_readBuffer.Position)
		{
			if (count < 5)
			{
				throw new InvalidDataException("Unexpected call to read less then NegotiateStream header");
			}
			if (m_clientHandshakeDone)
			{
				CreateNegotiateStreamMessageHeader(buffer, offset, 20, 0L);
				return 5;
			}
			PayloadData value = await m_serverSession.ReceiveReplyAsync(m_ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ReadOnlyMemory<byte> readOnlyMemory = value.Memory;
			if (readOnlyMemory.Length > 65535)
			{
				throw new InvalidDataException($"Payload too big for NegotiateStream - {readOnlyMemory.Length:d} bytes");
			}
			switch (readOnlyMemory.Span[0])
			{
			case 0:
				MySQLProtocolPayload = value;
				CreateNegotiateStreamMessageHeader(buffer, offset, 20, 0L);
				return 5;
			case 1:
				readOnlyMemory = readOnlyMemory.Slice(1, readOnlyMemory.Length - 1);
				break;
			}
			m_readBuffer = new MemoryStream(readOnlyMemory.ToArray());
			CreateNegotiateStreamMessageHeader(buffer, offset, 22, m_readBuffer.Length);
			num = 5;
			offset += num;
			count -= num;
		}
		if (count > 0)
		{
			num += m_readBuffer.Read(buffer, offset, count);
		}
		return num;
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		return ReadAsync(buffer, offset, count, m_cancellationToken).GetAwaiter().GetResult();
	}

	public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		if (m_writePayloadLength == 0)
		{
			if (count < 5)
			{
				throw new InvalidDataException("Cannot parse NegotiateStream handshake message header");
			}
			byte b = buffer[offset];
			byte b2 = buffer[offset + 1];
			byte b3 = buffer[offset + 2];
			byte b4 = buffer[offset + 4];
			byte b5 = buffer[offset + 3];
			if (b2 != 1 || b3 != 0)
			{
				throw new FormatException($"Unknown version of NegotiateStream protocol {b2:d}.{b3:d}, expected {1:d}.{0:d}");
			}
			if (b != 20 && b != 21 && b != 22)
			{
				throw new FormatException($"Invalid NegotiateStream MessageId 0x{b:X2}");
			}
			m_writePayloadLength = b4 + (b5 << 8);
			if (b == 20)
			{
				m_clientHandshakeDone = true;
			}
			count -= 5;
		}
		if (count == 0)
		{
			return;
		}
		if (count + m_writeBuffer.Length > m_writePayloadLength)
		{
			throw new InvalidDataException("Attempt to write more than a single message");
		}
		PayloadData payload;
		if (count < m_writePayloadLength)
		{
			m_writeBuffer.Write(buffer, offset, count);
			if (m_writeBuffer.Length < m_writePayloadLength)
			{
				return;
			}
			byte[] array = m_writeBuffer.ToArray();
			payload = new PayloadData(new ArraySegment<byte>(array, 0, (int)m_writeBuffer.Length));
			m_writeBuffer.SetLength(0L);
		}
		else
		{
			payload = new PayloadData(new ArraySegment<byte>(buffer, offset, m_writePayloadLength));
		}
		await m_serverSession.SendReplyAsync(payload, m_ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		m_writePayloadLength = 0;
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		WriteAsync(buffer, offset, count, m_cancellationToken).GetAwaiter().GetResult();
	}

	public override void Flush()
	{
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		throw new NotImplementedException();
	}

	public override void SetLength(long value)
	{
		throw new NotImplementedException();
	}
}
