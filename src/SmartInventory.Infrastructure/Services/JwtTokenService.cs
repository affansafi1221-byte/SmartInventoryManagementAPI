using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SmartInventory.Application.Interfaces;
using SmartInventory.Domain.Entities;
using SmartInventory.Infrastructure.Options;

namespace SmartInventory.Infrastructure.Services;

public class JwtTokenService(JwtSettings settings) : IJwtTokenService
{
    public (string Token, DateTime ExpiresAt) Create(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.FullName),
            new("role", user.Role.Name)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}