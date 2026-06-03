using Microsoft.EntityFrameworkCore;
using WebApplication.Models.ElectronicQueueProf;
using WebApplication.Services.Dashboard;
using Xunit;

namespace WebApplication.Tests.Dashboard;

public sealed class QueueDashboardDoctorsOnShiftQueryTests
{
    private static readonly DateOnly Today = new(2026, 6, 2);

    [Fact]
    public async Task CountAsync_returns_zero_when_no_open_shifts()
    {
        await using var db = CreateDb();
        db.LogWorks.Add(new EqLogWork
        {
            IdLogWork = 1,
            IdDoctor = 10,
            IdCabinet = 1,
            DateWork = Today,
            TimeBegin = new TimeOnly(8, 0),
            TimeEnd = new TimeOnly(17, 0)
        });
        await db.SaveChangesAsync();

        var count = await QueueDashboardDoctorsOnShiftQuery.CountAsync(db.LogWorks, Today);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountAsync_counts_distinct_doctors_with_open_shifts()
    {
        await using var db = CreateDb();
        db.LogWorks.AddRange(
            new EqLogWork
            {
                IdLogWork = 1,
                IdDoctor = 10,
                IdCabinet = 1,
                DateWork = Today,
                TimeBegin = new TimeOnly(8, 0)
            },
            new EqLogWork
            {
                IdLogWork = 2,
                IdDoctor = 10,
                IdCabinet = 1,
                DateWork = Today,
                TimeBegin = new TimeOnly(8, 0)
            },
            new EqLogWork
            {
                IdLogWork = 3,
                IdDoctor = 20,
                IdCabinet = 2,
                DateWork = Today,
                TimeBegin = new TimeOnly(9, 0)
            });
        await db.SaveChangesAsync();

        var count = await QueueDashboardDoctorsOnShiftQuery.CountAsync(db.LogWorks, Today);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CountAsync_ignores_other_days_and_invalid_doctors()
    {
        await using var db = CreateDb();
        db.LogWorks.AddRange(
            new EqLogWork
            {
                IdLogWork = 1,
                IdDoctor = 10,
                IdCabinet = 1,
                DateWork = Today.AddDays(-1),
                TimeBegin = new TimeOnly(8, 0)
            },
            new EqLogWork
            {
                IdLogWork = 2,
                IdDoctor = 0,
                IdCabinet = 1,
                DateWork = Today,
                TimeBegin = new TimeOnly(8, 0)
            },
            new EqLogWork
            {
                IdLogWork = 3,
                IdDoctor = 30,
                IdCabinet = 3,
                DateWork = Today,
                TimeBegin = new TimeOnly(8, 0),
                TimeEnd = new TimeOnly(12, 0)
            });
        await db.SaveChangesAsync();

        var count = await QueueDashboardDoctorsOnShiftQuery.CountAsync(db.LogWorks, Today);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountTotalDoctorsAsync_counts_doctors_with_positive_id()
    {
        await using var db = CreateDb();
        db.Doctors.AddRange(
            new EqDoctor { IdDoctor = 1, FullName = "Doctor A" },
            new EqDoctor { IdDoctor = 2, FullName = "Doctor B" },
            new EqDoctor { IdDoctor = 3, FullName = "Doctor C" });
        await db.SaveChangesAsync();

        var count = await QueueDashboardDoctorsOnShiftQuery.CountTotalDoctorsAsync(db.Doctors);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task LoadOpenShiftsByDoctorAsync_deduplicates_doctor_and_picks_latest_id()
    {
        await using var db = CreateDb();
        db.Cabinets.Add(new EqCabinet { IdCabinet = 1, CabinetNumber = "101" });
        db.LogWorks.AddRange(
            new EqLogWork
            {
                IdLogWork = 1,
                IdDoctor = 10,
                IdCabinet = 1,
                DateWork = Today,
                TimeBegin = new TimeOnly(8, 0)
            },
            new EqLogWork
            {
                IdLogWork = 2,
                IdDoctor = 10,
                IdCabinet = 1,
                DateWork = Today,
                TimeBegin = new TimeOnly(8, 0)
            },
            new EqLogWork
            {
                IdLogWork = 3,
                IdDoctor = 20,
                IdCabinet = 1,
                DateWork = Today,
                TimeBegin = new TimeOnly(9, 0)
            });
        await db.SaveChangesAsync();

        var shifts = await QueueDashboardDoctorsOnShiftQuery.LoadOpenShiftsByDoctorAsync(db.LogWorks, Today);

        Assert.Equal(2, shifts.Count);
        Assert.Equal("101", shifts[10].CabinetNumber);
        Assert.Equal("101", shifts[20].CabinetNumber);
    }

    [Fact]
    public async Task LoadOpenShiftsByDoctorAsync_returns_empty_for_closed_or_other_day()
    {
        await using var db = CreateDb();
        db.Cabinets.Add(new EqCabinet { IdCabinet = 1, CabinetNumber = "101" });
        db.LogWorks.AddRange(
            new EqLogWork
            {
                IdLogWork = 1,
                IdDoctor = 10,
                IdCabinet = 1,
                DateWork = Today.AddDays(-1),
                TimeBegin = new TimeOnly(8, 0)
            },
            new EqLogWork
            {
                IdLogWork = 2,
                IdDoctor = 20,
                IdCabinet = 1,
                DateWork = Today,
                TimeBegin = new TimeOnly(8, 0),
                TimeEnd = new TimeOnly(17, 0)
            });
        await db.SaveChangesAsync();

        var shifts = await QueueDashboardDoctorsOnShiftQuery.LoadOpenShiftsByDoctorAsync(db.LogWorks, Today);

        Assert.Empty(shifts);
    }

    private static LogWorkTestDb CreateDb()
    {
        var options = new DbContextOptionsBuilder<LogWorkTestDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LogWorkTestDb(options);
    }

    private sealed class LogWorkTestDb : DbContext
    {
        public LogWorkTestDb(DbContextOptions<LogWorkTestDb> options) : base(options) { }

        public DbSet<EqLogWork> LogWorks => Set<EqLogWork>();

        public DbSet<EqDoctor> Doctors => Set<EqDoctor>();

        public DbSet<EqCabinet> Cabinets => Set<EqCabinet>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EqCabinet>(entity =>
            {
                entity.HasKey(x => x.IdCabinet);
                entity.Ignore(x => x.ListItems);
                entity.Ignore(x => x.LogWorks);
            });

            modelBuilder.Entity<EqLogWork>(entity =>
            {
                entity.HasKey(x => x.IdLogWork);
                entity.HasOne(x => x.Cabinet)
                    .WithMany(x => x.LogWorks)
                    .HasForeignKey(x => x.IdCabinet);
                entity.Ignore(x => x.Doctor);
            });

            modelBuilder.Entity<EqDoctor>(entity =>
            {
                entity.HasKey(x => x.IdDoctor);
                entity.Ignore(x => x.ListItems);
                entity.Ignore(x => x.LogWorks);
            });
        }
    }
}
