using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Api.Persistence;

public sealed class ControlPlaneDbContext : DbContext
{
    public ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();

    public DbSet<OperationExecutionEntity> OperationExecutions => Set<OperationExecutionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectEntity>(entity =>
        {
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Id).HasMaxLength(120);
            entity.Property(project => project.Name).HasMaxLength(300);
            entity.Property(project => project.TagsJson).HasColumnType("TEXT");
            entity.Property(project => project.EnvironmentsJson).HasColumnType("TEXT");
        });

        modelBuilder.Entity<OperationExecutionEntity>(entity =>
        {
            entity.HasKey(execution => execution.Id);
            entity.Property(execution => execution.ProjectId).HasMaxLength(120);
            entity.Property(execution => execution.OperationId).HasMaxLength(240);
            entity.Property(execution => execution.InitiatedBy).HasMaxLength(240);
            entity.Property(execution => execution.CorrelationId).HasMaxLength(240);
            entity.Property(execution => execution.Status).HasMaxLength(40);
            entity.Property(execution => execution.Message).HasMaxLength(2000);
            entity.Property(execution => execution.ErrorCode).HasMaxLength(120);
            entity.Property(execution => execution.InputJson).HasColumnType("TEXT");
            entity.Property(execution => execution.OutputJson).HasColumnType("TEXT");
        });
    }
}
