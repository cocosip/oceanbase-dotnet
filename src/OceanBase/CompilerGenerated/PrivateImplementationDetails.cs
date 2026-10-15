// 反编译器导出了对 _003CPrivateImplementationDetails_ 的调用（switch 表达式无匹配分支时
// 编译器合成的抛异常辅助类），但它导出的定义文件是空的，需要在此补上。
using System.Runtime.CompilerServices;

internal static class _003CPrivateImplementationDetails_003E
{
	public static void ThrowSwitchExpressionException(object? unmatchedValue)
		=> throw new SwitchExpressionException(unmatchedValue);
}
