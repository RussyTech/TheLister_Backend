using API.Entities;
using API.Entities.DealFinder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public class StoreContext(DbContextOptions options) : IdentityDbContext<ApplicationUser>(options)
{
    // ─── eBay & Amazon Connections ────────────────────────────────────────────
    public DbSet<EbayConnection> EbayConnections { get; set; }
    public DbSet<AmazonConnection> AmazonConnections { get; set; }

    // ─── Product ──────────────────────────────────────────────────────────────
    public DbSet<ProductCache> ProductCache { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }

    // ─── Listings ─────────────────────────────────────────────────────────────
    public DbSet<EbayListing> EbayListings { get; set; }

    public DbSet<EbayToken> EbayTokens { get; set; }

    // ─── Price Intelligence ───────────────────────────────────────────────────
    public DbSet<PriceComparison> PriceComparisons { get; set; }
    public DbSet<DealScan> DealScans { get; set; }

    public DbSet<SourcingDocument> SourcingDocuments => Set<SourcingDocument>();
    public DbSet<DealFinderDeal> DealFinderDeals { get; set; }

    // ─── Auto-update UpdatedAt on every save ──────────────────────────────────
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ─── ApplicationUser timestamps (SQLite) ──────────────────────────────
        builder.Entity<ApplicationUser>()
            .Property(u => u.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        builder.Entity<ApplicationUser>()
            .Property(u => u.UpdatedAt)
            .HasDefaultValueSql("datetime('now')");

        // ─── Identity Roles ───────────────────────────────────────────────────
        builder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "a1b2c3d4-0001-0000-0000-000000000001", Name = "Admin", NormalizedName = "ADMIN" },
            new IdentityRole { Id = "a1b2c3d4-0002-0000-0000-000000000002", Name = "Business", NormalizedName = "BUSINESS" },
            new IdentityRole { Id = "a1b2c3d4-0003-0000-0000-000000000003", Name = "Pro", NormalizedName = "PRO" },
            new IdentityRole { Id = "a1b2c3d4-0004-0000-0000-000000000004", Name = "Standard", NormalizedName = "STANDARD" }
        );

        // ─── ApplicationUser relationships ────────────────────────────────────
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.EbayConnection)
            .WithOne(ec => ec.User)
            .HasForeignKey<EbayConnection>(ec => ec.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApplicationUser>()
            .HasOne(u => u.AmazonConnection)
            .WithOne(ac => ac.User)
            .HasForeignKey<AmazonConnection>(ac => ac.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApplicationUser>()
            .HasMany(u => u.EbayListings)
            .WithOne(l => l.User)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApplicationUser>()
            .HasMany(u => u.ProductCache)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApplicationUser>()
            .HasMany(u => u.PriceComparisons)
            .WithOne(pc => pc.User)
            .HasForeignKey(pc => pc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApplicationUser>()
            .HasMany(u => u.DealScans)
            .WithOne(ds => ds.User)
            .HasForeignKey(ds => ds.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─── ProductCache ─────────────────────────────────────────────────────
        builder.Entity<ProductCache>()
            .HasIndex(p => new { p.UserId, p.Asin });

        builder.Entity<ProductCache>()
            .HasMany(p => p.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductCacheId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProductCache>()
            .HasMany(p => p.EbayListings)
            .WithOne(l => l.Product)
            .HasForeignKey(l => l.ProductCacheId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ProductCache>()
            .Property(p => p.AmazonPrice)
            .HasPrecision(18, 2);

        // ─── EbayListing ──────────────────────────────────────────────────────
        builder.Entity<EbayListing>()
            .HasIndex(l => new { l.UserId, l.Status });

        builder.Entity<EbayListing>()
            .HasIndex(l => l.EbayListingId);

        builder.Entity<EbayListing>()
            .Property(l => l.SellingPrice)
            .HasPrecision(18, 2);

        // Store List<string> ImageUrls as JSON
        builder.Entity<EbayListing>()
            .Property(l => l.ImageUrls)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
            );

        // Store Dictionary<string,string> ItemSpecifics as JSON
        builder.Entity<EbayListing>()
            .Property(l => l.ItemSpecifics)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, string>()
            );

        builder.Entity<EbayToken>(entity =>
        {
            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<EbayToken>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId).IsUnique(); // one token record per user
        });

        // ─── PriceComparison ──────────────────────────────────────────────────
        builder.Entity<PriceComparison>()
            .HasIndex(p => new { p.UserId, p.Asin });

        builder.Entity<PriceComparison>()
            .Property(p => p.AmazonPrice).HasPrecision(18, 2);
        builder.Entity<PriceComparison>()
            .Property(p => p.EbayLowestNewPrice).HasPrecision(18, 2);
        builder.Entity<PriceComparison>()
            .Property(p => p.EbayAverageNewPrice).HasPrecision(18, 2);
        builder.Entity<PriceComparison>()
            .Property(p => p.EstimatedEbayFees).HasPrecision(18, 2);
        builder.Entity<PriceComparison>()
            .Property(p => p.EstimatedProfit).HasPrecision(18, 2);
        builder.Entity<PriceComparison>()
            .Property(p => p.ProfitMarginPercent).HasPrecision(18, 2);

        // ─── DealScan ─────────────────────────────────────────────────────────
        builder.Entity<DealScan>()
            .HasIndex(d => new { d.UserId, d.ScannedAt });

        builder.Entity<DealScan>()
            .Property(d => d.AmazonPrice).HasPrecision(18, 2);
        builder.Entity<DealScan>()
            .Property(d => d.EbayAveragePrice).HasPrecision(18, 2);
        builder.Entity<DealScan>()
            .Property(d => d.ProfitMarginPercent).HasPrecision(18, 2);
    }
}