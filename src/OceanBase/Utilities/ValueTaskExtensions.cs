using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace OceanBase.Utilities;

internal static class ValueTaskExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueTask FromException(Exception exception)
	{
		return ValueTask.FromException(exception);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueTask<T> FromException<T>(Exception exception)
	{
		return ValueTask.FromException<T>(exception);
	}
}
