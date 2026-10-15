namespace OceanBase;

/// <summary>
/// Indicates that an Oracle custom type or provider type instance represents a database NULL sentinel.
/// </summary>
/// <remarks>
/// <para>
/// Custom UDT types that support ODP.NET-style <c>static Null</c> sentinels should implement this interface
/// and set <see cref="P:OceanBase.IOracleNullable.IsNull" /> to <see langword="true" /> on the sentinel instance.
/// </para>
/// <para>
/// For compatibility with existing ODP.NET code, <see cref="T:System.Data.SqlTypes.INullable" /> is also recognized.
/// New application code should prefer <see cref="T:OceanBase.IOracleNullable" />.
/// </para>
/// </remarks>
public interface IOracleNullable
{
	/// <summary>
	/// Gets a value indicating whether this instance is a NULL sentinel.
	/// </summary>
	bool IsNull { get; }
}
