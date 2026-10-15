using System;

namespace OceanBase;

public interface IOracleArrayTypeFactory
{
	Array CreateArray(int numElems);

	Array? CreateStatusArray(int numElems);
}
