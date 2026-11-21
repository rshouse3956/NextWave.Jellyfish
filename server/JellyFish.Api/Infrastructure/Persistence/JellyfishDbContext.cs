using JellyFish.Api.Domain;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace JellyFish.Api.Infrastructure.Persistence;

public class JellyfishDbContext : DbContext
{
    public JellyfishDbContext(DbContextOptions<JellyfishDbContext> options, IOptions<MongoSettings> mongoOptions) : base(options)
    {
        if (mongoOptions.Value.DisableTransactions)
        {
            Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        }
    }

    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Pattern> Patterns => Set<Pattern>();
    public DbSet<PatternDetail> PatternDetails => Set<PatternDetail>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ActiveZone> ActiveZones => Set<ActiveZone>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Folder>().ToCollection("folder");
        modelBuilder.Entity<Folder>().HasKey(f => f.Id);

        modelBuilder.Entity<Pattern>().ToCollection("pattern");
        modelBuilder.Entity<Pattern>().HasKey(p => p.Id);

        modelBuilder.Entity<PatternDetail>().ToCollection("patternDetail");
        modelBuilder.Entity<PatternDetail>().HasKey(p => p.Id);

        modelBuilder.Entity<Holiday>().ToCollection("holiday");
        modelBuilder.Entity<Holiday>().HasKey(h => h.Id);

        modelBuilder.Entity<Zone>().ToCollection("zone");
        modelBuilder.Entity<Zone>().HasKey(z => z.Id);

        modelBuilder.Entity<Schedule>().ToCollection("schedule");
        modelBuilder.Entity<Schedule>().HasKey(s => s.Id);

        modelBuilder.Entity<ActiveZone>().ToCollection("activeZone");
        modelBuilder.Entity<ActiveZone>().HasKey(a => a.Id);

        modelBuilder.Entity<AuditLog>().ToCollection("auditLog");
        modelBuilder.Entity<AuditLog>().HasKey(a => a.Id);

        base.OnModelCreating(modelBuilder);
    }
}

