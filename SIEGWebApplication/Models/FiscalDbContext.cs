using Microsoft.EntityFrameworkCore;

namespace SIEGWebApplication.Models;

public class FiscalDocument
{
    public Guid Id { get; set; }
    public string AccessKey { get; set; } = string.Empty;
    public int DocumentType { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public DateTimeOffset EmissionDate { get; set; }
    public string EmitterCnpjCpf { get; set; } = string.Empty;
    public string EmitterName { get; set; } = string.Empty;
    public string EmitterState { get; set; } = string.Empty;
    public string? ReceiverCnpjCpf { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string? ReceiverState { get; set; }
    public decimal TotalValue { get; set; }
    public string RawXml { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<FiscalDocumentItem> Items { get; set; } = new List<FiscalDocumentItem>();
}

public class FiscalDocumentItem
{
    public Guid Id { get; set; }
    public Guid FiscalDocumentId { get; set; }
    public int ItemNumber { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Ncm { get; set; }
    public string Cfop { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public FiscalDocument? FiscalDocument { get; set; }
}

public class FiscalDbContext : DbContext
{
    public FiscalDbContext(DbContextOptions<FiscalDbContext> options) : base(options) { }

    public DbSet<FiscalDocument> FiscalDocuments => Set<FiscalDocument>();
    public DbSet<FiscalDocumentItem> FiscalDocumentItems => Set<FiscalDocumentItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Mapeia nomes para minúsculas (PostgreSQL)
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName != null)
                entity.SetTableName(tableName.ToLowerInvariant());

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(property.Name.ToLowerInvariant());
            }
        }

        modelBuilder.Entity<FiscalDocument>(entity =>
        {
            entity.ToTable("fiscaldocuments");
            entity.HasIndex(e => e.AccessKey).IsUnique();
            entity.Property(e => e.AccessKey).HasColumnName("accesskey").HasMaxLength(44);
            entity.Property(e => e.EmitterCnpjCpf).HasColumnName("emittercnpjcpf").HasMaxLength(14);
            entity.Property(e => e.EmitterState).HasColumnName("emitterstate").HasMaxLength(2);
            entity.Property(e => e.ReceiverCnpjCpf).HasColumnName("receivercnpjcpf").HasMaxLength(14);
            entity.Property(e => e.ReceiverState).HasColumnName("receiverstate").HasMaxLength(2);
        });

        modelBuilder.Entity<FiscalDocumentItem>(entity =>
        {
            entity.ToTable("fiscaldocumentitems");
            entity.HasOne(d => d.FiscalDocument)
                .WithMany(p => p.Items)
                .HasForeignKey(d => d.FiscalDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
