using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplication.Models;

namespace WebApplication.Data
{
    public class AppDbContext : IdentityDbContext<User, IdentityRole, string>
    {
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<User>().ToTable("user");
            builder.Entity<IdentityRole>().ToTable("roles");

            builder.Entity<Permission>(entity =>
            {
                entity.ToTable("permission");
                entity.HasKey(x => x.PermissionId);
                entity.Property(x => x.PermissionId).HasColumnName("permission_id");
                entity.Property(x => x.PermissionName).HasColumnName("permission_name").HasMaxLength(256);
                entity.HasIndex(x => x.PermissionName).IsUnique();
            });

            builder.Entity<RolePermission>(entity =>
            {
                entity.ToTable("role_permission");
                entity.HasKey(x => new { x.RoleId, x.PermissionId });
                entity.Property(x => x.RoleId).HasColumnName("role_id").HasMaxLength(450);
                entity.Property(x => x.PermissionId).HasColumnName("permission_id");

                entity.HasOne(x => x.Permission)
                    .WithMany(p => p.RolePermissions)
                    .HasForeignKey(x => x.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<IdentityRole>()
                    .WithMany()
                    .HasForeignKey(x => x.RoleId)
                    .HasPrincipalKey(r => r.Id)
                    .OnDelete(DeleteBehavior.Cascade);
            });

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
