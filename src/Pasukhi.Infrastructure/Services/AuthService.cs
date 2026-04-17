using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pasukhi.Application.DTOs.Auth;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<AdminUser> _userManager;
    private readonly PasukhiDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(UserManager<AdminUser> userManager, PasukhiDbContext db, IConfiguration config)
    {
        _userManager = userManager;
        _db = db;
        _config = config;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = GenerateAccessToken(user, roles);
        var rawRefreshToken = await CreateRefreshTokenAsync(user.Id);
        await _db.SaveChangesAsync();

        return new AuthResponse(accessToken, rawRefreshToken, ToDto(user, roles));
    }

    public async Task<AuthResponse> RefreshTokenAsync(string tokenHash)
    {
        var storedToken = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == tokenHash && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);

        if (storedToken == null)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        storedToken.IsRevoked = true;
        var rawRefreshToken = await CreateRefreshTokenAsync(storedToken.UserId);

        var roles = await _userManager.GetRolesAsync(storedToken.User);
        var accessToken = GenerateAccessToken(storedToken.User, roles);

        await _db.SaveChangesAsync();

        return new AuthResponse(accessToken, rawRefreshToken, ToDto(storedToken.User, roles));
    }

    public async Task LogoutAsync(string userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<AdminUserDto> GetCurrentUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");
        var roles = await _userManager.GetRolesAsync(user);
        return ToDto(user, roles);
    }

    private string GenerateAccessToken(AdminUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new("FirstName", user.FirstName),
            new("LastName", user.LastName),
            new(ClaimTypes.Role, roles.FirstOrDefault() ?? "Operator"),
        };

        if (user.BusinessId.HasValue)
        {
            claims.Add(new Claim("BusinessId", user.BusinessId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(
            int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "15"));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Task<string> CreateRefreshTokenAsync(string userId)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hash = HashRefreshToken(rawToken);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Token = hash,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "7")),
            CreatedAt = DateTime.UtcNow
        });

        return Task.FromResult(rawToken);
    }

    public static string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static AdminUserDto ToDto(AdminUser user, IList<string> roles) =>
        new(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            roles.FirstOrDefault() ?? "Operator",
            user.BusinessId);
}
