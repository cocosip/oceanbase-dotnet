using System;
using System.Buffers.Binary;

namespace OceanBase;

/// <summary>
/// Represents MySQL's internal GEOMETRY format: https://dev.mysql.com/doc/refman/8.0/en/gis-data-formats.html#gis-internal-format
/// </summary>
public sealed class MySqlGeometry
{
	private readonly byte[] m_bytes;

	/// <summary>
	/// The Spatial Reference System ID of this geometry.
	/// </summary>
	public int SRID => BinaryPrimitives.ReadInt32LittleEndian(m_bytes);

	/// <summary>
	/// The Well-known Binary serialization of this geometry.
	/// </summary>
	public ReadOnlySpan<byte> WKB
	{
		get
		{
			ReadOnlySpan<byte> valueSpan = ValueSpan;
			return valueSpan.Slice(4, valueSpan.Length - 4);
		}
	}

	/// <summary>
	/// The internal MySQL form of this geometry.
	/// </summary>
	public byte[] Value => ValueSpan.ToArray();

	internal ReadOnlySpan<byte> ValueSpan => m_bytes;

	/// <summary>
	/// Constructs a <see cref="T:OceanBase.MySqlGeometry" /> from a SRID and Well-known Binary bytes.
	/// </summary>
	/// <param name="srid">The SRID (Spatial Reference System ID).</param>
	/// <param name="wkb">The Well-known Binary serialization of the geometry.</param>
	/// <returns>A new <see cref="T:OceanBase.MySqlGeometry" /> containing the specified geometry.</returns>
	public static MySqlGeometry FromWkb(int srid, ReadOnlySpan<byte> wkb)
	{
		byte[] array = new byte[wkb.Length + 4];
		BinaryPrimitives.WriteInt32LittleEndian(array, srid);
		Span<byte> span = array.AsSpan();
		wkb.CopyTo(span.Slice(4, span.Length - 4));
		return new MySqlGeometry(array);
	}

	/// <summary>
	/// Constructs a <see cref="T:OceanBase.MySqlGeometry" /> from MySQL's internal format.
	/// </summary>
	/// <param name="value">The raw bytes of MySQL's internal GEOMETRY format.</param>
	/// <returns>A new <see cref="T:OceanBase.MySqlGeometry" /> containing the specified geometry.</returns>
	/// <remarks>See <a href="https://dev.mysql.com/doc/refman/8.0/en/gis-data-formats.html#gis-internal-format">Internal Geometry Storage Format</a>.</remarks>
	public static MySqlGeometry FromMySql(ReadOnlySpan<byte> value)
	{
		return new MySqlGeometry(value.ToArray());
	}

	internal MySqlGeometry(byte[] bytes)
	{
		m_bytes = bytes;
	}
}
