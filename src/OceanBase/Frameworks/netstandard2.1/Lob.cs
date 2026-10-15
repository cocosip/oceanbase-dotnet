using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;

namespace OceanBase;

[Serializable]
public abstract class Lob : ISerializable, IDisposable
{
	private sealed class LobOutputStream : Stream
	{
		private readonly Lob m_lob;

		private int m_position;

		public override bool CanRead => false;

		public override bool CanSeek => false;

		public override bool CanWrite => true;

		public override long Length
		{
			get
			{
				throw new NotSupportedException();
			}
		}

		public override long Position
		{
			get
			{
				return m_position;
			}
			set
			{
				throw new NotSupportedException();
			}
		}

		public LobOutputStream(Lob lob, int startIndex)
		{
			m_lob = lob ?? throw new ArgumentNullException("lob");
			m_position = startIndex;
		}

		public override void Flush()
		{
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException("buffer");
			}
			if ((uint)offset > (uint)buffer.Length)
			{
				throw new ArgumentOutOfRangeException("offset");
			}
			if ((uint)count > (uint)(buffer.Length - offset))
			{
				throw new ArgumentOutOfRangeException("count");
			}
			int num = checked(m_position + count);
			if (m_lob.m_data.Length < num)
			{
				byte[] array = new byte[num];
				if (m_lob.m_length > 0)
				{
					Array.Copy(m_lob.m_data, m_lob.m_offset, array, 0, m_lob.m_length);
				}
				m_lob.m_data = array;
				m_lob.m_offset = 0;
				m_lob.m_length = num;
			}
			else if (m_lob.m_length < num)
			{
				m_lob.m_length = num;
			}
			Array.Copy(buffer, offset, m_lob.m_data, m_position, count);
			m_position += count;
		}
	}

	protected const int DbmsLobMaxAmount = 32767;

	[NonSerialized]
	private readonly object m_locatorLock = new object();

	private byte[] m_data = Array.Empty<byte>();

	[NonSerialized]
	private int m_offset;

	[NonSerialized]
	private int m_length;

	private ObLobLocator? m_locator;

	private string? m_encoding;

	private string? m_charData;

	private int m_lengthFromServer = -1;

	private object? m_exceptionInterceptor;

	private bool m_isOracleMode;

	private bool m_isClosed;

	internal ObLobLocator? LocatorInternal
	{
		get
		{
			return m_locator;
		}
		set
		{
			m_locator = value;
		}
	}

	protected int DataOffset
	{
		get
		{
			return m_offset;
		}
		set
		{
			m_offset = value;
		}
	}

	protected int DataLength
	{
		get
		{
			return m_length;
		}
		set
		{
			m_length = value;
		}
	}

	protected string? EncodingName
	{
		get
		{
			return m_encoding;
		}
		set
		{
			m_encoding = value;
		}
	}

	protected string? CharData
	{
		get
		{
			return m_charData;
		}
		set
		{
			m_charData = value;
		}
	}

	protected int LengthFromServer
	{
		get
		{
			return m_lengthFromServer;
		}
		set
		{
			m_lengthFromServer = value;
		}
	}

	protected object? ExceptionInterceptor
	{
		get
		{
			return m_exceptionInterceptor;
		}
		set
		{
			m_exceptionInterceptor = value;
		}
	}

	protected bool IsOracleMode
	{
		get
		{
			return m_isOracleMode;
		}
		set
		{
			m_isOracleMode = value;
		}
	}

	protected bool IsClosedInternal
	{
		get
		{
			return m_isClosed;
		}
		set
		{
			m_isClosed = value;
		}
	}

	internal ObLobLocator? Locator
	{
		get
		{
			lock (m_locatorLock)
			{
				return m_locator;
			}
		}
	}

	protected byte[] GetDataArray()
	{
		return m_data;
	}

	protected void SetDataArray(byte[] value)
	{
		m_data = value;
	}

	protected Lob()
	{
		m_data = Array.Empty<byte>();
		m_offset = 0;
		m_length = 0;
	}

	/// <summary>
	/// Safely resolves <see cref="F:OceanBase.Lob.m_encoding" /> to an <see cref="T:System.Text.Encoding" />.
	/// Falls back to UTF-8 when the encoding is not supported by the current .NET runtime
	/// (e.g., GBK on macOS/Linux without CodePagesEncodingProvider).
	/// </summary>
	protected Encoding ResolveEncoding()
	{
		string text = m_encoding ?? "UTF-8";
		try
		{
			if (text.Equals("gbk", StringComparison.OrdinalIgnoreCase) || text.Equals("gb2312", StringComparison.OrdinalIgnoreCase))
			{
				return Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
			}
			return Encoding.GetEncoding(text);
		}
		catch
		{
			return Encoding.UTF8;
		}
	}

	protected Lob(byte[] bytes)
	{
		m_data = bytes ?? Array.Empty<byte>();
		m_offset = 0;
		m_length = m_data.Length;
	}

	protected Lob(byte[] bytes, object? exceptionInterceptor)
	{
		m_data = bytes ?? Array.Empty<byte>();
		m_offset = 0;
		m_length = m_data.Length;
		m_exceptionInterceptor = exceptionInterceptor;
	}

	protected Lob(byte[] bytes, int offset, int length)
	{
		m_data = bytes ?? Array.Empty<byte>();
		m_offset = offset;
		m_length = Math.Min(m_data.Length - offset, length);
	}

	public void Copy(Lob lob)
	{
		if (lob == null)
		{
			throw new ArgumentNullException("lob");
		}
		if (this != lob)
		{
			ObLobLocator locator = lob.m_locator?.Clone();
			m_locator?.Dispose();
			m_locator = locator;
			m_data = lob.m_data;
			m_offset = lob.m_offset;
			m_length = lob.m_length;
			m_encoding = lob.m_encoding;
			m_charData = lob.m_charData;
			m_lengthFromServer = lob.m_lengthFromServer;
			m_exceptionInterceptor = lob.m_exceptionInterceptor;
			m_isOracleMode = lob.m_isOracleMode;
			m_isClosed = lob.m_isClosed;
		}
	}

	public void ClearData()
	{
		m_data = Array.Empty<byte>();
		m_charData = null;
		m_offset = 0;
		m_length = 0;
	}

	internal void BuildObLobLocatorV1(bool isBinary, ReadOnlySpan<byte> bufferBytes, long magicCode, long version, ConnectionFacade? conn)
	{
		ByteArrayReader reader = new ByteArrayReader(bufferBytes);
		ObLobLocatorV1 obLobLocatorV = new ObLobLocatorV1();
		obLobLocatorV.BinaryData = bufferBytes.ToArray();
		reader.Offset += 8;
		obLobLocatorV.MagicCode = magicCode;
		obLobLocatorV.Version = version;
		obLobLocatorV.SnapshotVersion = reader.ReadUInt64();
		obLobLocatorV.TableId = reader.ReadByteString(8).ToArray();
		obLobLocatorV.ColumnId = reader.ReadInt32();
		obLobLocatorV.Flags = reader.ReadInt16();
		obLobLocatorV.Option = reader.ReadInt16();
		obLobLocatorV.PayloadOffset = reader.ReadInt32();
		obLobLocatorV.PayloadSize = reader.ReadInt32();
		if (obLobLocatorV.PayloadSize + obLobLocatorV.PayloadOffset <= reader.BytesRemaining)
		{
			obLobLocatorV.RowId = reader.ReadByteString((int)obLobLocatorV.PayloadOffset).ToArray();
			ReadInRowLobData(isBinary, ref reader, obLobLocatorV.PayloadSize);
		}
		obLobLocatorV.SetConnection(conn);
		m_locator = obLobLocatorV;
	}

	internal void BuildObLobLocatorV2(bool isBinary, ReadOnlySpan<byte> bufferBytes, long magicCode, long version, ConnectionFacade? conn)
	{
		ByteArrayReader reader = new ByteArrayReader(bufferBytes);
		ObLobLocatorV2 obLobLocatorV = new ObLobLocatorV2();
		ObMemLobCommon obMemLobCommon = new ObMemLobCommon(magicCode, version);
		reader.Offset += 8;
		if (obMemLobCommon.HasInrowData())
		{
			if (obMemLobCommon.HasExtern())
			{
				reader.Offset += 24;
				uint num = reader.ReadUInt32();
				obLobLocatorV.PayloadSize = reader.ReadUInt32();
				if (obLobLocatorV.PayloadSize + num <= reader.BytesRemaining)
				{
					reader.Offset += (int)num;
					ReadInRowLobData(isBinary, ref reader, obLobLocatorV.PayloadSize);
				}
			}
			else if (obMemLobCommon.IsSimple())
			{
				obLobLocatorV.PayloadSize = reader.BytesRemaining;
				ReadInRowLobData(isBinary, ref reader, obLobLocatorV.PayloadSize);
			}
			else
			{
				reader.ReadByte();
				ObLobCommon obLobCommon = new ObLobCommon(reader.ReadInt3Bytes());
				if (!obLobCommon.IsInit())
				{
					obLobLocatorV.PayloadSize = reader.BytesRemaining;
					ReadInRowLobData(isBinary, ref reader, obLobLocatorV.PayloadSize);
				}
				else
				{
					reader.Offset += 16;
					obLobLocatorV.PayloadSize = reader.ReadUInt64();
					if (!obLobCommon.InRow())
					{
						reader.Offset += 56;
					}
					ReadInRowLobData(isBinary, ref reader, obLobLocatorV.PayloadSize);
				}
			}
		}
		else if (obMemLobCommon.HasExtern())
		{
			reader.Offset += 22;
			short num2 = reader.ReadInt16();
			reader.Offset += 8;
			short num3 = reader.ReadInt16();
			reader.Offset += num2 + num3;
			reader.Offset += 4;
			reader.Offset += 16;
			obLobLocatorV.PayloadSize = reader.ReadUInt64();
		}
		else
		{
			reader.Offset += 4;
			reader.Offset += 16;
			obLobLocatorV.PayloadSize = reader.ReadUInt64();
		}
		obLobLocatorV.BinaryData = bufferBytes.ToArray();
		obLobLocatorV.Common = obMemLobCommon;
		obLobLocatorV.SetConnection(conn);
		m_locator = obLobLocatorV;
	}

	private void ReadInRowLobData(bool isBinary, ref ByteArrayReader reader, long lobLength)
	{
		checked
		{
			if (isBinary)
			{
				m_data = reader.ReadByteString((int)lobLength).ToArray();
				m_length = m_data.Length;
				return;
			}
			if (lobLength > 1073741824)
			{
				throw new MySqlException($"Exceed max length of Clob for support {1073741824} current {lobLength}");
			}
			Encoding encoding = ResolveEncoding();
			m_charData = encoding.GetString(reader.ReadByteString((int)lobLength).ToArray());
			m_length = m_charData.Length;
		}
	}

	public Stream SetBinaryStream(long pos)
	{
		if (m_isClosed)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (pos < 1)
		{
			if (!m_isOracleMode)
			{
				throw new MySqlException("Invalid position in blob");
			}
			if (pos < 0)
			{
				throw new ArgumentOutOfRangeException("pos");
			}
		}
		if (m_offset > 0)
		{
			byte[] array = new byte[m_length];
			Array.Copy(m_data, m_offset, array, 0, m_length);
			m_data = array;
			m_offset = 0;
		}
		return new LobOutputStream(this, checked((int)(((pos > 0) ? (pos - 1) : pos) + m_offset)));
	}

	public Stream GetBinaryStream()
	{
		if (m_isClosed)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (m_length == 0 && !IsEmptyLob() && m_locator is ObLobLocatorV2)
		{
			ReadFromServer();
		}
		return GetBinaryStream(1L, m_length);
	}

	public Stream GetBinaryStream(long pos, long length)
	{
		if (m_isClosed)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (pos < 1)
		{
			throw new MySqlException("Out of range (position should be > 0)");
		}
		if (pos - 1 > m_length)
		{
			throw new MySqlException("Out of range (position > stream size)");
		}
		if (pos + length - 1 > m_length)
		{
			throw new MySqlException("Out of range (position + length - 1 > streamSize)");
		}
		return new MemoryStream(m_data, m_offset + checked((int)pos) - 1, checked((int)length), writable: false, publiclyVisible: true);
	}

	public int SetBytes(long pos, byte[] bytes)
	{
		if (bytes == null)
		{
			throw new ArgumentNullException("bytes");
		}
		if (pos < 1)
		{
			throw new MySqlException("pos should be > 0, first position is 1.");
		}
		int num = checked((int)pos - 1);
		if (m_length > num + bytes.Length)
		{
			Array.Copy(bytes, 0, m_data, m_offset + num, bytes.Length);
		}
		else
		{
			byte[] array = new byte[num + bytes.Length];
			if (this is OracleClob)
			{
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = 32;
				}
			}
			if (Math.Min(num, m_length) > 0)
			{
				Array.Copy(m_data, m_offset, array, 0, Math.Min(num, m_length));
			}
			Array.Copy(bytes, 0, array, num, bytes.Length);
			m_data = array;
			m_length = num + bytes.Length;
			m_offset = 0;
		}
		Encoding encoding = ResolveEncoding();
		m_charData = encoding.GetString(m_data, m_offset, m_length);
		return bytes.Length;
	}

	public void Free()
	{
		if (m_locator != null)
		{
			m_locator.Dispose();
			m_locator = null;
		}
		m_data = Array.Empty<byte>();
		m_offset = 0;
		m_length = 0;
		m_charData = null;
		m_isClosed = true;
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			Free();
		}
	}

	public bool IsEmptyLob()
	{
		if (m_locator != null)
		{
			return m_locator.PayloadSize == 0;
		}
		if (m_data.Length == 0)
		{
			return string.IsNullOrEmpty(m_charData);
		}
		return false;
	}

	public byte[] GetBytes(long pos, int length)
	{
		if (m_isClosed)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (pos < 1)
		{
			throw new MySqlException(string.Format(CultureInfo.InvariantCulture, "Out of range (position should be > 0, but is {0})", pos));
		}
		if (m_length == 0 && !IsEmptyLob() && m_locator is ObLobLocatorV2)
		{
			ReadFromServer();
		}
		int sourceIndex = m_offset + checked((int)(pos - 1));
		byte[] array = new byte[(length > m_length) ? m_length : length];
		Array.Copy(m_data, sourceIndex, array, 0, Math.Min(m_length - checked((int)(pos - 1)), length));
		return array;
	}

	public void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		info.AddValue("m_offset", m_offset);
		info.AddValue("m_length", m_length);
		info.AddValue("m_data", m_data);
		info.AddValue("m_locator", m_locator);
		info.AddValue("m_encoding", m_encoding);
		info.AddValue("m_charData", m_charData);
		info.AddValue("m_lengthFromServer", m_lengthFromServer);
		info.AddValue("m_exceptionInterceptor", m_exceptionInterceptor);
		info.AddValue("m_isOracleMode", m_isOracleMode);
		info.AddValue("m_isClosed", m_isClosed);
	}

	protected Lob(SerializationInfo info, StreamingContext context)
	{
		m_offset = info.GetInt32("m_offset");
		m_length = info.GetInt32("m_length");
		m_data = (byte[])info.GetValue("m_data", typeof(byte[]));
		m_locator = (ObLobLocator)info.GetValue("m_locator", typeof(ObLobLocator));
		m_encoding = info.GetString("m_encoding");
		m_charData = info.GetString("m_charData");
		m_lengthFromServer = info.GetInt32("m_lengthFromServer");
		m_exceptionInterceptor = info.GetValue("m_exceptionInterceptor", typeof(object));
		m_isOracleMode = info.GetBoolean("m_isOracleMode");
		m_isClosed = info.GetBoolean("m_isClosed");
	}

	protected abstract void ReadFromServer();

	protected OracleConnection GetReadConnection()
	{
		OracleConnection oracleConnection = LocatorInternal?.GetConnection()?.AsOracleConnection;
		if (oracleConnection == null)
		{
			throw new MySqlException("Invalid operation on closed " + GetType().Name);
		}
		if (LocatorInternal == null)
		{
			return oracleConnection;
		}
		try
		{
			return LocatorInternal.GetReadConnection(oracleConnection);
		}
		catch (Exception ex) when (((ex is InvalidOperationException || ex is MySqlException) ? 1 : 0) != 0)
		{
			throw new MySqlException("Failed to open read connection for " + GetType().Name, ex);
		}
	}
}
