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
    public DbSet<EmailTemplate> EmailTemplates { get; set; }
    public DbSet<AiGenerationRun> AiGenerationRuns { get; set; }
    public DbSet<AiGeneratedTemplateVersion> AiGeneratedTemplateVersions { get; set; }
    public DbSet<AiTemplateProfile> AiTemplateProfiles { get; set; }

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
            b.Property(e => e.AiPrompt).HasMaxLength(EmailOperation.MaxAiPromptLength);
            b.Property(e => e.AiTone).HasMaxLength(EmailOperation.MaxAiToneLength);
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

            b.HasOne<EmailTemplate>()
             .WithMany()
             .HasForeignKey(e => e.TemplateId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EmailSender>(b =>
        {
            b.ToTable("EmailSenders");
            b.HasIndex(e => e.Email).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<EmailTemplate>(b =>
        {
            b.ToTable("EmailTemplates");
            b.HasIndex(e => e.OperationId);
            b.HasOne<EmailOperation>()
             .WithMany()
             .HasForeignKey(e => e.OperationId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);  // null = shared global template

            b.HasOne<AiGenerationRun>()
             .WithMany()
             .HasForeignKey(e => e.AiGenerationRunId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.NoAction);

            b.HasOne<AiGeneratedTemplateVersion>()
             .WithMany()
             .HasForeignKey(e => e.AiGeneratedVersionId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(e => e.AiGeneratedVersionId);
        });

        modelBuilder.Entity<AiGenerationRun>(b =>
        {
            b.ToTable("AiGenerationRuns");
            b.HasIndex(e => new { e.OperationId, e.CreationTime });
            b.HasOne<EmailOperation>()
             .WithMany()
             .HasForeignKey(e => e.OperationId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AiGeneratedTemplateVersion>(b =>
        {
            b.ToTable("AiGeneratedTemplateVersions");
            b.HasIndex(e => new { e.OperationId, e.CreationTime });
            b.HasIndex(e => e.SubjectHash);
            b.HasIndex(e => e.BodyHash);
            b.HasIndex(e => e.StructureHash);

            b.HasOne<EmailOperation>()
             .WithMany()
             .HasForeignKey(e => e.OperationId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne<AiGenerationRun>()
             .WithMany()
             .HasForeignKey(e => e.GenerationRunId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiTemplateProfile>(b =>
        {
            b.ToTable("AiTemplateProfiles");
            b.HasIndex(e => e.ProfileKey).IsUnique();
            b.HasIndex(e => e.SourceFingerprint);
        });
    }
}
