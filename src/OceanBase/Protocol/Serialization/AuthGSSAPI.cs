using System;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OceanBase.Core;

namespace OceanBase.Protocol.Serialization;

internal static class AuthGSSAPI
{
	private static string GetServicePrincipalName(byte[] switchRequest)
	{
		ByteArrayReader byteArrayReader = new ByteArrayReader(switchRequest.AsSpan());
		return Encoding.UTF8.GetString(byteArrayReader.ReadNullOrEofTerminatedByteString());
	}

	public static async Task<PayloadData> AuthenticateAsync(ConnectionSettings cs, byte[] switchRequestPayloadData, ServerSession session, IOBehavior ioBehavior, CancellationToken cancellationToken)
	{
		using NegotiateToMySqlConverterStream innerStream = new NegotiateToMySqlConverterStream(session, ioBehavior, cancellationToken);
		using NegotiateStream negotiateStream = new NegotiateStream(innerStream);
		string targetName = ((cs.ServerSPN.Length == 0) ? GetServicePrincipalName(switchRequestPayloadData) : cs.ServerSPN);
		if (ioBehavior != IOBehavior.Synchronous)
		{
			await negotiateStream.AuthenticateAsClientAsync(CredentialCache.DefaultNetworkCredentials, targetName).ConfigureAwait(continueOnCapturedContext: false);
		}
		else
		{
			negotiateStream.AuthenticateAsClient(CredentialCache.DefaultNetworkCredentials, targetName);
		}
		if (cs.ServerSPN.Length != 0 && !negotiateStream.IsMutuallyAuthenticated)
		{
			throw new AuthenticationException("GSSAPI : Unable to verify server principal name using authentication type " + negotiateStream.RemoteIdentity?.AuthenticationType);
		}
		return innerStream.MySQLProtocolPayload ?? (await session.ReceiveReplyAsync(ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
	}
}
