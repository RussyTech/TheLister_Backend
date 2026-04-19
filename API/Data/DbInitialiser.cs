using API.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public static class DbInitialiser
{
    public static async Task InitDb(WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var context     = scope.ServiceProvider.GetRequiredService<StoreContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger      = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            await context.Database.MigrateAsync();
            await SeedUsers(userManager, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during database initialisation");
        }
    }

    // ─── Users ────────────────────────────────────────────────────────────────
    private static async Task SeedUsers(UserManager<ApplicationUser> userManager, ILogger logger)
    {
        if (await userManager.Users.AnyAsync()) return;

        var now = DateTime.UtcNow;

        var users = new List<(ApplicationUser user, string password, string role)>
        {
            (
                new ApplicationUser
                {
                    Id          = "11111111-0000-0000-0000-000000000001",
                    UserName    = "admin@syncpilot.com",
                    Email       = "admin@syncpilot.com",
                    DisplayName = "SyncPilot Admin",
                    CreatedAt   = now,
                    UpdatedAt   = now
                },
                "Admin1234!", "Admin"
            ),
            (
                new ApplicationUser
                {
                    Id          = "11111111-0000-0000-0000-000000000002",
                    UserName    = "business@syncpilot.com",
                    Email       = "business@syncpilot.com",
                    DisplayName = "Business User",
                    CreatedAt   = now,
                    UpdatedAt   = now
                },
                "Business1234!", "Business"
            ),
            (
                new ApplicationUser
                {
                    Id          = "11111111-0000-0000-0000-000000000003",
                    UserName    = "pro@syncpilot.com",
                    Email       = "pro@syncpilot.com",
                    DisplayName = "Pro User",
                    CreatedAt   = now,
                    UpdatedAt   = now
                },
                "Pro1234!", "Pro"
            ),
            (
                new ApplicationUser
                {
                    Id          = "11111111-0000-0000-0000-000000000004",
                    UserName    = "standard@syncpilot.com",
                    Email       = "standard@syncpilot.com",
                    DisplayName = "Standard User",
                    CreatedAt   = now,
                    UpdatedAt   = now
                },
                "Standard1234!", "Standard"
            )
        };

        foreach (var (user, password, role) in users)
        {
            var result = await userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
                logger.LogInformation("Seed user created: {Email} [{Role}]", user.Email, role);
            }
            else
            {
                logger.LogWarning("Failed to create user {Email}: {Errors}",
                    user.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}