using System.Threading.Tasks;

namespace OceanBase.Core;

/// <summary>
/// <see cref="T:OceanBase.Core.IValuesEnumerator" /> provides an abstraction over iterating through a sequence of
/// rows, where each row can fill an array of field values.
/// </summary>
internal interface IValuesEnumerator
{
	int FieldCount { get; }

	ValueTask<bool> MoveNextAsync();

	bool MoveNext();

	void GetValues(object[] values);
}
