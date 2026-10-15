using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace OceanBase.Authentication;

/// <summary>
/// A registry of known authentication plugins.
/// </summary>
public static class AuthenticationPlugins
{
	private static readonly object s_lock = new object();

	private static readonly Dictionary<string, IAuthenticationPlugin> s_plugins = new Dictionary<string, IAuthenticationPlugin>();

	/// <summary>
	/// Registers the specified authentication plugin. The name of this plugin must be unique.
	/// </summary>
	/// <param name="plugin">The authentication plugin.</param>
	public static void Register(IAuthenticationPlugin plugin)
	{
		ExceptionExtensions.ThrowIfNull(plugin, "plugin");
		ExceptionExtensions.ThrowIfNullOrEmpty(plugin.Name, "plugin.Name");
		lock (s_lock)
		{
			s_plugins.Add(plugin.Name, plugin);
		}
	}

	internal static bool TryGetPlugin(string name, [NotNullWhen(true)] out IAuthenticationPlugin? plugin)
	{
		lock (s_lock)
		{
			return s_plugins.TryGetValue(name, out plugin);
		}
	}
}
