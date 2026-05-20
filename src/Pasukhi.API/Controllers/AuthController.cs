using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Auth;
using Pasukhi.Application.Interfaces;
using Pasukhi.Infrastructure.Services;
using System.ComponentModel.DataAnnotations;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IConfiguration _config;

    public AuthController(IAuthService auth, IConfiguration config)
    {
        _auth = auth;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        SetRefreshTokenCookie(result.RawRefreshToken);
        return Ok(new { accessToken = result.AccessToken, user = result.User });
    }

    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] GoogleAuthRequest request)
    {
        var result = await _auth.GoogleLoginAsync(request.IdToken);
        SetRefreshTokenCookie(result.RawRefreshToken);
        return Ok(new { accessToken = result.AccessToken, user = result.User });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var token = Request.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized();
        }

        var result = await _auth.RefreshTokenAsync(AuthService.HashRefreshToken(token));
        SetRefreshTokenCookie(result.RawRefreshToken);
        return Ok(new { accessToken = result.AccessToken, user = result.User });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
        {
            await _auth.LogoutAsync(userId);
        }

        Response.Cookies.Delete("refresh_token");
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
        return Ok(await _auth.GetCurrentUserAsync(userId));
    }

    private void SetRefreshTokenCookie(string token)
    {
        var days = int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "7");
        Response.Cookies.Append("refresh_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(days),
            Path = "/api/auth"
        });
    }
}
