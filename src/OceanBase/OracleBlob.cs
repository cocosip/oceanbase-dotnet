using System;
using System.Buffers.Binary;
using System.Data;
using System.IO;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;

namespace OceanBase;

public sealed class OracleBlob : Lob
{
	public const int maxLength = int.MaxValue;

	private const uint LobMagicCode = 1280262689u;

	private const uint LobLocatorV2SimpleInRowVersion = 40962u;

	private long m_streamPosition;

	public long Position
	{
		get
		{
			return m_streamPosition;
		}
		set
		{
			Seek(value, SeekOrigin.Begin);
		}
	}

	public bool CanRead => !base.IsClosedInternal;

	public bool CanWrite => !base.IsClosedInternal;

	public bool CanSeek => !base.IsClosedInternal;

	public bool IsEmpty => IsEmptyLob();

	public long Length
	{
		get
		{
			if (base.IsClosedInternal)
			{
				throw new ObjectDisposedException("OracleBlob");
			}
			if (base.LocatorInternal is ObLobLocatorV2)
			{
				return base.LocatorInternal.PayloadSize;
			}
			return base.DataLength;
		}
	}

	public OracleBlob()
	{
		SetDataArray(Array.Empty<byte>());
		base.DataOffset = 0;
		base.DataLength = 0;
		m_streamPosition = 0L;
	}

	public static OracleBlob GetEmptyBLOB()
	{
		byte[] array = new byte[40];
		array[0] = 33;
		array[1] = 66;
		array[2] = 79;
		array[3] = 76;
		array[4] = 1;
		return new OracleBlob(true, (ReadOnlySpan<byte>)array, (string?)null, (ConnectionFacade?)null);
	}

	public OracleBlob(OracleBlob other)
	{
		if (other == null)
		{
			throw new ArgumentNullException("other");
		}
		SetDataArray(other.GetDataArray());
		base.DataOffset = other.DataOffset;
		base.DataLength = other.DataLength;
		m_streamPosition = other.m_streamPosition;
	}

	public OracleBlob(byte[] bytes)
		: base(bytes)
	{
		SetDataArray(bytes ?? Array.Empty<byte>());
		base.DataOffset = 0;
		base.DataLength = GetDataArray().Length;
		m_streamPosition = 0L;
	}

	public OracleBlob(byte[] bytes, object? exceptionInterceptor)
	{
		SetDataArray(bytes ?? Array.Empty<byte>());
		base.DataOffset = 0;
		base.DataLength = GetDataArray().Length;
		base.ExceptionInterceptor = exceptionInterceptor;
		m_streamPosition = 0L;
	}

	public OracleBlob(byte[] bytes, int offset, int length)
	{
		SetDataArray(bytes ?? Array.Empty<byte>());
		base.DataOffset = offset;
		base.DataLength = Math.Min(GetDataArray().Length - offset, length);
		m_streamPosition = 0L;
	}

	internal OracleBlob(ReadOnlySpan<byte> payload, bool useLobLocatorV2)
	{
		byte[] array = (useLobLocatorV2 ? CreateBindLocatorV2(payload) : CreateBindLocatorV1(payload));
		if (useLobLocatorV2)
		{
			BuildObLobLocatorV2(isBinary: true, array, 1280262689L, 40962L, null);
		}
		else
		{
			BuildObLobLocatorV1(isBinary: true, array, 1280262689L, 1L, null);
		}
		m_streamPosition = 0L;
	}

	internal OracleBlob(bool hasLocator, ReadOnlySpan<byte> binaryData, string? encoding, ConnectionFacade? connection)
	{
		if (connection.HasValue)
		{
			base.EncodingName = "UTF-8";
		}
		else if (encoding != null && encoding.Length > 0)
		{
			base.EncodingName = encoding;
		}
		if (!hasLocator || binaryData.Length <= 0)
		{
			return;
		}
		ByteArrayReader byteArrayReader = new ByteArrayReader(binaryData);
		uint num = byteArrayReader.ReadUInt32();
		uint num2 = byteArrayReader.ReadUInt32();
		if (num2 == 1)
		{
			BuildObLobLocatorV1(isBinary: true, binaryData, num, num2, connection);
			return;
		}
		if ((num2 & 0xFF) == 2)
		{
			BuildObLobLocatorV2(isBinary: true, binaryData, num, num2, connection);
			return;
		}
		throw new MySqlException($"UnKnown version of LOB locator: {num2}");
	}

	private static byte[] CreateBindLocatorV1(ReadOnlySpan<byte> payload)
	{
		byte[] array = new byte[40 + payload.Length];
		BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(0, 4), 1280262689u);
		BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(4, 4), 1u);
		BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(36, 4), (uint)payload.Length);
		payload.CopyTo(array.AsSpan(40));
		return array;
	}

	private static byte[] CreateBindLocatorV2(ReadOnlySpan<byte> payload)
	{
		byte[] array = new byte[8 + payload.Length];
		BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(0, 4), 1280262689u);
		BinaryPrimitives.WriteUInt32LittleEndian(array.AsSpan(4, 4), 40962u);
		payload.CopyTo(array.AsSpan(8));
		return array;
	}

	public OracleBlob(bool hasLocator, ReadOnlySpan<byte> binaryData, string? encoding, OracleConnection? oracleConnection)
		: this(hasLocator, binaryData, encoding, (oracleConnection == null) ? ((ConnectionFacade?)null) : new ConnectionFacade?(new ConnectionFacade(oracleConnection)))
	{
		m_streamPosition = 0L;
	}

	public int Read(byte[] buffer, int offset, int count)
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
		if (!CanRead)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (m_streamPosition >= Length)
		{
			return 0;
		}
		long num = Length - m_streamPosition;
		int length = Math.Min(count, checked((int)num));
		byte[] bytes = GetBytes(m_streamPosition + 1, length);
		Array.Copy(bytes, 0, buffer, offset, bytes.Length);
		m_streamPosition += bytes.Length;
		return bytes.Length;
	}

	public void Write(byte[] buffer, int offset, int count)
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
		if (!CanWrite)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (count != 0)
		{
			SetBytes(m_streamPosition + 1, buffer, offset, count);
			m_streamPosition += count;
		}
	}

	public long Seek(long offset, SeekOrigin origin)
	{
		if (!CanSeek)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		long num = checked(origin switch
		{
			SeekOrigin.Begin => offset, 
			SeekOrigin.Current => m_streamPosition + offset, 
			SeekOrigin.End => Length + offset, 
			_ => throw new ArgumentOutOfRangeException("origin"), 
		});
		if (num < 0)
		{
			throw new IOException("Attempted to seek before the beginning of the LOB.");
		}
		m_streamPosition = num;
		return m_streamPosition;
	}

	public TextReader? GetCharacterStream()
	{
		if (base.DataLength == 0 && !IsEmptyLob() && base.LocatorInternal is ObLobLocatorV2)
		{
			ReadFromServer();
		}
		byte[] dataArray = GetDataArray();
		if (dataArray != null && dataArray.Length > 0)
		{
			return new StringReader(ResolveEncoding().GetString(GetDataArray(), base.DataOffset, base.DataLength));
		}
		return null;
	}

	public long FindPosition(ReadOnlySpan<byte> pattern, long start)
	{
		if (pattern.Length == 0)
		{
			return 0L;
		}
		if (start < 1)
		{
			throw new ArgumentOutOfRangeException("start", "position must be > 0");
		}
		if (start > base.DataLength)
		{
			throw new ArgumentOutOfRangeException("start", "start > stream size");
		}
		int num = (int)(base.DataOffset + start - 1);
		int num2 = base.DataOffset + base.DataLength - pattern.Length;
		if (num <= num2)
		{
			int num3 = ((ReadOnlySpan<byte>)GetDataArray().AsSpan(num, num2 - num + 1)).IndexOf(pattern);
			if (num3 != -1)
			{
				return num + num3 + 1 - base.DataOffset;
			}
		}
		return -1L;
	}

	public long FindPosition(OracleBlob pattern, long start)
	{
		byte[] bytes = pattern.GetBytes(1L, (int)pattern.Length);
		return FindPosition(bytes, start);
	}

	public new int SetBytes(long pos, byte[] bytes)
	{
		if (base.IsClosedInternal)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (pos < 1)
		{
			throw new MySqlException("pos should be > 0, first position is 1.");
		}
		if (base.IsOracleMode && (bytes == null || bytes.Length == 0))
		{
			return 0;
		}
		if (base.LocatorInternal != null)
		{
			UpdateBlobToServer(pos, bytes, 0, bytes.Length);
			return bytes.Length;
		}
		int num = checked((int)pos - 1);
		if (base.DataLength > num + bytes.Length)
		{
			Array.Copy(bytes, 0, GetDataArray(), base.DataOffset + num, bytes.Length);
		}
		else
		{
			int num2 = Math.Min(base.DataLength, num) + bytes.Length;
			if (base.IsOracleMode)
			{
				num2 = num + bytes.Length;
			}
			byte[] array = new byte[num2];
			if (Math.Min(num, base.DataLength) > 0)
			{
				Array.Copy(GetDataArray(), base.DataOffset, array, 0, Math.Min(num, base.DataLength));
			}
			Array.Copy(bytes, 0, array, num, bytes.Length);
			SetDataArray(array);
			base.DataLength = num2;
			base.DataOffset = 0;
			base.CharData = ResolveEncoding().GetString(GetDataArray(), base.DataOffset, base.DataLength);
		}
		return bytes.Length;
	}

	public int SetBytes(long pos, byte[] bytes, int offset, int len)
	{
		if (pos < 1)
		{
			throw new MySqlException("pos should be > 0, first position is 1.");
		}
		if (base.LocatorInternal != null)
		{
			UpdateBlobToServer(pos, bytes, offset, len);
			return len;
		}
		int num = checked((int)pos - 1);
		int num2 = Math.Min(len, bytes.Length - offset);
		if (base.DataLength > num + num2)
		{
			Array.Copy(bytes, offset, GetDataArray(), base.DataOffset + num, num2);
		}
		else
		{
			byte[] array = new byte[num + num2];
			if (Math.Min(num, base.DataLength) > 0)
			{
				Array.Copy(GetDataArray(), base.DataOffset, array, 0, Math.Min(num, base.DataLength));
			}
			Array.Copy(bytes, offset, array, num, num2);
			SetDataArray(array);
			base.DataLength = num + num2;
			base.DataOffset = 0;
			base.CharData = ResolveEncoding().GetString(GetDataArray(), base.DataOffset, base.DataLength);
		}
		return num2;
	}

	public void Truncate(long len)
	{
		if (base.IsClosedInternal)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (len < 0)
		{
			if (base.IsOracleMode)
			{
				throw new MySqlException("len should be >= 0");
			}
			throw new MySqlException("len argument can not be < 1");
		}
		if (len > base.DataLength)
		{
			throw new MySqlException("len argument can not be larger than the BLOB's length");
		}
		if (base.LocatorInternal != null)
		{
			TrimBlobToServer((int)len);
		}
		else if (len >= 0 && len < base.DataLength)
		{
			base.DataLength = (int)len;
		}
	}

	public void UpdateBlobToServer(long writeAt, byte[] bytes, int offset, int length)
	{
		OracleConnection oracleConnection = base.LocatorInternal?.GetConnection()?.AsOracleConnection;
		if (oracleConnection == null)
		{
			throw new MySqlException("Invalid operation on closed BLOB");
		}
		if (bytes == null)
		{
			throw new ArgumentNullException("bytes");
		}
		if ((uint)offset > (uint)bytes.Length)
		{
			throw new ArgumentOutOfRangeException("offset");
		}
		if ((uint)length > (uint)(bytes.Length - offset))
		{
			throw new ArgumentOutOfRangeException("length");
		}
		int num = checked((int)writeAt);
		int num2 = offset;
		int num3 = length;
		using OracleCommand oracleCommand = new OracleCommand("DBMS_LOB.write", oracleConnection)
		{
			CommandType = CommandType.StoredProcedure
		};
		oracleCommand.NoActivity = true;
		byte[] value = base.LocatorInternal.BinaryData.ToArray();
		OracleParameter oracleParameter = new OracleParameter("lob_loc", OracleDbType.Blob, value, ParameterDirection.InputOutput)
		{
			ForceSerializeByOracleDbType = true
		};
		OracleParameter oracleParameter2 = new OracleParameter("amount", OracleDbType.Int32)
		{
			Value = 0
		};
		OracleParameter oracleParameter3 = new OracleParameter("offset", OracleDbType.Int32)
		{
			Value = 0
		};
		OracleParameter oracleParameter4 = new OracleParameter("buffer", OracleDbType.Raw)
		{
			Value = Array.Empty<byte>()
		};
		oracleCommand.Parameters.Add(oracleParameter);
		oracleCommand.Parameters.Add(oracleParameter2);
		oracleCommand.Parameters.Add(oracleParameter3);
		oracleCommand.Parameters.Add(oracleParameter4);
		while (num3 > 0)
		{
			int num4 = Math.Min(num3, 32767);
			byte[] array = new byte[num4];
			Array.Copy(bytes, num2, array, 0, num4);
			oracleParameter2.Value = num4;
			oracleParameter3.Value = num;
			oracleParameter4.Value = array;
			oracleCommand.ExecuteNonQuery();
			if (!(oracleParameter.Value is byte[] array2) || array2.Length == 0)
			{
				throw new MySqlException("Invalid operator on setBytes for BLOB");
			}
			oracleParameter.Value = array2;
			num += num4;
			num2 += num4;
			num3 -= num4;
		}
		if (oracleParameter.Value is byte[] array3)
		{
			OracleBlob lob = new OracleBlob(hasLocator: true, array3, base.EncodingName, new ConnectionFacade(oracleConnection));
			Copy(lob);
		}
	}

	private void TrimBlobToServer(int len)
	{
		OracleConnection oracleConnection = base.LocatorInternal?.GetConnection()?.AsOracleConnection;
		if (oracleConnection == null)
		{
			throw new MySqlException("Invalid operation on closed BLOB");
		}
		using OracleCommand oracleCommand = new OracleCommand("DBMS_LOB.trim", oracleConnection)
		{
			CommandType = CommandType.StoredProcedure
		};
		oracleCommand.NoActivity = true;
		byte[] value = base.LocatorInternal.BinaryData.ToArray();
		OracleParameter oracleParameter = new OracleParameter("lob_loc", OracleDbType.Blob, value, ParameterDirection.InputOutput)
		{
			ForceSerializeByOracleDbType = true
		};
		OracleParameter parameter = new OracleParameter("len", OracleDbType.Int32)
		{
			Value = len
		};
		oracleCommand.Parameters.Add(oracleParameter);
		oracleCommand.Parameters.Add(parameter);
		oracleCommand.ExecuteNonQuery();
		if (!(oracleParameter.Value is byte[] array) || array.Length == 0)
		{
			throw new MySqlException("Invalid operator on setBytes for BLOB");
		}
		if (oracleParameter.Value is byte[] array2)
		{
			OracleBlob lob = new OracleBlob(hasLocator: true, array2, base.EncodingName, new ConnectionFacade(oracleConnection));
			Copy(lob);
		}
	}

	protected override void ReadFromServer()
	{
		OracleConnection readConnection = GetReadConnection();
		ObLobLocator obLobLocator = base.LocatorInternal ?? throw new MySqlException("Invalid operation on closed BLOB");
		ClearData();
		int num = 1;
		checked
		{
			using MemoryStream memoryStream = new MemoryStream();
			using OracleCommand oracleCommand = new OracleCommand("DBMS_LOB.READ", readConnection)
			{
				CommandType = CommandType.StoredProcedure
			};
			oracleCommand.NoActivity = true;
			byte[] value = obLobLocator.BinaryData.ToArray();
			OracleParameter oracleParameter = new OracleParameter("lob_loc", OracleDbType.Blob, value, ParameterDirection.Input)
			{
				ForceSerializeByOracleDbType = true
			};
			OracleParameter oracleParameter2 = new OracleParameter("amount", OracleDbType.Int32, 32767, ParameterDirection.InputOutput);
			OracleParameter oracleParameter3 = new OracleParameter("offset", OracleDbType.Int32)
			{
				Value = num
			};
			OracleParameter oracleParameter4 = new OracleParameter("buffer", OracleDbType.Raw)
			{
				Direction = ParameterDirection.Output
			};
			oracleCommand.Parameters.Add(oracleParameter);
			oracleCommand.Parameters.Add(oracleParameter2);
			oracleCommand.Parameters.Add(oracleParameter3);
			oracleCommand.Parameters.Add(oracleParameter4);
			while (true)
			{
				try
				{
					oracleParameter2.Value = 32767;
					oracleParameter3.Value = num;
					oracleParameter4.Value = DBNull.Value;
					oracleCommand.ExecuteNonQuery();
					int num2 = ((oracleParameter2.Value is int num3) ? num3 : ((oracleParameter2.Value is long num4) ? ((int)num4) : 0));
					byte[] array = (oracleParameter4.Value as byte[]) ?? Array.Empty<byte>();
					if (num2 <= 0 || array.Length == 0)
					{
						break;
					}
					memoryStream.Write(array, 0, array.Length);
					num += num2;
					if (oracleParameter.Value is byte[] array2 && array2.Length != 0)
					{
						oracleParameter.Value = array2;
						obLobLocator.BinaryData = array2;
					}
					continue;
				}
				catch (MySqlException ex) when (ex.Message.IndexOf("no data found", StringComparison.OrdinalIgnoreCase) >= 0)
				{
				}
				break;
			}
			SetDataArray(memoryStream.ToArray());
			base.DataOffset = 0;
			base.DataLength = GetDataArray().Length;
		}
	}

	internal void SetOracleMode(bool isOracleMode)
	{
		base.IsOracleMode = isOracleMode;
	}

	internal byte[]? TryGetInMemoryBytes()
	{
		if (base.IsClosedInternal)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (IsEmptyLob())
		{
			return Array.Empty<byte>();
		}
		byte[] dataArray = GetDataArray();
		if (base.DataLength <= 0 || dataArray.Length == 0)
		{
			return null;
		}
		byte[] array = new byte[base.DataLength];
		Array.Copy(dataArray, base.DataOffset, array, 0, base.DataLength);
		return array;
	}
}
