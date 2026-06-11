using MeBio.Models;
using MeBio.Services;
using Microsoft.EntityFrameworkCore;

namespace MeBio.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, PasswordHasher hasher)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.Users.AnyAsync())
            return;

        var (hash, salt) = hasher.Hash("admin123");
        db.Users.Add(new User
        {
            FirstName = "Admin",
            LastName = "System",
            Email = "admin@mebio.pl",
            PasswordHash = hash,
            PasswordSalt = salt,
            Age = 30,
            Gender = Gender.Other,
            Role = UserRole.Admin,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
