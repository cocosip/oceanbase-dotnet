// netstandard2.0 / netstandard2.1 目标所需的补齐类型。
//
// 说明：反编译器导出了对 _003CPrivateImplementationDetails_ 的调用（switch 表达式无匹配分支时
// 编译器合成的抛异常辅助类），但它导出的定义文件是空的，需要在此补上。
//   - netstandard2.0 : 使用 ThrowInvalidOperationException（netstandard2.0 无 SwitchExpressionException）
//   - netstandard2.1 : 使用 ThrowSwitchExpressionException（该类型自 netstandard2.1 起可用）
//
// 注意：不要在这里补 System.Range / System.Index 之类的 BCL polyfill。
// 原程序集在 netstandard2.0 上并不引用它们（DLL 内无 System.Range 字样），
// 反编译产物里偶现的 [..N] 是伪影，应还原为 Substring，否则会往包里注入多余的公开类型、
// 破坏 API 对齐校验。

using System;
using System.Runtime.CompilerServices;

internal static class _003CPrivateImplementationDetails_003E
{
#if NETSTANDARD2_0
	// netstandard2.0 无 SwitchExpressionException，反编译产物的调用点也不传参。
	public static void ThrowInvalidOperationException()
		=> throw new InvalidOperationException("Unmatched value in switch expression.");
#else
	public static void ThrowSwitchExpressionException(object? unmatchedValue)
		=> throw new SwitchExpressionException(unmatchedValue);
#endif
}
