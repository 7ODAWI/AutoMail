using Abp.Zero.EntityFrameworkCore;
using AutoMail.Authorization.Roles;
using AutoMail.Authorization.Users;
using AutoMail.MultiTenancy;
using AutoMail.Project_Models;
using Microsoft.EntityFrameworkCore;

namespace AutoMail.EntityFrameworkCore;

public class AutoMailDbContext : AbpZeroDbContext<Tenant, Role, User, AutoMailDbContext>
{
    public DbSet<EmailOperation> EmailOperations { get; set; }
    public DbSet<OperationEmail> OperationEmails { get; set; }
    public DbSet<EmailSender> EmailSenders { get; set; }

    public AutoMailDbContext(DbContextOptions<AutoMailDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EmailOperation>(b =>
        {
            b.ToTable("EmailOperations");
            b.Property(e => e.Body).IsRequired();
        });

        modelBuilder.Entity<OperationEmail>(b =>
        {
            b.ToTable("OperationEmails");
            b.HasIndex(e => new { e.OperationId, e.Status });

            b.HasOne(e => e.Operation)
             .WithMany()
             .HasForeignKey(e => e.OperationId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(e => e.Sender)
             .WithMany()
             .HasForeignKey(e => e.SenderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmailSender>(b =>
        {
            b.ToTable("EmailSenders");
            b.HasIndex(e => e.Email).IsUnique().HasFilter("[IsDeleted] = 0");
        });
    }
}
