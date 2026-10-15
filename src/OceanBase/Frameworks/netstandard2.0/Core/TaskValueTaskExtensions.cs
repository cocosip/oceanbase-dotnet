using System.Threading.Tasks;

namespace OceanBase.Core;

internal static class TaskValueTaskExtensions
{
	public static ValueTask<T> AsValueTask<T>(this Task<T> task)
	{
		return new ValueTask<T>(task);
	}
}
