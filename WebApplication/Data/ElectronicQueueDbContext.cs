using Microsoft.EntityFrameworkCore;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Data;

/// <summary>
/// Только чтение из БД ElectronicQueueProf. Аналитика: прибытие = DateArrival + TimeArrival;
/// завершение талона по умолчанию = DateArrival + TimeComplete (если задано).
/// </summary>
public sealed class ElectronicQueueDbContext : DbContext
{
    private const string ReadOnlyMessage =
        "ElectronicQueueDbContext предназначен только для чтения; запись во внешнюю БД запрещена.";

    public ElectronicQueueDbContext(DbContextOptions<ElectronicQueueDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<EqCabinet> Cabinets => Set<EqCabinet>();
    public DbSet<EqDoctor> Doctors => Set<EqDoctor>();
    public DbSet<EqCategory> Categories => Set<EqCategory>();
    public DbSet<EqSpecialty> Specialties => Set<EqSpecialty>();
    public DbSet<EqAppointment> Appointments => Set<EqAppointment>();
    public DbSet<EqListItem> ListItems => Set<EqListItem>();
    public DbSet<EqLogWork> LogWorks => Set<EqLogWork>();
    public DbSet<EqStatusItemList> StatusItemLists => Set<EqStatusItemList>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new InvalidOperationException(ReadOnlyMessage);

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(ReadOnlyMessage);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<EqCabinet>(entity =>
        {
            entity.ToTable("Cabinet");
            entity.HasKey(x => x.IdCabinet);
            entity.Property(x => x.IdCabinet).HasColumnName("id_cabinet");
            entity.Property(x => x.CabinetNumber).HasColumnName("cabinet_number").HasMaxLength(64);
        });

        builder.Entity<EqDoctor>(entity =>
        {
            entity.ToTable("Doctor");
            entity.HasKey(x => x.IdDoctor);
            entity.Property(x => x.IdDoctor).HasColumnName("id_doctor");
            entity.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(500);
        });

        builder.Entity<EqCategory>(entity =>
        {
            entity.ToTable("Category");
            entity.HasKey(x => x.IdCategory);
            entity.Property(x => x.IdCategory).HasColumnName("id_category");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(500);
            entity.Property(x => x.Priority).HasColumnName("priority");
        });

        builder.Entity<EqSpecialty>(entity =>
        {
            entity.ToTable("Specialty");
            entity.HasKey(x => x.IdSpecialty);
            entity.Property(x => x.IdSpecialty).HasColumnName("id_specialty");
            entity.Property(x => x.Definition).HasColumnName("definition").HasMaxLength(500);
            entity.Property(x => x.TimeServicing).HasColumnName("time_servicing");
        });

        builder.Entity<EqAppointment>(entity =>
        {
            entity.ToTable("Appointment");
            entity.HasKey(x => x.IdAppointment);
            entity.Property(x => x.IdAppointment).HasColumnName("id_appointment");
            entity.Property(x => x.IdCategory).HasColumnName("id_category");
            entity.Property(x => x.DateArrival).HasColumnName("date_arrival");
            entity.Property(x => x.TimeArrival).HasColumnName("time_arrival");
            entity.Property(x => x.TimeStartPause).HasColumnName("time_start_pause");
            entity.Property(x => x.Priority).HasColumnName("priority");
            entity.Property(x => x.Info).HasColumnName("info").HasMaxLength(1000);
            entity.Property(x => x.IdClient).HasColumnName("id_client");
            entity.Property(x => x.TimeComplete).HasColumnName("time_complete");

            entity.HasOne(x => x.Category)
                .WithMany(x => x.Appointments)
                .HasForeignKey(x => x.IdCategory)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EqStatusItemList>(entity =>
        {
            entity.ToTable("Status_item_list");
            entity.HasKey(x => x.IdStatusItem);
            entity.Property(x => x.IdStatusItem).HasColumnName("id_status_item");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        });

        builder.Entity<EqListItem>(entity =>
        {
            entity.ToTable("List_item");
            entity.HasKey(x => x.IdListItem);
            entity.Property(x => x.IdListItem).HasColumnName("id_list_item");
            entity.Property(x => x.IdAppointment).HasColumnName("id_appointment");
            entity.Property(x => x.IdSpecialty).HasColumnName("id_specialty");
            entity.Property(x => x.TimeStartServicing).HasColumnName("time_start_servicing");
            entity.Property(x => x.TimeEndServicing).HasColumnName("time_end_servicing");
            entity.Property(x => x.IdStatusItem).HasColumnName("id_status_item");
            entity.Property(x => x.IdCabinet).HasColumnName("id_cabinet");
            entity.Property(x => x.TimeCall).HasColumnName("time_call");
            entity.Property(x => x.ServiceTime).HasColumnName("service_time");
            entity.Property(x => x.IdDoctor).HasColumnName("id_doctor");

            entity.HasOne(x => x.Appointment)
                .WithMany(x => x.ListItems)
                .HasForeignKey(x => x.IdAppointment)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Specialty)
                .WithMany(x => x.ListItems)
                .HasForeignKey(x => x.IdSpecialty)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.StatusItem)
                .WithMany(x => x.ListItems)
                .HasForeignKey(x => x.IdStatusItem)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Cabinet)
                .WithMany(x => x.ListItems)
                .HasForeignKey(x => x.IdCabinet)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Doctor)
                .WithMany(x => x.ListItems)
                .HasForeignKey(x => x.IdDoctor)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EqLogWork>(entity =>
        {
            entity.ToTable("Log_work");
            entity.HasKey(x => x.IdLogWork);
            entity.Property(x => x.IdLogWork).HasColumnName("id_log_work");
            entity.Property(x => x.IdCabinet).HasColumnName("id_cabinet");
            entity.Property(x => x.IdDoctor).HasColumnName("id_doctor");
            entity.Property(x => x.DateWork).HasColumnName("date_work");
            entity.Property(x => x.TimeBegin).HasColumnName("time_begin");
            entity.Property(x => x.TimeEnd).HasColumnName("time_end");
            entity.Property(x => x.LastRefresh).HasColumnName("last_refresh");

            entity.HasOne(x => x.Cabinet)
                .WithMany(x => x.LogWorks)
                .HasForeignKey(x => x.IdCabinet)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Doctor)
                .WithMany(x => x.LogWorks)
                .HasForeignKey(x => x.IdDoctor)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
