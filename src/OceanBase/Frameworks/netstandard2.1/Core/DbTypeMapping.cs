using System;
using System.Data;
using System.Globalization;

namespace OceanBase.Core;

internal sealed class DbTypeMapping(Type clrType, DbType[] dbTypes, Func<object, object>? convert = null)
{
	public Type ClrType { get; } = clrType;

	public DbType[] DbTypes { get; } = dbTypes;

	public object DoConversion(object obj)
	{
		if (obj.GetType() == ClrType)
		{
			return obj;
		}
		if (convert != null)
		{
			return convert(obj);
		}
		return Convert.ChangeType(obj, ClrType, CultureInfo.InvariantCulture);
	}
}
