using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DeveloperMemory.Api.Infrastructure.Authentication;

/// <summary>
/// Supplies a deterministic local identity only when the application is running in Development
/// and the explicit development bypass setting is enabled.
/// </summary>
public sealed class DevelopmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ApiKeySettings _settings;
    private readonly IHostEnvironment _environment;

    public DevelopmentAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<ApiKeySettings> settings,
        IHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _settings = settings.Value;
        _environment = environment;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_environment.IsDevelopment() || !_settings.DevelopmentBypass)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (string.IsNullOrWhiteSpace(_settings.DevelopmentOwnerId))
        {
            return Task.FromResult(AuthenticateResult.Fail("DevelopmentOwnerId must be configured when the development bypass is enabled."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _settings.DevelopmentOwnerId),
            new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(_settings.DevelopmentOwnerDisplayName)
                ? _settings.DevelopmentOwnerId
                : _settings.DevelopmentOwnerDisplayName),
            new Claim("development_bypass", "true")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
