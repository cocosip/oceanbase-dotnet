using System;

namespace OceanBase.Authentication;

/// <summary>
/// <see cref="T:OceanBase.Authentication.IAuthenticationPlugin2" /> is an extension to <see cref="T:OceanBase.Authentication.IAuthenticationPlugin" /> that returns a hash of the client's password.
/// </summary>
[Obsolete("Use IAuthenticationPlugin3 instead.")]
public interface IAuthenticationPlugin2 : IAuthenticationPlugin
{
	/// <summary>
	/// Hashes the client's password (e.g., for TLS certificate fingerprint verification).
	/// </summary>
	/// <param name="password">The client's password.</param>
	/// <param name="authenticationData">The authentication data supplied by the server; this is the <code>auth method data</code>
	/// from the <a href="https://dev.mysql.com/doc/internals/en/connection-phase-packets.html#packet-Protocol::AuthSwitchRequest">Authentication
	/// Method Switch Request Packet</a>.</param>
	/// <returns>The authentication-method-specific hash of the client's password.</returns>
	byte[] CreatePasswordHash(string password, ReadOnlySpan<byte> authenticationData);
}
