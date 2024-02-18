using DevsTutorialCenterAPI.Data.Entities;
using DevsTutorialCenterAPI.Data.Seed;
using DevsTutorialCenterAPI.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevsTutorialCenterAPI.Data;

public static class Seeder
{
    public static async Task Seed(IApplicationBuilder app)
    {
        var context = app.ApplicationServices.CreateScope().ServiceProvider
            .GetRequiredService<DevsTutorialCenterAPIContext>();
        if ((await context.Database.GetPendingMigrationsAsync()).Any())
            await context.Database.MigrateAsync();

        if (!context.Roles.Any())
        {
            var roles = new List<IdentityRole>
            {
                new() {Name = UserRolesConstants.Decadev, NormalizedName = UserRolesConstants.Decadev},
                new() {Name = UserRolesConstants.Editor, NormalizedName = UserRolesConstants.Editor},
                new() {Name = UserRolesConstants.Admin, NormalizedName = UserRolesConstants.Admin},
            };
            await context.Roles.AddRangeAsync(roles);
        }
        
        if (!context.AppUsers.Any())
        {
            var seeder = new SeedData(context);
            await seeder.Run();
        }
    }
}