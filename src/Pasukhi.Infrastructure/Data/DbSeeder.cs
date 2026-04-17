using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pasukhi.Domain.Entities;

namespace Pasukhi.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PasukhiDbContext>();
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[] { "SuperAdmin", "Operator" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Slug == "demo");
        if (business == null)
        {
            business = new Business
            {
                Id = Guid.NewGuid(),
                Name = "Demo Business",
                Slug = "demo",
                Description = "Local development tenant",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Businesses.Add(business);
            await db.SaveChangesAsync();
        }

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

        const string operatorEmail = "operator@pasukhi.ge";
        var operatorUser = await userManager.FindByEmailAsync(operatorEmail);
        if (operatorUser == null)
        {
            operatorUser = new AdminUser
            {
                UserName = operatorEmail,
                Email = operatorEmail,
                FirstName = "Demo",
                LastName = "Operator",
                BusinessId = business.Id,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            await userManager.CreateAsync(operatorUser, "Operator@123!");
        }
        else if (operatorUser.BusinessId != business.Id)
        {
            operatorUser.BusinessId = business.Id;
            await userManager.UpdateAsync(operatorUser);
        }

        if (!await userManager.IsInRoleAsync(operatorUser, "Operator"))
        {
            await userManager.AddToRoleAsync(operatorUser, "Operator");
        }
    }
}
