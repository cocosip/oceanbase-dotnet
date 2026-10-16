using System;
using System.Data;
using System.Reflection;
using OceanBase.Core;
using Xunit;

namespace OceanBase.Tests;

/// <summary>
/// ArrayBinding（数组绑定）参数校验逻辑的离线测试（internal，无需服务端）。
///
/// <see cref="SingleCommandPayloadCreator"/> 在发送 COM_STMT_PREPARE_EXECUTE 前会
/// 校验 OracleParameter 数组绑定的方向、形状、元素类型一致性；这些校验是纯函数，
/// 只依赖参数对象本身。
/// </summary>
public sealed class ArrayBindingValidationTests
{
	private const BindingFlags StaticFlags = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public;

	private static void InvokeValidate(OracleParameter[] parameters, int arrayBindCount, out Array[] arrayValues, out OracleDbType[] elementTypes)
	{
		arrayValues = new Array[parameters.Length];
		elementTypes = new OracleDbType[parameters.Length];
		var method = typeof(SingleCommandPayloadCreator).GetMethod("ValidateOracleArrayBindingParameters",
			StaticFlags, null, new[] { typeof(OracleParameter[]), typeof(int), typeof(Array[]), typeof(OracleDbType[]) }, null)!;
		method.Invoke(null, new object[] { parameters, arrayBindCount, arrayValues, elementTypes });
	}

	private static OracleParameter ArrayParameter<T>(params T[] values) => new()
	{
		ParameterName = "p",
		Value = values,
	};

	// ---------- 校验失败路径 ----------

	[Fact]
	public void Validate_rejects_empty_parameter_list()
	{
		var ex = Assert.Throws<TargetInvocationException>(() => InvokeValidate(Array.Empty<OracleParameter>(), 3, out _, out _));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	[Fact]
	public void Validate_rejects_non_input_direction()
	{
		var parameter = new OracleParameter { ParameterName = "p", Direction = ParameterDirection.Output, Value = new[] { 1, 2, 3 } };

		var ex = Assert.Throws<TargetInvocationException>(() => InvokeValidate(new[] { parameter }, 3, out _, out _));
		Assert.IsType<NotSupportedException>(ex.InnerException);
	}

	[Fact]
	public void Validate_rejects_scalar_value()
	{
		var parameter = new OracleParameter { ParameterName = "p", Value = 42 };

		var ex = Assert.Throws<TargetInvocationException>(() => InvokeValidate(new[] { parameter }, 1, out _, out _));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	[Fact]
	public void Validate_rejects_binary_array()
	{
		// byte[]/char[] 是「值」而非数组绑定集合，必须用 byte[][] 表示 RAW 数组
		var parameter = new OracleParameter { ParameterName = "p", Value = new byte[] { 1, 2 } };

		var ex = Assert.Throws<TargetInvocationException>(() => InvokeValidate(new[] { parameter }, 2, out _, out _));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	[Fact]
	public void Validate_rejects_multidimensional_array()
	{
		var parameter = new OracleParameter { ParameterName = "p", Value = new int[2, 2] };

		var ex = Assert.Throws<TargetInvocationException>(() => InvokeValidate(new[] { parameter }, 4, out _, out _));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	[Fact]
	public void Validate_rejects_length_mismatch_with_array_bind_count()
	{
		var ex = Assert.Throws<TargetInvocationException>(() => InvokeValidate(new[] { ArrayParameter(1, 2) }, 3, out _, out _));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	[Fact]
	public void Validate_rejects_array_bind_size_mismatch()
	{
		var parameter = ArrayParameter(1, 2, 3);
		parameter.ArrayBindSize = new[] { 4, 4 };

		var ex = Assert.Throws<TargetInvocationException>(() => InvokeValidate(new[] { parameter }, 3, out _, out _));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	[Fact]
	public void Validate_rejects_mixed_element_types()
	{
		var parameter = new OracleParameter { ParameterName = "p", Value = new object[] { 1, "text", 3 } };

		var ex = Assert.Throws<TargetInvocationException>(() => InvokeValidate(new[] { parameter }, 3, out _, out _));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	[Fact]
	public void Validate_rejects_element_conversion_failure_for_explicit_db_type()
	{
		var parameter = new OracleParameter("p", OracleDbType.Int32) { Value = new object[] { 1, "not-a-number" } };

		var ex = Assert.Throws<TargetInvocationException>(() => InvokeValidate(new[] { parameter }, 2, out _, out _));
		Assert.IsType<InvalidOperationException>(ex.InnerException);
	}

	// ---------- 校验通过路径 ----------

	[Fact]
	public void Validate_accepts_uniform_int_array_and_infers_element_type()
	{
		InvokeValidate(new[] { ArrayParameter(1, 2, 3) }, 3, out var arrayValues, out var elementTypes);

		Assert.Equal(new[] { 1, 2, 3 }, (int[])arrayValues[0]!);
		Assert.Equal(OracleDbType.Int32, elementTypes[0]);
	}

	[Fact]
	public void Validate_accepts_null_cells_and_uses_first_non_null_for_inference()
	{
		// OracleUdt 的 NULL 哨兵值跳过；推断以第一个非空元素为准
		var parameter = new OracleParameter { ParameterName = "p", Value = new object?[] { null, 5 } };

		InvokeValidate(new[] { parameter }, 2, out _, out var elementTypes);

		Assert.Equal(OracleDbType.Int32, elementTypes[0]);
	}

	[Fact]
	public void Validate_accepts_explicit_db_type_with_convertible_elements()
	{
		// 元素类型必须一致（混合类型在校验前段就被拒绝）；显式 DbType 时逐元素校验可转换性
		var parameter = new OracleParameter("p", OracleDbType.Int32) { Value = new[] { "1", "2", "3" } };

		InvokeValidate(new[] { parameter }, 3, out _, out var elementTypes);

		Assert.Equal(OracleDbType.Int32, elementTypes[0]);
	}

	[Fact]
	public void Validate_accepts_array_bind_size_matching_count()
	{
		var parameter = ArrayParameter(1, 2, 3);
		parameter.ArrayBindSize = new[] { 4, 4, 4 };

		InvokeValidate(new[] { parameter }, 3, out _, out _);
	}

	// ---------- UDT 类型名解析 ----------

	[Theory]
	[InlineData("SCHEMA.TYPE_NAME", "SCHEMA", "TYPE_NAME")]
	[InlineData("TYPE_NAME", "", "TYPE_NAME")]
	[InlineData("A.B.C", "A", "B.C")]
	public void ParseUdtTypeName_splits_on_first_dot(string input, string schema, string typeName)
	{
		var method = typeof(SingleCommandPayloadCreator).GetMethod("ParseUdtTypeName", StaticFlags, null, new[] { typeof(string) }, null)!;
		var result = ((string, string))method.Invoke(null, new object[] { input })!;

		Assert.Equal((schema, typeName), result);
	}
}
