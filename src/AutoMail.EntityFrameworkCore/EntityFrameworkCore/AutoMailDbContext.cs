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
    }
}
