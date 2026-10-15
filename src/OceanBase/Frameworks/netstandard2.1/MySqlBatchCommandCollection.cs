using System;
using System.Collections;
using System.Collections.Generic;

namespace OceanBase;

public sealed class MySqlBatchCommandCollection : IList<MySqlBatchCommand>, ICollection<MySqlBatchCommand>, IEnumerable<MySqlBatchCommand>, IEnumerable
{
	private readonly List<MySqlBatchCommand> m_commands;

	public MySqlBatchCommand this[int index]
	{
		get
		{
			return m_commands[index];
		}
		set
		{
			m_commands[index] = value;
		}
	}

	public int Count => m_commands.Count;

	public bool IsReadOnly => false;

	internal IReadOnlyList<MySqlBatchCommand> Commands => m_commands;

	internal MySqlBatchCommandCollection()
	{
		m_commands = new List<MySqlBatchCommand>();
	}

	public void Add(MySqlBatchCommand item)
	{
		m_commands.Add(item);
	}

	public void Clear()
	{
		m_commands.Clear();
	}

	public bool Contains(MySqlBatchCommand item)
	{
		return m_commands.Contains(item);
	}

	public void CopyTo(MySqlBatchCommand[] array, int arrayIndex)
	{
		throw new NotImplementedException();
	}

	public IEnumerator<MySqlBatchCommand> GetEnumerator()
	{
		foreach (MySqlBatchCommand command in m_commands)
		{
			yield return command;
		}
	}

	public int IndexOf(MySqlBatchCommand item)
	{
		return m_commands.IndexOf(item);
	}

	public void Insert(int index, MySqlBatchCommand item)
	{
		m_commands.Insert(index, item);
	}

	public bool Remove(MySqlBatchCommand item)
	{
		return m_commands.Remove(item);
	}

	public void RemoveAt(int index)
	{
		m_commands.RemoveAt(index);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
