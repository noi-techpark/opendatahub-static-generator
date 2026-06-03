namespace GeneratorWorker.Models;

public enum AuthType
{
    None,
    Basic,
    ClientCredentials
}

public class AuthConfig
{
    public AuthType Type { get; set; } = AuthType.None;

    // Basic auth
    public string? Username { get; set; }
    public string? Password { get; set; }

    // OAuth2 Client Credentials
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? TokenUrl { get; set; }
    public string? Scope { get; set; }
}
