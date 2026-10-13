using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Kadari.Application.Abstractions.Identity;
using Kadari.Domain.Identity;

namespace Kadari.Infrastructure.Security;

/// <summary>Issues signed JWT access tokens (HS256) with the user's id, mobile and role claims.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public AccessToken Issue(UserAccount user, SessionId sessionId)
    {
        var settings = options.Value;
        var expiresAt = clock.GetUtcNow().AddMinutes(settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new(JwtRegisteredClaimNames.Sid, sessionId.Value.ToString()),
            new("mobile", user.MobileNumber),
            new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}".Trim()),
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.ToString())));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expiresAt);
    }
}
