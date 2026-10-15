using System;
using System.Runtime.Serialization;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.MySqlConversionException" /> is thrown when a MySQL value can't be converted to another type.
/// </summary>
[Serializable]
public sealed class MySqlConversionException : Exception
{
	/// <summary>
	/// Initializes a new instance of <see cref="T:OceanBase.MySqlConversionException" />.
	/// </summary>
	/// <param name="message">The exception message.</param>
	internal MySqlConversionException(string message)
		: base(message)
	{
	}

	private MySqlConversionException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}
