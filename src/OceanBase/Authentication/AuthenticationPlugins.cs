using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace OceanBase.Authentication;

/// <summary>
/// A registry of known authentication plugins.
/// </summary>
public static class AuthenticationPlugins
{
	#if NET9_0_OR_GREATER
	#if NET9_0_OR_GREATER
	private static readonly Lock s_lock = new Lock();
	#else
	private static readonly object s_lock = new object();
	#endif
	#else
	private static readonly object s_lock = new object();
	#endif

	private static readonly Dictionary<string, IAuthenticationPlugin> s_plugins = new Dictionary<string, IAuthenticationPlugin>();

	/// <summary>
	/// Registers the specified authentication plugin. The name of this plugin must be unique.
	/// </summary>
	/// <param name="plugin">The authentication plugin.</param>
	public static void Register(IAuthenticationPlugin plugin)
	{
		ArgumentNullException.ThrowIfNull(plugin, "plugin");
		ArgumentException.ThrowIfNullOrEmpty(plugin.Name, "plugin.Name");
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
