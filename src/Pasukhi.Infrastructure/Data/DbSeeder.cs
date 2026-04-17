using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Pasukhi.Domain.Entities;

namespace Pasukhi.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<AdminUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[] { "SuperAdmin", "Operator" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
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
    }
}
