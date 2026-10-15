using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace OceanBase;

public sealed class OracleParameterCollection : DbParameterCollection, IEnumerable<OracleParameter>, IEnumerable
{
	private readonly List<OracleParameter> m_parameters;

	private readonly Dictionary<string, int> m_nameToIndex;

	public override bool IsFixedSize => false;

	public override bool IsReadOnly => false;

	public override bool IsSynchronized => false;

	public override int Count => m_parameters.Count;

	public override object SyncRoot
	{
		get
		{
			throw new NotSupportedException();
		}
	}

	public new OracleParameter this[int index]
	{
		get
		{
			return m_parameters[index];
		}
		set
		{
			SetParameter(index, value);
		}
	}

	public new OracleParameter this[string name]
	{
		get
		{
			return (OracleParameter)GetParameter(name);
		}
		set
		{
			SetParameter(name, value);
		}
	}

	internal OracleParameterCollection()
	{
		m_parameters = new List<OracleParameter>();
		m_nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
	}

	public OracleParameter Add(string parameterName, DbType dbType)
	{
		OracleParameter oracleParameter = new OracleParameter
		{
			ParameterName = parameterName,
			DbType = dbType
		};
		AddParameter(oracleParameter, m_parameters.Count);
		return oracleParameter;
	}

	public override int Add(object value)
	{
		ArgumentNullException.ThrowIfNull(value, "value");
		AddParameter((OracleParameter)value, m_parameters.Count);
		return m_parameters.Count - 1;
	}

	public OracleParameter Add(OracleParameter parameter)
	{
		ArgumentNullException.ThrowIfNull(parameter, "parameter");
		AddParameter(parameter, m_parameters.Count);
		return parameter;
	}

	public OracleParameter Add(string parameterName, OracleDbType oracleDbType)
	{
		return Add(new OracleParameter(parameterName, oracleDbType));
	}

	public OracleParameter Add(string parameterName, object value)
	{
		return Add(new OracleParameter(parameterName, value));
	}

	public OracleParameter Add(string parameterName, OracleDbType oracleDbType, int size)
	{
		return Add(new OracleParameter(parameterName, oracleDbType, size));
	}

	public OracleParameter Add(string parameterName, OracleDbType oracleDbType, object value, ParameterDirection direction)
	{
		return Add(new OracleParameter(parameterName, oracleDbType, value, direction));
	}

	public OracleParameter Add(string parameterName, OracleDbType oracleDbType, int size, ParameterDirection parameterDirection, bool isNullable, byte precision, byte scale, string sourceColumn, DataRowVersion version, object value)
	{
		return Add(new OracleParameter(parameterName, oracleDbType, size, parameterDirection, isNullable, precision, scale, sourceColumn, version, value));
	}

	public override void AddRange(Array values)
	{
		foreach (object value in values)
		{
			Add(value);
		}
	}

	public OracleParameter AddWithValue(string parameterName, object? value)
	{
		OracleParameter oracleParameter = new OracleParameter
		{
			ParameterName = parameterName,
			Value = value
		};
		AddParameter(oracleParameter, m_parameters.Count);
		return oracleParameter;
	}

	public override bool Contains(object value)
	{
		if (value is OracleParameter item)
		{
			return m_parameters.Contains(item);
		}
		return false;
	}

	public override bool Contains(string value)
	{
		return IndexOf(value) != -1;
	}

	public override void CopyTo(Array array, int index)
	{
		((ICollection)m_parameters).CopyTo(array, index);
	}

	public override void Clear()
	{
		foreach (OracleParameter parameter in m_parameters)
		{
			parameter.ParameterCollection = null;
		}
		m_parameters.Clear();
		m_nameToIndex.Clear();
	}

	public override IEnumerator GetEnumerator()
	{
		return m_parameters.GetEnumerator();
	}

	IEnumerator<OracleParameter> IEnumerable<OracleParameter>.GetEnumerator()
	{
		return m_parameters.GetEnumerator();
	}

	protected override DbParameter GetParameter(int index)
	{
		return m_parameters[index];
	}

	protected override DbParameter GetParameter(string parameterName)
	{
		int num = IndexOf(parameterName);
		if (num == -1)
		{
			throw new ArgumentException("Parameter '" + parameterName + "' not found in the collection", "parameterName");
		}
		return m_parameters[num];
	}

	public override int IndexOf(object value)
	{
		if (!(value is OracleParameter item))
		{
			return -1;
		}
		return m_parameters.IndexOf(item);
	}

	public override int IndexOf(string parameterName)
	{
		return NormalizedIndexOf(parameterName);
	}

	internal int NormalizedIndexOf(string? parameterName)
	{
		return UnsafeIndexOf(OracleParameter.NormalizeParameterName(parameterName ?? ""));
	}

	internal int UnsafeIndexOf(string? normalizedParameterName)
	{
		if (!m_nameToIndex.TryGetValue(normalizedParameterName ?? "", out var value))
		{
			return -1;
		}
		return value;
	}

	public override void Insert(int index, object value)
	{
		AddParameter((OracleParameter)(value ?? throw new ArgumentNullException("value")), index);
	}

	public override void Remove(object value)
	{
		RemoveAt(IndexOf(value ?? throw new ArgumentNullException("value")));
	}

	public override void RemoveAt(int index)
	{
		OracleParameter oracleParameter = m_parameters[index];
		if (oracleParameter.NormalizedParameterName != null)
		{
			m_nameToIndex.Remove(oracleParameter.NormalizedParameterName);
		}
		oracleParameter.ParameterCollection = null;
		m_parameters.RemoveAt(index);
		foreach (KeyValuePair<string, int> item in m_nameToIndex.ToList())
		{
			if (item.Value > index)
			{
				m_nameToIndex[item.Key] = item.Value - 1;
			}
		}
	}

	public override void RemoveAt(string parameterName)
	{
		RemoveAt(IndexOf(parameterName));
	}

	protected override void SetParameter(int index, DbParameter value)
	{
		ArgumentNullException.ThrowIfNull(value, "value");
		OracleParameter oracleParameter = (OracleParameter)value;
		OracleParameter oracleParameter2 = m_parameters[index];
		if (oracleParameter2.NormalizedParameterName != null)
		{
			m_nameToIndex.Remove(oracleParameter2.NormalizedParameterName);
		}
		oracleParameter2.ParameterCollection = null;
		m_parameters[index] = oracleParameter;
		if (oracleParameter.NormalizedParameterName != null)
		{
			m_nameToIndex.Add(oracleParameter.NormalizedParameterName, index);
		}
		oracleParameter.ParameterCollection = this;
	}

	protected override void SetParameter(string parameterName, DbParameter value)
	{
		SetParameter(IndexOf(parameterName), value);
	}

	internal void ChangeParameterName(OracleParameter parameter, string oldName, string newName)
	{
		if (m_nameToIndex.TryGetValue(oldName, out var value) && m_parameters[value] == parameter)
		{
			m_nameToIndex.Remove(oldName);
		}
		else
		{
			value = m_parameters.IndexOf(parameter);
		}
		if (newName.Length != 0)
		{
			if (m_nameToIndex.ContainsKey(newName))
			{
				throw new MySqlException("There is already a parameter with the name '" + parameter.ParameterName + "' in this collection.");
			}
			m_nameToIndex[newName] = value;
		}
	}

	private void AddParameter(OracleParameter parameter, int index)
	{
		if (!string.IsNullOrEmpty(parameter.NormalizedParameterName) && NormalizedIndexOf(parameter.NormalizedParameterName) != -1)
		{
			throw new MySqlException("Parameter '" + parameter.ParameterName + "' has already been defined.");
		}
		if (index < m_parameters.Count)
		{
			foreach (KeyValuePair<string, int> item in m_nameToIndex.ToList())
			{
				if (item.Value >= index)
				{
					m_nameToIndex[item.Key] = item.Value + 1;
				}
			}
		}
		m_parameters.Insert(index, parameter);
		if (!string.IsNullOrEmpty(parameter.NormalizedParameterName))
		{
			m_nameToIndex[parameter.NormalizedParameterName] = index;
		}
		parameter.ParameterCollection = this;
	}
}
