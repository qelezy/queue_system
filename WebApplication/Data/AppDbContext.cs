using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplication.Models;

namespace WebApplication.Data
{
    public class AppDbContext : IdentityDbContext<User, IdentityRole, string>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<User>().ToTable("user");
            builder.Entity<IdentityRole>().ToTable("roles");

            builder.Entity<User>(entity =>
            {
                entity.Property(x => x.FirstName)
                    .HasColumnName("first_name")
                    .HasMaxLength(100);

                entity.Property(x => x.LastName)
                    .HasColumnName("last_name")
                    .HasMaxLength(100);

                entity.Property(x => x.Patronymic)
                    .HasColumnName("patronymic")
                    .HasMaxLength(100);

                entity.Property(x => x.RefreshToken)
                    .HasColumnName("refresh_token")
                    .HasMaxLength(512);

                entity.Property(x => x.RefreshTokenExpiresAt)
                    .HasColumnName("refresh_token_expires_at");

                entity.Property(x => x.RefreshSessionExtended)
                    .HasColumnName("refresh_session_extended");
            });
        }
    }
}
