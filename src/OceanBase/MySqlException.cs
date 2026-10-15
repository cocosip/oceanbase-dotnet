using System;
using System.Collections;
using System.Data.Common;
using System.Runtime.Serialization;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.MySqlException" /> is thrown when MySQL Server returns an error code, or there is a
/// communication error with the server.
/// </summary>
[Serializable]
public sealed class MySqlException : DbException
{
	private IDictionary? m_data;

	/// <summary>
	/// A <see cref="T:OceanBase.MySqlErrorCode" /> value identifying the kind of error. Prefer to use the <see cref="P:OceanBase.MySqlException.ErrorCode" /> property.
	/// </summary>
	public int Number { get; }

	/// <summary>
	/// A <see cref="T:OceanBase.MySqlErrorCode" /> value identifying the kind of error.
	/// </summary>
	public new MySqlErrorCode ErrorCode { get; }

	/// <summary>
	/// A <c>SQLSTATE</c> code identifying the kind of error.
	/// </summary>
	/// <remarks>See <a href="https://en.wikipedia.org/wiki/SQLSTATE">SQLSTATE</a> for more information.</remarks>
	public override string? SqlState { get; }

	/// <summary>
	/// Returns <c>true</c> if this exception could indicate a transient error condition (that could succeed if retried); otherwise, <c>false</c>.
	/// </summary>
	public override bool IsTransient => IsErrorTransient(ErrorCode);

	/// <summary>
	/// Gets a collection of key/value pairs that provide additional information about the exception.
	/// </summary>
	public override IDictionary Data
	{
		get
		{
			if (m_data == null)
			{
				m_data = base.Data;
				m_data["Server Error Code"] = Number;
				m_data["SqlState"] = SqlState;
			}
			return m_data;
		}
	}

	[Obsolete(DiagnosticId = "SYSLIB0051")]
	private MySqlException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
		Number = info.GetInt32("Number");
		ErrorCode = (MySqlErrorCode)Number;
		SqlState = info.GetString("SqlState");
	}

	/// <summary>
	/// Sets the <see cref="T:System.Runtime.Serialization.SerializationInfo" /> with information about the exception.
	/// </summary>
	/// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> that will be set.</param>
	/// <param name="context">The context.</param>
	[Obsolete("Do not use legacy serialization infrastructure APIs", DiagnosticId = "SYSLIB0051")]
	public override void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		base.GetObjectData(info, context);
		info.AddValue("Number", Number);
		info.AddValue("SqlState", SqlState);
	}

	internal MySqlException(string message)
		: this(message, null)
	{
	}

	internal MySqlException(string message, Exception? innerException)
		: this(MySqlErrorCode.None, null, message, innerException)
	{
	}

	internal MySqlException(MySqlErrorCode errorCode, string message)
		: this(errorCode, null, message, null)
	{
	}

	internal MySqlException(MySqlErrorCode errorCode, string message, Exception? innerException)
		: this(errorCode, null, message, innerException)
	{
	}

	internal MySqlException(MySqlErrorCode errorCode, string sqlState, string message)
		: this(errorCode, sqlState, message, null)
	{
	}

	internal MySqlException(MySqlErrorCode errorCode, string? sqlState, string message, Exception? innerException)
		: base(message, innerException)
	{
		ErrorCode = errorCode;
		Number = (int)errorCode;
		SqlState = sqlState;
	}

	internal static MySqlException CreateForTimeout()
	{
		return CreateForTimeout(null);
	}

	internal static MySqlException CreateForTimeout(Exception? innerException)
	{
		return new MySqlException(MySqlErrorCode.CommandTimeoutExpired, "The Command Timeout expired before the operation completed.", innerException);
	}

	private static bool IsErrorTransient(MySqlErrorCode errorCode)
	{
		switch (errorCode)
		{
		case MySqlErrorCode.ConnectionCountError:
		case MySqlErrorCode.UnableToConnectToHost:
		case MySqlErrorCode.LockWaitTimeout:
		case MySqlErrorCode.LockDeadlock:
		case MySqlErrorCode.XARBDeadlock:
			return true;
		default:
			return false;
		}
	}
}
