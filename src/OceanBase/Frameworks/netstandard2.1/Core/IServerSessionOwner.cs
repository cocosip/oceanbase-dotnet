using System;
using System.Data;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using OceanBase.Plugins;

namespace OceanBase.Core;

/// <summary>
/// Provides the minimal surface that <see cref="T:OceanBase.Core.ServerSession" /> needs from a
/// connection without tying it to a concrete connection type.
/// </summary>
internal interface IServerSessionOwner
{
	ZstandardPlugin? ZstandardPlugin { get; }

	Func<X509CertificateCollection, ValueTask>? ProvideClientCertificatesCallback { get; }

	Func<MySqlProvidePasswordContext, string>? ProvidePasswordCallback { get; }

	RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; }

	void SetState(ConnectionState newState);
}
