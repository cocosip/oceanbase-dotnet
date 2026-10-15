using System;

namespace OceanBase.Authentication;

/// <summary>
/// <see cref="T:OceanBase.Authentication.IAuthenticationPlugin3" /> is an extension to <see cref="T:OceanBase.Authentication.IAuthenticationPlugin" /> that also returns a hash of the client's password.
/// </summary>
/// <remarks>If an authentication plugin supports this interface, the base <see cref="M:OceanBase.Authentication.IAuthenticationPlugin.CreateResponse(System.String,System.ReadOnlySpan{System.Byte})" /> method will not be called.</remarks>
public interface IAuthenticationPlugin3 : IAuthenticationPlugin
{
	/// <summary>
	/// Creates the authentication response and hashes the client's password (e.g., for TLS certificate fingerprint verification).
	/// </summary>
	/// <param name="password">The client's password.</param>
	/// <param name="authenticationData">The authentication data supplied by the server; this is the <code>auth method data</code>
	/// from the <a href="https://dev.mysql.com/doc/internals/en/connection-phase-packets.html#packet-Protocol::AuthSwitchRequest">Authentication
	/// Method Switch Request Packet</a>.</param>
	/// <param name="authenticationResponse">The authentication response.</param>
	/// <param name="passwordHash">The authentication-method-specific hash of the client's password.</param>
	void CreateResponseAndPasswordHash(string password, ReadOnlySpan<byte> authenticationData, out byte[] authenticationResponse, out byte[] passwordHash);
}
