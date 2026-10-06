using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LeaseLedger.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace LeaseLedger.Web.Identity;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public string Create(AppUser user, string role)
    {
        var key = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(ClaimTypes.Name, user.Email ?? ""),
            new(ClaimTypes.GivenName, user.DisplayName),
            new(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(14),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
