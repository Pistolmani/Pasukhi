# Codex Task — Phase 1: Auth + Business CRUD

> Read `AGENTS.md` first. Phase 0 must be complete before starting this.

## Goal

By the end of this phase:
- Operators can log in and get a JWT access token
- Refresh tokens are issued in an HttpOnly cookie
- The frontend shows a working login page
- SuperAdmin can create and list businesses via the API
- Tenant context is resolved from JWT claims on every request

---

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 — Auth DTOs and Validators

### `src/Pasukhi.Application/DTOs/Auth/LoginRequest.cs`
(already created in Phase 0 — skip if exists)

### `src/Pasukhi.Application/Validators/LoginRequestValidator.cs`

```csharp
using FluentValidation;
using Pasukhi.Application.DTOs.Auth;

namespace Pasukhi.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
```

---

## Step 2 — Auth Service Implementation

### `src/Pasukhi.Infrastructure/Services/AuthService.cs`

```csharp
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
            throw new UnauthorizedAccessException("Invalid email or password.");

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = GenerateAccessToken(user, roles);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);

        return new AuthResponse(accessToken, ToDto(user, roles));
    }

    public async Task<AuthResponse> RefreshTokenAsync(string tokenHash)
    {
        var storedToken = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == tokenHash && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);

        if (storedToken == null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        storedToken.IsRevoked = true;
        var newRefreshToken = await CreateRefreshTokenAsync(storedToken.UserId);

        var roles = await _userManager.GetRolesAsync(storedToken.User);
        var accessToken = GenerateAccessToken(storedToken.User, roles);

        await _db.SaveChangesAsync();

        return new AuthResponse(accessToken, ToDto(storedToken.User, roles));
    }

    public async Task LogoutAsync(string userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();

        foreach (var t in tokens) t.IsRevoked = true;
        await _db.SaveChangesAsync();
    }

    public async Task<AdminUserDto> GetCurrentUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");
        var roles = await _userManager.GetRolesAsync(user);
        return ToDto(user, roles);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
            claims.Add(new Claim("BusinessId", user.BusinessId.Value.ToString()));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(
            int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "15"));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> CreateRefreshTokenAsync(string userId)
    {
        // Store SHA256 hash in DB; return raw token to caller (set in cookie)
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        _db.RefreshTokens.Add(new RefreshToken
        {
            Token = hash,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(
                int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "7")),
            CreatedAt = DateTime.UtcNow
        });

        return rawToken;
    }

    private static AdminUserDto ToDto(AdminUser user, IList<string> roles) =>
        new(user.Id, user.Email!, user.FirstName, user.LastName,
            roles.FirstOrDefault() ?? "Operator", user.BusinessId);
}
```

---

## Step 3 — Auth Controller

### `src/Pasukhi.API/Controllers/AuthController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Auth;
using Pasukhi.Application.Interfaces;

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
        SetRefreshTokenCookie(result.AccessToken); // NOTE: see helper below — set the raw token, not access token
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var token = Request.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(token)) return Unauthorized();

        var result = await _auth.RefreshTokenAsync(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(token))));

        SetRefreshTokenCookie(token); // will be replaced by new one — actually set result's new raw token
        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId != null) await _auth.LogoutAsync(userId);
        Response.Cookies.Delete("refresh_token");
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
        var user = await _auth.GetCurrentUserAsync(userId);
        return Ok(user);
    }

    private void SetRefreshTokenCookie(string token)
    {
        var days = int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "7");
        Response.Cookies.Append("refresh_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(days),
            Path = "/api/auth"
        });
    }
}
```

**NOTE:** The login flow must return the raw refresh token in the cookie. Fix the `Login` endpoint to store the raw token in the cookie correctly — the `AuthService.LoginAsync` must return both the access token AND the raw refresh token. Update `AuthResponse` to include the raw refresh token (not returned in JSON body, only for the cookie):

```csharp
// Update AuthService.LoginAsync to:
var rawRefreshToken = await CreateRefreshTokenAsync(user.Id);
await _db.SaveChangesAsync();
return new AuthResponse(accessToken, rawRefreshToken, ToDto(user, roles));

// Update AuthResponse:
public record AuthResponse(string AccessToken, string RawRefreshToken, AdminUserDto User);

// In AuthController.Login:
SetRefreshTokenCookie(result.RawRefreshToken);
return Ok(new { accessToken = result.AccessToken, user = result.User });
```

---

## Step 4 — Seed SuperAdmin

### `src/Pasukhi.Infrastructure/Data/DbSeeder.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using Pasukhi.Domain.Entities;

namespace Pasukhi.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Roles
        foreach (var role in new[] { "SuperAdmin", "Operator" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // Default SuperAdmin
        const string adminEmail = "admin@pasukhi.ge";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new AdminUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Super",
                LastName = "Admin",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
            await userManager.CreateAsync(admin, "Admin@123!");
            await userManager.AddToRoleAsync(admin, "SuperAdmin");
        }
    }
}
```

Call it in `Program.cs` before `app.Run()`:

```csharp
// After app.Build() and before app.Run():
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}
```

---

## Step 5 — Business CRUD

### `src/Pasukhi.Application/DTOs/Businesses/BusinessDto.cs`

```csharp
namespace Pasukhi.Application.DTOs.Businesses;

public record BusinessDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? LogoUrl,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateBusinessRequest(
    string Name,
    string Slug,
    string? Description,
    string? LogoUrl
);

public record UpdateBusinessRequest(
    string Name,
    string? Description,
    string? LogoUrl,
    bool IsActive
);
```

### `src/Pasukhi.Application/Validators/CreateBusinessRequestValidator.cs`

```csharp
using FluentValidation;
using Pasukhi.Application.DTOs.Businesses;

namespace Pasukhi.Application.Validators;

public class CreateBusinessRequestValidator : AbstractValidator<CreateBusinessRequest>
{
    public CreateBusinessRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug must be lowercase letters, numbers, and hyphens only.");
    }
}
```

### `src/Pasukhi.Application/Interfaces/IBusinessService.cs`

```csharp
using Pasukhi.Application.DTOs.Businesses;

namespace Pasukhi.Application.Interfaces;

public interface IBusinessService
{
    Task<List<BusinessDto>> GetAllAsync();
    Task<BusinessDto?> GetByIdAsync(Guid id);
    Task<BusinessDto> CreateAsync(CreateBusinessRequest request);
    Task<BusinessDto> UpdateAsync(Guid id, UpdateBusinessRequest request);
    Task DeleteAsync(Guid id);
}
```

### `src/Pasukhi.Infrastructure/Services/BusinessService.cs`

```csharp
using Mapster;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.Businesses;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.Infrastructure.Services;

public class BusinessService : IBusinessService
{
    private readonly PasukhiDbContext _db;

    public BusinessService(PasukhiDbContext db) => _db = db;

    public async Task<List<BusinessDto>> GetAllAsync() =>
        await _db.Businesses
            .OrderBy(b => b.Name)
            .ProjectToType<BusinessDto>()
            .ToListAsync();

    public async Task<BusinessDto?> GetByIdAsync(Guid id) =>
        await _db.Businesses
            .Where(b => b.Id == id)
            .ProjectToType<BusinessDto>()
            .FirstOrDefaultAsync();

    public async Task<BusinessDto> CreateAsync(CreateBusinessRequest request)
    {
        if (await _db.Businesses.AnyAsync(b => b.Slug == request.Slug))
            throw new InvalidOperationException($"Slug '{request.Slug}' is already taken.");

        var business = new Business
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            Description = request.Description,
            LogoUrl = request.LogoUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Businesses.Add(business);
        await _db.SaveChangesAsync();
        return business.Adapt<BusinessDto>();
    }

    public async Task<BusinessDto> UpdateAsync(Guid id, UpdateBusinessRequest request)
    {
        var business = await _db.Businesses.FindAsync(id)
            ?? throw new KeyNotFoundException($"Business {id} not found.");

        business.Name = request.Name;
        business.Description = request.Description;
        business.LogoUrl = request.LogoUrl;
        business.IsActive = request.IsActive;
        business.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return business.Adapt<BusinessDto>();
    }

    public async Task DeleteAsync(Guid id)
    {
        var business = await _db.Businesses.FindAsync(id)
            ?? throw new KeyNotFoundException($"Business {id} not found.");
        _db.Businesses.Remove(business);
        await _db.SaveChangesAsync();
    }
}
```

### `src/Pasukhi.API/Controllers/BusinessesController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pasukhi.Application.DTOs.Businesses;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/businesses")]
[Authorize(Roles = "SuperAdmin")]
public class BusinessesController : ControllerBase
{
    private readonly IBusinessService _businesses;

    public BusinessesController(IBusinessService businesses) => _businesses = businesses;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _businesses.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _businesses.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBusinessRequest request)
    {
        var result = await _businesses.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBusinessRequest request)
    {
        var result = await _businesses.UpdateAsync(id, request);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _businesses.DeleteAsync(id);
        return NoContent();
    }
}
```

---

## Step 6 — Register Services in Program.cs

Add these lines to `Program.cs` before `app.Build()`:

```csharp
// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
```

Also add FluentValidation auto-registration:

```csharp
// Add to Program.cs after AddControllers()
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
```

And register the NuGet package in `Pasukhi.API.csproj`:

```bash
dotnet add src/Pasukhi.API/Pasukhi.API.csproj package FluentValidation.DependencyInjectionExtensions
```

---

## Step 7 — Global Exception Middleware

### `src/Pasukhi.API/Middleware/ExceptionHandlingMiddleware.cs`

```csharp
using System.Text.Json;

namespace Pasukhi.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access");
            await WriteErrorAsync(context, 401, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await WriteErrorAsync(context, 404, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await WriteErrorAsync(context, 400, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, 500, "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(body);
    }
}
```

Register in `Program.cs` before `app.UseCors()`:

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();
```

---

## Step 8 — Frontend Login

### `pasukhi-admin/src/stores/auth-store.ts`

```typescript
import { create } from 'zustand'

interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  role: string
  businessId: string | null
}

interface AuthState {
  user: User | null
  accessToken: string | null
  setAuth: (user: User, token: string) => void
  clearAuth: () => void
  isAuthenticated: () => boolean
  isSuperAdmin: () => boolean
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  accessToken: null,
  setAuth: (user, accessToken) => set({ user, accessToken }),
  clearAuth: () => set({ user: null, accessToken: null }),
  isAuthenticated: () => get().accessToken !== null,
  isSuperAdmin: () => get().user?.role === 'SuperAdmin',
}))
```

### `pasukhi-admin/src/api/client.ts`

```typescript
import axios from 'axios'
import { useAuthStore } from '../stores/auth-store'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000',
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

let refreshing = false

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    if (error.response?.status === 401 && !error.config._retry && !refreshing) {
      error.config._retry = true
      refreshing = true
      try {
        const { data } = await axios.post(
          `${api.defaults.baseURL}/api/auth/refresh`,
          {},
          { withCredentials: true }
        )
        useAuthStore.getState().setAuth(data.user, data.accessToken)
        error.config.headers.Authorization = `Bearer ${data.accessToken}`
        return api(error.config)
      } catch {
        useAuthStore.getState().clearAuth()
        window.location.href = '/login'
      } finally {
        refreshing = false
      }
    }
    return Promise.reject(error)
  }
)

export default api
```

### `pasukhi-admin/src/api/auth.ts`

```typescript
import api from './client'

export interface LoginRequest { email: string; password: string }
export interface AuthResponse { accessToken: string; user: User }
export interface User {
  id: string; email: string; firstName: string; lastName: string;
  role: string; businessId: string | null
}

export const authApi = {
  login: (data: LoginRequest) =>
    api.post<AuthResponse>('/api/auth/login', data).then(r => r.data),
  logout: () =>
    api.post('/api/auth/logout'),
  me: () =>
    api.get<User>('/api/auth/me').then(r => r.data),
}
```

### `pasukhi-admin/src/schemas/auth-schemas.ts`

```typescript
import { z } from 'zod'

export const loginSchema = z.object({
  email: z.string().email('Invalid email address'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
})

export type LoginFormData = z.infer<typeof loginSchema>
```

### `pasukhi-admin/src/features/auth/login-page.tsx`

```tsx
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { authApi } from '../../api/auth'
import { useAuthStore } from '../../stores/auth-store'
import { loginSchema, type LoginFormData } from '../../schemas/auth-schemas'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'

export function LoginPage() {
  const navigate = useNavigate()
  const setAuth = useAuthStore(s => s.setAuth)

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
  })

  const onSubmit = async (data: LoginFormData) => {
    try {
      const result = await authApi.login(data)
      setAuth(result.user, result.accessToken)
      navigate('/')
    } catch {
      toast.error('Invalid email or password')
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <div className="w-full max-w-sm bg-white rounded-xl shadow-sm border p-8">
        <h1 className="text-2xl font-bold text-center mb-6">Pasukhi Admin</h1>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div>
            <Label htmlFor="email">Email</Label>
            <Input id="email" type="email" {...register('email')} />
            {errors.email && <p className="text-sm text-red-500 mt-1">{errors.email.message}</p>}
          </div>
          <div>
            <Label htmlFor="password">Password</Label>
            <Input id="password" type="password" {...register('password')} />
            {errors.password && <p className="text-sm text-red-500 mt-1">{errors.password.message}</p>}
          </div>
          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </Button>
        </form>
      </div>
    </div>
  )
}
```

### `pasukhi-admin/src/components/layout/auth-guard.tsx`

```tsx
import { Navigate, Outlet } from 'react-router-dom'
import { useAuthStore } from '../../stores/auth-store'

export function AuthGuard() {
  const isAuthenticated = useAuthStore(s => s.isAuthenticated())
  return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace />
}
```

---

## Step 9 — Update App.tsx with real routes

```tsx
import { Routes, Route, Navigate } from 'react-router-dom'
import { LoginPage } from './features/auth/login-page'
import { AuthGuard } from './components/layout/auth-guard'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<AuthGuard />}>
        <Route path="/" element={<div className="p-8 text-xl font-semibold">Dashboard coming soon</div>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
```

---

## Step 10 — Verify

```bash
# Backend
dotnet build   # 0 errors

# Test login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@pasukhi.ge","password":"Admin@123!"}'
# Expected: { "accessToken": "eyJ...", "user": { ... } }

# Test businesses (need access token)
curl http://localhost:5000/api/businesses \
  -H "Authorization: Bearer <token>"
# Expected: []  (empty array, no businesses yet)

# Frontend
cd pasukhi-admin && npx tsc --noEmit  # 0 errors
# Open http://localhost:5173
# Login with admin@pasukhi.ge / Admin@123!
# Should redirect to "/" after login
```

---

## Commit

```
feat(01-01): auth (JWT + refresh cookie) + business CRUD + login page
```

---

## What's Next

Phase 2: `docs/codex/phase-2.md` — Channel connections + FAQ + Automation rules foundation
