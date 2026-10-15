using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using OceanBase.Core;
using OceanBase.Protocol.Serialization;

namespace OceanBase;

/// <summary>
/// Oracle-mode CLOB implementation used by OceanBase Oracle compatibility layer.
/// This is a C# port of the prior Java/JDBC-style implementation; behavior is kept compatible
/// (1-based positions, Oracle-mode closed semantics, DBMS_LOB READ/WRITE/TRIM/GETLENGTH support).
/// </summary>
public sealed class OracleClob : Lob
{
	public const int maxLength = 1073741824;

	private const string StartingPositionCanNotBeLessThanOne = "Starting position can not be < 1";

	public OracleClob()
	{
		SetDataArray(Array.Empty<byte>());
		base.DataOffset = 0;
		base.DataLength = 0;
		base.EncodingName = Encoding.UTF8.WebName;
	}

	public OracleClob(byte[] bytes)
		: base(bytes)
	{
		SetDataArray(bytes ?? Array.Empty<byte>());
		base.DataOffset = 0;
		base.DataLength = GetDataArray().Length;
		base.EncodingName = Encoding.UTF8.WebName;
	}

	public OracleClob(byte[] bytes, object? exceptionInterceptor)
	{
		SetDataArray(bytes ?? Array.Empty<byte>());
		base.DataOffset = 0;
		base.DataLength = GetDataArray().Length;
		base.ExceptionInterceptor = exceptionInterceptor;
		base.EncodingName = Encoding.UTF8.WebName;
	}

	public OracleClob(byte[] bytes, int offset, int length)
	{
		SetDataArray(bytes ?? Array.Empty<byte>());
		base.DataOffset = offset;
		base.DataLength = Math.Min(GetDataArray().Length - offset, length);
		base.EncodingName = Encoding.UTF8.WebName;
	}

	public OracleClob(string charDataInit, object? exceptionInterceptor)
	{
		base.CharData = charDataInit;
		base.ExceptionInterceptor = exceptionInterceptor;
		base.EncodingName = Encoding.UTF8.WebName;
		base.DataOffset = 0;
		base.DataLength = charDataInit?.Length ?? 0;
	}

	public static OracleClob GetEmptyOracleClob()
	{
		byte[] array = new byte[40];
		array[0] = 33;
		array[1] = 66;
		array[2] = 79;
		array[3] = 76;
		array[4] = 1;
		return new OracleClob(true, (ReadOnlySpan<byte>)array, Encoding.UTF8.WebName, (ConnectionFacade?)null);
	}

	public static OracleClob getEmptyOracleClob()
	{
		return GetEmptyOracleClob();
	}

	internal OracleClob(bool hasLocator, ReadOnlySpan<byte> data, string? encoding, ConnectionFacade? connection)
	{
		if (!string.IsNullOrWhiteSpace(encoding))
		{
			base.EncodingName = encoding;
		}
		else
		{
			base.EncodingName = Encoding.UTF8.WebName;
		}
		if (!hasLocator)
		{
			Encoding encoding2 = ResolveEncoding();
			base.CharData = encoding2.GetString(data.ToArray());
			base.DataOffset = 0;
			base.DataLength = base.CharData?.Length ?? 0;
		}
		else
		{
			if (data.Length <= 0)
			{
				return;
			}
			ByteArrayReader byteArrayReader = new ByteArrayReader(data);
			uint num = byteArrayReader.ReadUInt32();
			uint num2 = byteArrayReader.ReadUInt32();
			if (num2 == 1)
			{
				BuildObLobLocatorV1(isBinary: false, data, num, num2, connection);
				return;
			}
			if ((num2 & 0xFF) != 2)
			{
				throw new MySqlException($"UnKnown version of LOB locator: {num2}");
			}
			BuildObLobLocatorV2(isBinary: false, data, num, num2, connection);
		}
	}

	public OracleClob(bool hasLocator, ReadOnlySpan<byte> data, string? encoding, OracleConnection? oracleConnection)
		: this(hasLocator, data, encoding, (oracleConnection == null) ? ((ConnectionFacade?)null) : new ConnectionFacade?(new ConnectionFacade(oracleConnection)))
	{
	}

	public string GetString()
	{
		if (base.CharData != null)
		{
			return base.CharData;
		}
		return ResolveEncoding().GetString(GetDataArray(), base.DataOffset, base.DataLength);
	}

	public string getString()
	{
		return GetString();
	}

	public override string ToString()
	{
		if (base.IsClosedInternal)
		{
			return string.Empty;
		}
		if (base.DataLength == 0 && !IsEmptyLob() && base.LocatorInternal is ObLobLocatorV2)
		{
			ReadFromServer();
		}
		return GetString();
	}

	public string? GetSubString(long pos, int length)
	{
		if (base.IsClosedInternal)
		{
			if (base.IsOracleMode)
			{
				throw new MySqlException("Invalid operation on closed LOB");
			}
			return null;
		}
		if (pos < 1)
		{
			throw new MySqlException("position must be >= 1");
		}
		if (length < 0)
		{
			throw new MySqlException("length must be > 0");
		}
		if (base.DataLength == 0 && !IsEmptyLob() && base.LocatorInternal is ObLobLocatorV2)
		{
			ReadFromServer();
		}
		int num = checked((int)pos - 1);
		int num2 = 0;
		if (base.CharData != null)
		{
			num2 = Math.Min(length, base.CharData.Length - num);
			return base.CharData.Substring(num, num2);
		}
		string text = GetString();
		num2 = Math.Min(length, text.Length - num);
		return text.Substring(num, num2);
	}

	public string? getSubString(long pos, int length)
	{
		return GetSubString(pos, length);
	}

	public TextReader? GetCharacterStream()
	{
		if (base.IsClosedInternal)
		{
			if (base.IsOracleMode)
			{
				throw new MySqlException("Invalid operation on closed LOB");
			}
			return null;
		}
		if (base.DataLength == 0 && !IsEmptyLob() && base.LocatorInternal is ObLobLocatorV2)
		{
			ReadFromServer();
		}
		return new StringReader(GetString());
	}

	public TextReader? getCharacterStream()
	{
		return GetCharacterStream();
	}

	public TextReader GetCharacterStream(long pos, long length)
	{
		string text = GetString();
		if (text.Length < (int)pos - 1 + length)
		{
			throw new MySqlException("pos + length is greater than the number of characters in the OracleClob");
		}
		return new StringReader(text.Substring((int)pos - 1, (int)length));
	}

	public TextReader getCharacterStream(long pos, long length)
	{
		return GetCharacterStream(pos, length);
	}

	public TextWriter SetCharacterStream(long pos)
	{
		if (pos < 1)
		{
			if (!base.IsOracleMode)
			{
				throw new MySqlException("Starting position can not be < 1");
			}
			if (pos < 0)
			{
				ArgumentOutOfRangeException.ThrowIfNegative(pos, "pos");
			}
		}
		int num = Utf8Position(checked((int)pos - 1));
		Stream stream = SetBinaryStream(num + 1);
		Encoding encoding = ResolveEncoding();
		return new StreamWriter(stream, encoding);
	}

	public TextWriter setCharacterStream(long pos)
	{
		return SetCharacterStream(pos);
	}

	public Stream? GetAsciiStream()
	{
		if (base.IsClosedInternal)
		{
			if (base.IsOracleMode)
			{
				throw new MySqlException("Invalid operation on closed LOB");
			}
			return null;
		}
		if (base.DataLength == 0 && !IsEmptyLob() && base.LocatorInternal is ObLobLocatorV2)
		{
			ReadFromServer();
		}
		if (base.CharData != null)
		{
			return new MemoryStream(ResolveEncoding().GetBytes(base.CharData), writable: false);
		}
		return GetBinaryStream();
	}

	public Stream? getAsciiStream()
	{
		return GetAsciiStream();
	}

	public long Position(string searchStr, long start)
	{
		if (base.IsOracleMode && base.IsClosedInternal)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (start < 1)
		{
			throw new MySqlException($"Illegal starting position for search, '{start}'");
		}
		string text = GetString();
		if (start - 1 > text.Length)
		{
			throw new MySqlException("Cannot truncate OracleClob of length ");
		}
		int num = text.IndexOf(searchStr, (int)start - 1, StringComparison.Ordinal);
		return (num == -1) ? (-1) : (num + 1);
	}

	public long position(string searchStr, long start)
	{
		return Position(searchStr, start);
	}

	public long Position(OracleClob searchStr, long start)
	{
		return Position(searchStr.GetString(), start);
	}

	public long position(OracleClob searchStr, long start)
	{
		return Position(searchStr, start);
	}

	/// <summary>
	/// Convert UTF-16 code unit position into byte position in the UTF-8 byte representation.
	/// This matches the Java implementation's behavior (Oracle-mode pads missing bytes with spaces).
	/// </summary>
	private int Utf8Position(int charPosition)
	{
		if (base.CharData != null)
		{
			Encoding encoding = ResolveEncoding();
			string charData = base.CharData;
			int num = Math.Min(charPosition, charData.Length);
			int num2 = encoding.GetByteCount(charData.AsSpan(0, num));
			if (base.IsOracleMode && charPosition > charData.Length)
			{
				num2 += charPosition - charData.Length;
			}
			return num2;
		}
		byte[] dataArray = GetDataArray();
		int num3 = base.DataOffset;
		for (int i = 0; i < charPosition; i++)
		{
			int num4 = ((!base.IsOracleMode || num3 < dataArray.Length) ? (dataArray[num3] & 0xFF) : 32);
			if (num4 < 128)
			{
				num3++;
				continue;
			}
			if (num4 < 194)
			{
				throw new IOException("invalid UTF8");
			}
			if (num4 < 224)
			{
				num3 += 2;
				continue;
			}
			if (num4 < 240)
			{
				num3 += 3;
				continue;
			}
			if (num4 < 248)
			{
				num3 += 4;
				continue;
			}
			throw new IOException("invalid UTF8");
		}
		return num3 - base.DataOffset;
	}

	public int SetString(long pos, string str)
	{
		if (base.IsOracleMode && base.IsClosedInternal)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (pos < 1)
		{
			throw new MySqlException("Starting position can not be < 1");
		}
		if (base.IsOracleMode && string.IsNullOrEmpty(str))
		{
			return 0;
		}
		if (base.LocatorInternal != null)
		{
			UpdateOracleClobToServer(pos, str, str.Length);
		}
		else
		{
			byte[] bytes = ResolveEncoding().GetBytes(str);
			int num = Utf8Position(checked((int)pos - 1));
			SetBytes(num + 1, bytes);
		}
		return str.Length;
	}

	public int setString(long pos, string str)
	{
		return SetString(pos, str);
	}

	public int SetString(long pos, string str, int offset, int len)
	{
		if (str == null)
		{
			throw new MySqlException("String to set can not be NULL");
		}
		return SetString(pos, str.Substring(offset, len));
	}

	public int setString(long pos, string str, int offset, int len)
	{
		return SetString(pos, str, offset, len);
	}

	public Stream SetAsciiStream(long pos)
	{
		return SetBinaryStream(Utf8Position(checked((int)pos - 1)) + 1);
	}

	public Stream setAsciiStream(long pos)
	{
		return SetAsciiStream(pos);
	}

	/// <summary>
	/// Return character length (UTF-16 code units). For UTF-8 encoded byte arrays, compute without decoding when possible.
	/// </summary>
	public long length()
	{
		if (base.IsOracleMode && base.IsClosedInternal)
		{
			throw new MySqlException("Invalid operation on closed LOB");
		}
		if (base.CharData != null)
		{
			return base.CharData.Length;
		}
		byte[] dataArray = GetDataArray();
		if ((dataArray.Length == 0 || base.DataLength == 0) && base.LocatorInternal is ObLobLocatorV2 obLobLocatorV && obLobLocatorV.PayloadSize > 0)
		{
			if (base.LengthFromServer == -1)
			{
				base.LengthFromServer = 0;
				GetLengthFromServer();
			}
			return base.LengthFromServer;
		}
		if (!ResolveEncoding().WebName.Equals(Encoding.UTF8.WebName, StringComparison.OrdinalIgnoreCase))
		{
			return GetString().Length;
		}
		long num = 0L;
		int i = base.DataOffset;
		int num2;
		for (num2 = base.DataOffset + base.DataLength; i < num2; i++)
		{
			if (num >= base.DataLength)
			{
				break;
			}
			if ((sbyte)dataArray[i] < 0)
			{
				break;
			}
			num++;
		}
		while (i < num2)
		{
			sbyte b = (sbyte)dataArray[i++];
			if (b < 0)
			{
				if (b >> 5 != -2 || (b & 0x1E) == 0)
				{
					if (b >> 4 == -2)
					{
						if (i + 1 < num2)
						{
							i += 2;
							num++;
							continue;
						}
						throw new IOException("invalid UTF8");
					}
					if (b >> 3 != -2)
					{
						throw new IOException("invalid UTF8");
					}
					if (i + 2 < num2)
					{
						i += 3;
						num += 2;
					}
					else
					{
						i = num2;
						num++;
					}
				}
				else
				{
					i++;
					num++;
				}
			}
			else
			{
				num++;
			}
		}
		return num;
	}

	public void truncate(long truncateLen)
	{
		if (base.IsClosedInternal)
		{
			if (base.IsOracleMode)
			{
				throw new MySqlException("Invalid operation on closed LOB");
			}
			throw new ObjectDisposedException("OracleClob", "LOB is closed.");
		}
		if (truncateLen < 0)
		{
			if (base.IsOracleMode)
			{
				throw new MySqlException("Invalid argument: 'len' should not be < 0");
			}
			throw new ArgumentOutOfRangeException("truncateLen", $"String index out of range: {truncateLen}");
		}
		if (base.CharData != null && truncateLen > base.CharData.Length)
		{
			throw new MySqlException($"Cannot truncate OracleClob of length {base.CharData.Length} to length of {truncateLen}");
		}
		if (base.LocatorInternal != null)
		{
			TrimOracleClobToServer((int)truncateLen);
			return;
		}
		if (base.CharData != null)
		{
			base.CharData = base.CharData.Substring(0, (int)truncateLen);
			base.DataLength = base.CharData.Length;
			return;
		}
		long num = 0L;
		byte[] dataArray = GetDataArray();
		int i = base.DataOffset;
		int num2;
		for (num2 = base.DataOffset + base.DataLength; i < num2; i++)
		{
			if (num >= truncateLen)
			{
				break;
			}
			if ((sbyte)dataArray[i] < 0)
			{
				break;
			}
			num++;
		}
		while (i < num2 && num < truncateLen)
		{
			sbyte b = (sbyte)dataArray[i++];
			if (b < 0)
			{
				if (b >> 5 != -2 || (b & 0x1E) == 0)
				{
					if (b >> 4 == -2)
					{
						if (i + 1 < num2)
						{
							i += 2;
							num++;
							continue;
						}
						throw new IOException("invalid UTF8");
					}
					if (b >> 3 != -2)
					{
						throw new IOException("invalid UTF8");
					}
					if (i + 2 >= num2)
					{
						throw new IOException("invalid UTF8");
					}
					if (num + 2 <= truncateLen)
					{
						i += 3;
						num += 2;
					}
					else
					{
						i++;
						num = truncateLen;
					}
				}
				else
				{
					i++;
					num++;
				}
			}
			else
			{
				num++;
			}
		}
		base.DataLength = i - base.DataOffset;
	}

	private void UpdateOracleClobToServer(long writeAt, string str, int length)
	{
		OracleConnection oracleConnection = base.LocatorInternal?.GetConnection()?.AsOracleConnection;
		if (oracleConnection == null)
		{
			throw new MySqlException("Invalid operation on closed OracleClob");
		}
		int num = checked((int)writeAt);
		int num2 = 0;
		int num3 = length;
		using OracleCommand oracleCommand = new OracleCommand("DBMS_LOB.write", oracleConnection)
		{
			CommandType = CommandType.StoredProcedure
		};
		oracleCommand.NoActivity = true;
		byte[] value = base.LocatorInternal.BinaryData.ToArray();
		OracleParameter oracleParameter = new OracleParameter("lob_loc", OracleDbType.Clob, value, ParameterDirection.InputOutput)
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
		OracleParameter oracleParameter4 = new OracleParameter("buffer", OracleDbType.Varchar2)
		{
			Value = ""
		};
		oracleCommand.Parameters.Add(oracleParameter);
		oracleCommand.Parameters.Add(oracleParameter2);
		oracleCommand.Parameters.Add(oracleParameter3);
		oracleCommand.Parameters.Add(oracleParameter4);
		while (num3 > 0)
		{
			int num4 = Math.Min(num3, 32767);
			oracleParameter2.Value = num4;
			oracleParameter3.Value = num;
			oracleParameter4.Value = str.Substring(num2, num4);
			oracleCommand.ExecuteNonQuery();
			if (!(oracleParameter.Value is byte[] array) || array.Length == 0)
			{
				throw new MySqlException("Invalid operation on closed OracleClob");
			}
			oracleParameter.Value = array;
			num += num4;
			num2 += num4;
			num3 -= num4;
		}
		if (oracleParameter.Value is byte[] array2)
		{
			OracleClob lob = new OracleClob(hasLocator: true, array2, base.EncodingName, new ConnectionFacade(oracleConnection));
			Copy(lob);
		}
	}

	private void TrimOracleClobToServer(int len)
	{
		OracleConnection oracleConnection = base.LocatorInternal?.GetConnection()?.AsOracleConnection;
		if (oracleConnection == null)
		{
			throw new MySqlException("Invalid operation on closed OracleClob");
		}
		ClearData();
		using OracleCommand oracleCommand = new OracleCommand("DBMS_LOB.trim", oracleConnection)
		{
			CommandType = CommandType.StoredProcedure
		};
		oracleCommand.NoActivity = true;
		byte[] value = base.LocatorInternal.BinaryData.ToArray();
		OracleParameter oracleParameter = new OracleParameter("lob_loc", OracleDbType.Clob, value, ParameterDirection.InputOutput)
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
			throw new MySqlException("Invalid operation on closed OracleClob");
		}
		if (oracleParameter.Value is byte[] array2)
		{
			OracleClob lob = new OracleClob(hasLocator: true, array2, base.EncodingName, new ConnectionFacade(oracleConnection));
			Copy(lob);
		}
	}

	protected override void ReadFromServer()
	{
		OracleConnection readConnection = GetReadConnection();
		ObLobLocator obLobLocator = base.LocatorInternal ?? throw new MySqlException("Invalid operation on closed OracleClob");
		ClearData();
		int num = 1;
		StringBuilder stringBuilder = new StringBuilder();
		using OracleCommand oracleCommand = new OracleCommand("DBMS_LOB.READ", readConnection)
		{
			CommandType = CommandType.StoredProcedure
		};
		oracleCommand.NoActivity = true;
		byte[] value = obLobLocator.BinaryData.ToArray();
		OracleParameter parameter = new OracleParameter("lob_loc", OracleDbType.Clob, value, ParameterDirection.Input)
		{
			ForceSerializeByOracleDbType = true
		};
		OracleParameter oracleParameter = new OracleParameter("amount", OracleDbType.Int32, 32767, ParameterDirection.InputOutput);
		OracleParameter oracleParameter2 = new OracleParameter("offset", OracleDbType.Int32)
		{
			Value = num
		};
		OracleParameter oracleParameter3 = new OracleParameter("buffer", OracleDbType.Varchar2)
		{
			Direction = ParameterDirection.Output
		};
		oracleParameter3.HasSetDbType = false;
		oracleCommand.Parameters.Add(parameter);
		oracleCommand.Parameters.Add(oracleParameter);
		oracleCommand.Parameters.Add(oracleParameter2);
		oracleCommand.Parameters.Add(oracleParameter3);
		while (true)
		{
			try
			{
				oracleParameter.Value = 32767;
				oracleParameter2.Value = num;
				oracleParameter3.Value = DBNull.Value;
				oracleCommand.ExecuteNonQuery();
				int num2 = ((oracleParameter.Value is int num3) ? num3 : ((oracleParameter.Value is long num4) ? checked((int)num4) : 0));
				object value2 = oracleParameter3.Value;
				string text = ((value2 != null && !(value2 is DBNull)) ? ((value2 is string text2) ? text2 : ((!(value2 is byte[] bytes)) ? (Convert.ToString(oracleParameter3.Value, CultureInfo.InvariantCulture) ?? "") : ResolveEncoding().GetString(bytes))) : "");
				string text3 = text;
				if (num2 <= 0 || text3.Length == 0)
				{
					break;
				}
				if (stringBuilder.Length + text3.Length > 1073741824)
				{
					throw new MySqlException($"Exceed max length of OracleClob for support {1073741824} current {stringBuilder.Length + text3.Length}");
				}
				stringBuilder.Append(text3);
				num = checked(num + num2);
				continue;
			}
			catch (MySqlException ex) when (ex.Message.Contains("no data found", StringComparison.OrdinalIgnoreCase))
			{
			}
			break;
		}
		base.CharData = stringBuilder.ToString();
		base.DataLength = base.CharData.Length;
	}

	private void GetLengthFromServer()
	{
		OracleConnection readConnection = GetReadConnection();
		ObLobLocator obLobLocator = base.LocatorInternal ?? throw new MySqlException("Invalid operation on closed OracleClob");
		using OracleCommand oracleCommand = new OracleCommand();
		oracleCommand.Connection = readConnection;
		oracleCommand.CommandText = "BEGIN\n\t\t\t\t\t\t\t\t\t:clob_length := DBMS_LOB.GETLENGTH( :lob_loc);\n\t\t\t\t\t\t\t\tEND;";
		oracleCommand.NoActivity = true;
		byte[] value = obLobLocator.BinaryData.ToArray();
		OracleParameter oracleParameter = new OracleParameter("clob_length", OracleDbType.Int32)
		{
			Direction = ParameterDirection.Output
		};
		OracleParameter oracleParameter2 = new OracleParameter("lob_loc", OracleDbType.Clob)
		{
			ForceSerializeByOracleDbType = true
		};
		oracleParameter2.Direction = ParameterDirection.Input;
		oracleParameter2.Value = value;
		oracleCommand.Parameters.Add(oracleParameter);
		oracleCommand.Parameters.Add(oracleParameter2);
		oracleCommand.Prepare();
		oracleCommand.ExecuteNonQuery();
		object value2 = oracleParameter.Value;
		int lengthFromServer = ((value2 is int num) ? num : ((!(value2 is long num2)) ? Convert.ToInt32(oracleParameter.Value, CultureInfo.InvariantCulture) : checked((int)num2)));
		base.LengthFromServer = lengthFromServer;
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
		if (base.CharData != null)
		{
			return ResolveEncoding().GetBytes(base.CharData);
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
