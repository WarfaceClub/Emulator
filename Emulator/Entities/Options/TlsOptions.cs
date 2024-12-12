using System.Security.Authentication;

namespace Emulator.Entities.Options;

public class TlsOptions
{
    public bool GenerateSelfSignedCertificate { get; set; }
    public bool IsOptional { get; set; }
    public string CertificateFile { get; set; }
    public string CertificatePassword { get; set; }
    public IEnumerable<SslProtocols> HandshakeProtocols { get; set; }
}
