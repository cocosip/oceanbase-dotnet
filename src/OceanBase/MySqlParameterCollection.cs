using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace OceanBase;

public sealed class MySqlParameterCollection : DbParameterCollection, IEnumerable<MySqlParameter>, IEnumerable
{
	private readonly List<MySqlParameter> m_parameters;

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

	public new MySqlParameter this[int index]
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

	public new MySqlParameter this[string name]
	{
		get
		{
			return (MySqlParameter)GetParameter(name);
		}
		set
		{
			SetParameter(name, value);
		}
	}

	internal MySqlParameterCollection()
	{
		m_parameters = new List<MySqlParameter>();
		m_nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
	}

	public MySqlParameter Add(string parameterName, DbType dbType)
	{
		MySqlParameter mySqlParameter = new MySqlParameter
		{
			ParameterName = parameterName,
			DbType = dbType
		};
		AddParameter(mySqlParameter, m_parameters.Count);
		return mySqlParameter;
	}

	public override int Add(object value)
	{
		ArgumentNullException.ThrowIfNull(value, "value");
		AddParameter((MySqlParameter)value, m_parameters.Count);
		return m_parameters.Count - 1;
	}

	public MySqlParameter Add(MySqlParameter parameter)
	{
		ArgumentNullException.ThrowIfNull(parameter, "parameter");
		AddParameter(parameter, m_parameters.Count);
		return parameter;
	}

	public MySqlParameter Add(string parameterName, MySqlDbType mySqlDbType)
	{
		return Add(new MySqlParameter(parameterName, mySqlDbType));
	}

	public MySqlParameter Add(string parameterName, MySqlDbType mySqlDbType, int size)
	{
		return Add(new MySqlParameter(parameterName, mySqlDbType, size));
	}

	public override void AddRange(Array values)
	{
		foreach (object value in values)
		{
			Add(value);
		}
	}

	public MySqlParameter AddWithValue(string parameterName, object? value)
	{
		MySqlParameter mySqlParameter = new MySqlParameter
		{
			ParameterName = parameterName,
			Value = value
		};
		AddParameter(mySqlParameter, m_parameters.Count);
		return mySqlParameter;
	}

	public override bool Contains(object value)
	{
		if (value is MySqlParameter item)
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
		foreach (MySqlParameter parameter in m_parameters)
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

	IEnumerator<MySqlParameter> IEnumerable<MySqlParameter>.GetEnumerator()
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
		if (!(value is MySqlParameter item))
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
		return UnsafeIndexOf(MySqlParameter.NormalizeParameterName(parameterName ?? ""));
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
		AddParameter((MySqlParameter)(value ?? throw new ArgumentNullException("value")), index);
	}

	public override void Remove(object value)
	{
		RemoveAt(IndexOf(value ?? throw new ArgumentNullException("value")));
	}

	public override void RemoveAt(int index)
	{
		MySqlParameter mySqlParameter = m_parameters[index];
		if (mySqlParameter.NormalizedParameterName != null)
		{
			m_nameToIndex.Remove(mySqlParameter.NormalizedParameterName);
		}
		mySqlParameter.ParameterCollection = null;
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
		MySqlParameter mySqlParameter = (MySqlParameter)value;
		MySqlParameter mySqlParameter2 = m_parameters[index];
		if (mySqlParameter2.NormalizedParameterName != null)
		{
			m_nameToIndex.Remove(mySqlParameter2.NormalizedParameterName);
		}
		mySqlParameter2.ParameterCollection = null;
		m_parameters[index] = mySqlParameter;
		if (mySqlParameter.NormalizedParameterName != null)
		{
			m_nameToIndex.Add(mySqlParameter.NormalizedParameterName, index);
		}
		mySqlParameter.ParameterCollection = this;
	}

	protected override void SetParameter(string parameterName, DbParameter value)
	{
		SetParameter(IndexOf(parameterName), value);
	}

	internal void ChangeParameterName(MySqlParameter parameter, string oldName, string newName)
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

	private void AddParameter(MySqlParameter parameter, int index)
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
