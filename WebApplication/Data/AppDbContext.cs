using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplication.Models;

namespace MyWebApplication.Data
{
    public class AppDbContext : IdentityDbContext<User, IdentityRole, string>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

    }
    
}