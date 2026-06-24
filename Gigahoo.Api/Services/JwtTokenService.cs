using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Gigahoo.Api.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Gigahoo.Api.Services;

public interface IJwtTokenService
{
    string GenerateAccessToken(Account account);
    ClaimsPrincipal? ValidateToken(string token);
}

public class JwtTokenService(IConfiguration config) : IJwtTokenService
{
    public string GenerateAccessToken(Account account)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new("account_id", account.Id.ToString()),
            new(ClaimTypes.Email, account.Email ?? string.Empty),
        };

        if (account.DisplayName is not null)
            claims.Add(new Claim(ClaimTypes.Name, account.DisplayName));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(double.Parse(config["Jwt:ExpirationDays"] ?? "7")),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = config["Jwt:Issuer"],
                ValidAudience = config["Jwt:Audience"],
                IssuerSigningKey = key,
            }, out _);
        }
        catch
        {
            return null;
        }
    }
}
