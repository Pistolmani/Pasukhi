using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.DTOs.AdminUsers;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;

namespace Pasukhi.API.Controllers;

[ApiController]
[Route("api/admin-users")]
[Authorize(Roles = "SuperAdmin")]
public class AdminUsersController : ControllerBase
{
    private const string OperatorRole = "Operator";

    private readonly UserManager<AdminUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly PasukhiDbContext _db;

    public AdminUsersController(
        UserManager<AdminUser> userManager,
        RoleManager<IdentityRole> roleManager,
        PasukhiDbContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<ManagedAdminUserDto>>> GetAll(
        [FromQuery] Guid? businessId,
        CancellationToken cancellationToken)
    {
        var query = _db.Users.AsNoTracking();

        if (businessId.HasValue)
        {
            query = query.Where(u => u.BusinessId == businessId.Value);
        }

        var users = await query
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);

        var result = new List<ManagedAdminUserDto>();
        foreach (var user in users)
        {
            result.Add(await ToDtoAsync(user, cancellationToken));
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ManagedAdminUserDto>> Create(
        [FromBody] CreateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        var business = await _db.Businesses
            .FirstOrDefaultAsync(b => b.Id == request.BusinessId, cancellationToken)
            ?? throw new KeyNotFoundException($"Business {request.BusinessId} not found.");

        if (!await _roleManager.RoleExistsAsync(OperatorRole))
        {
            await _roleManager.CreateAsync(new IdentityRole(OperatorRole));
        }

        var user = new AdminUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BusinessId = business.Id,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", createResult.Errors.Select(e => e.Description)));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, OperatorRole);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", roleResult.Errors.Select(e => e.Description)));
        }

        var dto = new ManagedAdminUserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            OperatorRole,
            user.BusinessId,
            business.Name,
            user.CreatedAt);

        return CreatedAtAction(nameof(GetAll), new { businessId = business.Id }, dto);
    }

    private async Task<ManagedAdminUserDto> ToDtoAsync(AdminUser user, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var businessName = user.BusinessId.HasValue
            ? await _db.Businesses
                .Where(b => b.Id == user.BusinessId.Value)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new ManagedAdminUserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            roles.FirstOrDefault() ?? OperatorRole,
            user.BusinessId,
            businessName,
            user.CreatedAt);
    }
}
