using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
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

        if (user == null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new UnauthorizedAccessException("Account is temporarily locked. Try again later.");

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = GenerateAccessToken(user, roles);
        var rawRefreshToken = await CreateRefreshTokenAsync(user.Id);
        await _db.SaveChangesAsync();

        return new AuthResponse(accessToken, rawRefreshToken, await ToDtoAsync(user, roles));
    }

    public async Task<AuthResponse> ExternalLoginAsync(string provider, string providerId, string email, string firstName, string lastName)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.ExternalProvider == provider && u.ExternalProviderId == providerId);

        if (user == null && !string.IsNullOrEmpty(email))
        {
            user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                user.ExternalProvider = provider;
                user.ExternalProviderId = providerId;
                await _userManager.UpdateAsync(user);
            }
        }

        if (user == null)
        {
            user = new AdminUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                ExternalProvider = provider,
                ExternalProviderId = providerId,
                CreatedAt = DateTime.UtcNow,
            };
            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            await _userManager.AddToRoleAsync(user, "Operator");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = GenerateAccessToken(user, roles);
        var rawRefreshToken = await CreateRefreshTokenAsync(user.Id);
        await _db.SaveChangesAsync();

        return new AuthResponse(accessToken, rawRefreshToken, await ToDtoAsync(user, roles));
    }

    public async Task<AuthResponse> GoogleLoginAsync(string idToken)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Google:ClientId"] }
            });
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedAccessException("Invalid Google token.");
        }

        var user = await _userManager.FindByEmailAsync(payload.Email)
            ?? throw new UnauthorizedAccessException("No account found for this Google address. Contact your administrator.");

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = GenerateAccessToken(user, roles);
        var rawRefreshToken = await CreateRefreshTokenAsync(user.Id);
        await _db.SaveChangesAsync();

        return new AuthResponse(accessToken, rawRefreshToken, await ToDtoAsync(user, roles));
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

        return new AuthResponse(accessToken, rawRefreshToken, await ToDtoAsync(storedToken.User, roles));
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
        return await ToDtoAsync(user, roles);
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

    public async Task<AuthResponse> SetupBusinessAsync(string userId, string name, string? description)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.BusinessId.HasValue)
            throw new InvalidOperationException("User already has a business.");

        var slug = GenerateSlug(name);
        if (await _db.Businesses.AnyAsync(b => b.Slug == slug))
            slug = $"{slug}-{Guid.NewGuid().ToString()[..6]}";

        var business = new Business
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Businesses.Add(business);

        user.BusinessId = business.Id;
        await _userManager.UpdateAsync(user);
        await _db.SaveChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = GenerateAccessToken(user, roles);
        var rawRefreshToken = await CreateRefreshTokenAsync(user.Id);
        await _db.SaveChangesAsync();

        return new AuthResponse(accessToken, rawRefreshToken, await ToDtoAsync(user, roles));
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant().Replace(" ", "-");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
        return slug.Trim('-');
    }

    public static string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private async Task<AdminUserDto> ToDtoAsync(AdminUser user, IList<string> roles)
    {
        var businessName = user.BusinessId.HasValue
            ? await _db.Businesses
                .Where(b => b.Id == user.BusinessId.Value)
                .Select(b => b.Name)
                .FirstOrDefaultAsync()
            : null;

        return new AdminUserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            roles.FirstOrDefault() ?? "Operator",
            user.BusinessId,
            businessName);
    }
}
