using System;
using System.Collections;
using System.Collections.Generic;

namespace OceanBase;

/// <summary>
/// <see cref="T:OceanBase.MySqlAttributeCollection" /> represents a collection of query attributes that can be added to a <see cref="T:OceanBase.MySqlCommand" />.
/// </summary>
public sealed class MySqlAttributeCollection : IEnumerable<MySqlAttribute>, IEnumerable
{
	private readonly List<MySqlAttribute> m_attributes;

	/// <summary>
	/// Returns the number of attributes in the collection.
	/// </summary>
	public int Count => m_attributes.Count;

	/// <summary>
	/// Gets the attribute at the specified index.
	/// </summary>
	/// <param name="index">The index.</param>
	/// <returns>The <see cref="T:OceanBase.MySqlAttribute" /> at that index.</returns>
	public MySqlAttribute this[int index] => m_attributes[index];

	/// <summary>
	/// Adds a new <see cref="T:OceanBase.MySqlAttribute" /> to the collection.
	/// </summary>
	/// <param name="attribute">The attribute to add.</param>
	/// <remarks>The attribute name must not be empty, and must not already exist in the collection.</remarks>
	public void Add(MySqlAttribute attribute)
	{
		ArgumentNullException.ThrowIfNull(attribute, "attribute");
		ArgumentException.ThrowIfNullOrEmpty(attribute.AttributeName, "attribute.AttributeName");
		foreach (MySqlAttribute attribute2 in m_attributes)
		{
			if (attribute2.AttributeName == attribute.AttributeName)
			{
				throw new ArgumentException("An attribute with the name " + attribute.AttributeName + " already exists in the collection", "attribute");
			}
		}
		m_attributes.Add(attribute);
	}

	/// <summary>
	/// Sets the attribute with the specified name to the given value, overwriting it if it already exists.
	/// </summary>
	/// <param name="attributeName">The attribute name.</param>
	/// <param name="value">The attribute value.</param>
	public void SetAttribute(string attributeName, object? value)
	{
		ArgumentException.ThrowIfNullOrEmpty(attributeName, "attributeName");
		for (int i = 0; i < m_attributes.Count; i++)
		{
			if (m_attributes[i].AttributeName == attributeName)
			{
				m_attributes[i] = new MySqlAttribute(attributeName, value);
				return;
			}
		}
		m_attributes.Add(new MySqlAttribute(attributeName, value));
	}

	/// <summary>
	/// Clears the collection.
	/// </summary>
	public void Clear()
	{
		m_attributes.Clear();
	}

	/// <summary>
	/// Returns an enumerator for the collection.
	/// </summary>
	public IEnumerator<MySqlAttribute> GetEnumerator()
	{
		return m_attributes.GetEnumerator();
	}

	/// <summary>
	/// Removes the specified attribute from the collection.
	/// </summary>
	/// <param name="attribute">The attribute to remove.</param>
	/// <returns><c>true</c> if that attribute was removed; otherwise, <c>false</c>.</returns>
	public bool Remove(MySqlAttribute attribute)
	{
		return m_attributes.Remove(attribute);
	}

	/// <summary>
	/// Returns an enumerator for the collection.
	/// </summary>
	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	internal MySqlAttributeCollection()
	{
		m_attributes = new List<MySqlAttribute>();
	}
}
