using System;
using System.Collections.Generic;
using System.Data.Common;

namespace OceanBase;

public sealed class MySqlBatchCommandCollection : DbBatchCommandCollection
{
	private readonly List<MySqlBatchCommand> m_commands;

	public new MySqlBatchCommand this[int index]
	{
		get
		{
			return (MySqlBatchCommand)base[index];
		}
		set
		{
			base[index] = value;
		}
	}

	public override int Count => m_commands.Count;

	public override bool IsReadOnly => false;

	internal IReadOnlyList<MySqlBatchCommand> Commands => m_commands;

	internal MySqlBatchCommandCollection()
	{
		m_commands = new List<MySqlBatchCommand>();
	}

	public override void Add(DbBatchCommand item)
	{
		m_commands.Add((MySqlBatchCommand)item);
	}

	public override void Clear()
	{
		m_commands.Clear();
	}

	public override bool Contains(DbBatchCommand item)
	{
		return m_commands.Contains((MySqlBatchCommand)item);
	}

	public override void CopyTo(DbBatchCommand[] array, int arrayIndex)
	{
		throw new NotImplementedException();
	}

	public override IEnumerator<DbBatchCommand> GetEnumerator()
	{
		foreach (MySqlBatchCommand command in m_commands)
		{
			yield return command;
		}
	}

	public override int IndexOf(DbBatchCommand item)
	{
		return m_commands.IndexOf((MySqlBatchCommand)item);
	}

	public override void Insert(int index, DbBatchCommand item)
	{
		m_commands.Insert(index, (MySqlBatchCommand)item);
	}

	public override bool Remove(DbBatchCommand item)
	{
		return m_commands.Remove((MySqlBatchCommand)item);
	}

	public override void RemoveAt(int index)
	{
		m_commands.RemoveAt(index);
	}

	protected override DbBatchCommand GetBatchCommand(int index)
	{
		return m_commands[index];
	}

	protected override void SetBatchCommand(int index, DbBatchCommand batchCommand)
	{
		m_commands[index] = (MySqlBatchCommand)batchCommand;
	}
}
