using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;

namespace HyperV.CentralManagement.Services;

public class LdapOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 636;
    public bool UseSsl { get; set; } = true;
    public string BaseDn { get; set; } = string.Empty;
    public string UserDnTemplate { get; set; } = string.Empty;
}

public class LdapAuthService
{
    private readonly LdapOptions _options;

    public LdapAuthService(IOptions<LdapOptions> options)
    {
        _options = options.Value;
    }

    public bool IsEnabled => _options.Enabled;

    public bool ValidateCredentials(string username, string password, out string error)
    {
        error = string.Empty;
        if (!_options.Enabled)
        {
            error = "LDAP authentication is disabled.";
            return false;
        }

        if (!_options.UseSsl || _options.Port != 636)
        {
            error = "LDAP SSL (ldaps) is required.";
            return false;
        }

        var userDn = string.Format(_options.UserDnTemplate, username);
        var identifier = new LdapDirectoryIdentifier(_options.Host, _options.Port, false, false);
        using var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            SessionOptions =
            {
                SecureSocketLayer = true,
                ProtocolVersion = 3
            }
        };

        connection.Credential = new NetworkCredential(userDn, password);

        try
        {
            connection.Bind();
            return true;
        }
        catch (LdapException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
