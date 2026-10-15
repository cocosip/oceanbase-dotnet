using System;
using System.Text;
using OceanBase.Protocol.Serialization;
using OceanBase.Utilities;

namespace OceanBase.Protocol.Payloads;

internal sealed class ColumnDefinitionPayload
{
	private ResizableArraySegment<byte> m_originalData;

	private bool m_readNames;

	private string? m_name;

	private string? m_schemaName;

	private string? m_catalogName;

	private string? m_table;

	private string? m_physicalTable;

	private string? m_physicalName;

	private int m_complexMetadataOffset;

	private bool m_readComplexNames;

	private string? m_complexTypeName;

	public string Name
	{
		get
		{
			if (!m_readNames)
			{
				ReadNames();
			}
			return m_name;
		}
	}

	public CharacterSet CharacterSet { get; private set; }

	public uint ColumnLength { get; private set; }

	public ColumnType ColumnType { get; private set; }

	public ColumnFlags ColumnFlags { get; private set; }

	public string SchemaName
	{
		get
		{
			if (!m_readNames)
			{
				ReadNames();
			}
			return m_schemaName;
		}
	}

	public string CatalogName
	{
		get
		{
			if (!m_readNames)
			{
				ReadNames();
			}
			return m_catalogName;
		}
	}

	public string Table
	{
		get
		{
			if (!m_readNames)
			{
				ReadNames();
			}
			return m_table;
		}
	}

	public string PhysicalTable
	{
		get
		{
			if (!m_readNames)
			{
				ReadNames();
			}
			return m_physicalTable;
		}
	}

	public string PhysicalName
	{
		get
		{
			if (!m_readNames)
			{
				ReadNames();
			}
			return m_physicalName;
		}
	}

	/// <summary>
	/// For Oracle-mode COMPLEX columns, returns the UDT type name (e.g., "UDT2_BASIC_O").
	/// Mirrors JDBC ColumnDefinition#getComplexTypeName.
	/// </summary>
	public string? ComplexTypeName
	{
		get
		{
			if (!m_readComplexNames)
			{
				ReadComplexNames();
			}
			return m_complexTypeName;
		}
	}

	public byte Decimals { get; private set; }

	public byte Precision { get; private set; }

	public byte InoutType { get; private set; }

	public int PrecisionAdjustFactor { get; private set; }

	public static void Initialize(ref ColumnDefinitionPayload payload, ResizableArraySegment<byte> arraySegment, bool isOracleMode)
	{
		if (payload == null)
		{
			payload = new ColumnDefinitionPayload();
		}
		payload.Initialize(arraySegment, isOracleMode);
	}

	private void Initialize(ResizableArraySegment<byte> originalData, bool isOracleMode)
	{
		m_originalData = originalData;
		ByteArrayReader reader = new ByteArrayReader(originalData);
		SkipLengthEncodedByteString(ref reader);
		SkipLengthEncodedByteString(ref reader);
		SkipLengthEncodedByteString(ref reader);
		SkipLengthEncodedByteString(ref reader);
		SkipLengthEncodedByteString(ref reader);
		SkipLengthEncodedByteString(ref reader);
		reader.ReadByte(12);
		CharacterSet = (CharacterSet)reader.ReadUInt16();
		ColumnLength = reader.ReadUInt32();
		byte protocolType = reader.ReadByte();
		ColumnType = ColumnTypeMap.FromProtocolType(protocolType, isOracleMode);
		ColumnFlags = (ColumnFlags)reader.ReadUInt16();
		Decimals = reader.ReadByte();
		Precision = reader.ReadByte();
		InoutType = reader.ReadByte();
		if (isOracleMode && (ColumnFlags & ColumnFlags.Unsigned) == 0)
		{
			ColumnType columnType = ColumnType;
			if ((columnType == ColumnType.Decimal || columnType == ColumnType.NewDecimal) ? true : false)
			{
				if (Precision == byte.MaxValue)
				{
					Precision = 0;
				}
				if (Decimals == 171)
				{
					Decimals = 0;
				}
			}
		}
		if (isOracleMode && (ColumnFlags & ColumnFlags.Unsigned) == 0)
		{
			ColumnType columnType = ColumnType;
			if ((columnType == ColumnType.Decimal || columnType == ColumnType.NewDecimal) ? true : false)
			{
				PrecisionAdjustFactor = -1;
			}
			else
			{
				columnType = ColumnType;
				if ((uint)(columnType - 1004) <= 1u)
				{
					PrecisionAdjustFactor = 1;
				}
				else
				{
					PrecisionAdjustFactor = 0;
				}
			}
		}
		else
		{
			ColumnType columnType = ColumnType;
			if ((uint)(columnType - 1004) <= 1u)
			{
				PrecisionAdjustFactor = 1;
			}
			else
			{
				PrecisionAdjustFactor = 0;
			}
		}
		m_complexMetadataOffset = reader.Offset;
		if (m_readNames)
		{
			m_catalogName = null;
			m_schemaName = null;
			m_table = null;
			m_physicalTable = null;
			m_name = null;
			m_physicalName = null;
			m_readNames = false;
		}
		if (m_readComplexNames)
		{
			m_complexTypeName = null;
			m_readComplexNames = false;
		}
	}

	private static void SkipLengthEncodedByteString(ref ByteArrayReader reader)
	{
		int num = checked((int)reader.ReadLengthEncodedInteger());
		reader.Offset += num;
	}

	private ColumnDefinitionPayload()
	{
	}

	private void ReadNames()
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(m_originalData);
		m_catalogName = Utility.GetString(Encoding.UTF8, byteArrayReader.ReadLengthEncodedByteString());
		m_schemaName = Utility.GetString(Encoding.UTF8, byteArrayReader.ReadLengthEncodedByteString());
		m_table = Utility.GetString(Encoding.UTF8, byteArrayReader.ReadLengthEncodedByteString());
		m_physicalTable = Utility.GetString(Encoding.UTF8, byteArrayReader.ReadLengthEncodedByteString());
		m_name = Utility.GetString(Encoding.UTF8, byteArrayReader.ReadLengthEncodedByteString());
		m_physicalName = Utility.GetString(Encoding.UTF8, byteArrayReader.ReadLengthEncodedByteString());
		m_readNames = true;
	}

	private void ReadComplexNames()
	{
		ColumnType columnType = ColumnType;
		if ((uint)(columnType - 160) > 2u)
		{
			m_complexTypeName = null;
			m_readComplexNames = true;
			return;
		}
		ByteArrayReader reader = new ByteArrayReader(m_originalData);
		reader.Offset = m_complexMetadataOffset;
		SkipLengthEncodedByteString(ref reader);
		ReadOnlySpan<byte> span = reader.ReadLengthEncodedByteString();
		m_complexTypeName = ((span.Length > 0) ? Utility.GetString(Encoding.UTF8, span) : null);
		m_readComplexNames = true;
	}
}
