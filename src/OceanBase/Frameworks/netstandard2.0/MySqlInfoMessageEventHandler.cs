namespace OceanBase;

/// <summary>
/// Defines the event handler for <see cref="E:OceanBase.MySqlConnection.InfoMessage" />.
/// </summary>
/// <param name="sender">The sender. This is the associated <see cref="T:OceanBase.MySqlConnection" />.</param>
/// <param name="args">The <see cref="T:OceanBase.MySqlInfoMessageEventArgs" /> containing the errors.</param>
public delegate void MySqlInfoMessageEventHandler(object sender, MySqlInfoMessageEventArgs args);
