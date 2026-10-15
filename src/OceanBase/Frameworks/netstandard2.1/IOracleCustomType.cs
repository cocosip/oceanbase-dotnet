namespace OceanBase;

public interface IOracleCustomType
{
	void FromCustomObject(OracleConnection con, object udt);

	void ToCustomObject(OracleConnection con, object udt);
}
