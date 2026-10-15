using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace OceanBase.Utilities;

internal static class ValueTaskExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueTask FromException(Exception exception)
	{
		return new ValueTask(Task.FromException(exception));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueTask<T> FromException<T>(Exception exception)
	{
		return new ValueTask<T>(Task.FromException<T>(exception));
	}
}
