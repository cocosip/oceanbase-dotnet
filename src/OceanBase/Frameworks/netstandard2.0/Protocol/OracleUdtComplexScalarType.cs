namespace OceanBase.Protocol;

/// <summary>
/// Scalar attribute kinds for OceanBase Oracle-mode complex (UDT) wire encoding,
/// aligned with JDBC <c>ComplexDataType</c> and <c>BinaryRowProtocol#getComplexAttrData</c>.
/// </summary>
internal enum OracleUdtComplexScalarType
{
	Unknown,
	Number,
	Varchar2,
	Raw,
	Date,
	Timestamp,
	Char,
	IntervalYM,
	IntervalDS,
	BinaryFloat,
	BinaryDouble
}
