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

        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
        public DbSet<Doctor> Doctors => Set<Doctor>();
        public DbSet<Cabinet> Cabinets => Set<Cabinet>();
        public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
        public DbSet<Appointment> Appointments => Set<Appointment>();

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
            });

            builder.Entity<Patient>(entity =>
            {
                entity.ToTable("patient");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("patient_id");
                entity.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100);
                entity.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100);
                entity.Property(x => x.Patronymic).HasColumnName("patronymic").HasMaxLength(100);
            });

            builder.Entity<ServiceCategory>(entity =>
            {
                entity.ToTable("service_category");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("service_category_id");
                entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
                entity.Property(x => x.Priority).HasColumnName("priority");
            });

            builder.Entity<Doctor>(entity =>
            {
                entity.ToTable("doctor");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("doctor_id");
                entity.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100);
                entity.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100);
                entity.Property(x => x.Patronymic).HasColumnName("patronymic").HasMaxLength(100);
                entity.Property(x => x.Specialization).HasColumnName("specialization").HasMaxLength(200);
            });

            builder.Entity<Cabinet>(entity =>
            {
                entity.ToTable("cabinet");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("cabinet_id");
                entity.Property(x => x.CabinetNumber).HasColumnName("cabinet_number").HasMaxLength(32);
                entity.HasIndex(x => x.CabinetNumber).IsUnique();
            });

            builder.Entity<QueueEntry>(entity =>
            {
                entity.ToTable("queue_entry");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("queue_entry_id");
                entity.Property(x => x.PatientId).HasColumnName("patient_id");
                entity.Property(x => x.ServiceCategoryId).HasColumnName("service_category_id");
                entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
                entity.Property(x => x.QueuedAt).HasColumnName("queued_at");
                entity.Property(x => x.CalledAt).HasColumnName("called_at");

                entity.HasIndex(x => new { x.Status, x.QueuedAt });

                entity.HasOne(x => x.Patient)
                    .WithMany(x => x.QueueEntries)
                    .HasForeignKey(x => x.PatientId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.ServiceCategory)
                    .WithMany(x => x.QueueEntries)
                    .HasForeignKey(x => x.ServiceCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Appointment>(entity =>
            {
                entity.ToTable("appointment");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("appointment_id");
                entity.Property(x => x.QueueEntryId).HasColumnName("queue_entry_id");
                entity.Property(x => x.DoctorId).HasColumnName("doctor_id");
                entity.Property(x => x.CabinetId).HasColumnName("cabinet_id");
                entity.Property(x => x.StartTime).HasColumnName("start_time");
                entity.Property(x => x.EndTime).HasColumnName("end_time");

                entity.HasIndex(x => x.StartTime);
                entity.HasIndex(x => new { x.DoctorId, x.StartTime });
                entity.HasIndex(x => new { x.CabinetId, x.StartTime });

                entity.HasOne(x => x.QueueEntry)
                    .WithMany(x => x.Appointments)
                    .HasForeignKey(x => x.QueueEntryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Doctor)
                    .WithMany(d => d.Appointments)
                    .HasForeignKey(x => x.DoctorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Cabinet)
                    .WithMany(c => c.Appointments)
                    .HasForeignKey(x => x.CabinetId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
    
}