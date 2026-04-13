using Abp.Zero.EntityFrameworkCore;
using AutoMail.Authorization.Roles;
using AutoMail.Authorization.Users;
using AutoMail.MultiTenancy;
using AutoMail.Project_Models;
using Microsoft.EntityFrameworkCore;

namespace AutoMail.EntityFrameworkCore;

public class AutoMailDbContext : AbpZeroDbContext<Tenant, Role, User, AutoMailDbContext>
{
    public DbSet<BulkEmail> BulkEmails { get; set; }
    public DbSet<EmailSender> EmailSenders { get; set; }
    public DbSet<BulkEmailLog> BulkEmailLogs { get; set; }

    public AutoMailDbContext(DbContextOptions<AutoMailDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BulkEmail>(b =>
        {
            b.ToTable("BulkEmails");
            b.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<EmailSender>(b =>
        {
            b.ToTable("EmailSenders");
            b.HasIndex(e => e.Email).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<BulkEmailLog>(b =>
        {
            b.ToTable("BulkEmailLogs");
            b.HasIndex(e => e.BulkEmailId);
            b.HasIndex(e => new { e.SenderId, e.Status, e.SentTime });
            b.HasIndex(e => e.Status);

            b.HasOne(e => e.BulkEmail)
             .WithMany()
             .HasForeignKey(e => e.BulkEmailId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(e => e.EmailSender)
             .WithMany()
             .HasForeignKey(e => e.SenderId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
