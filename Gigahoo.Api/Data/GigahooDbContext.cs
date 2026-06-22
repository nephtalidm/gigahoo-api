using Gigahoo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gigahoo.Api.Data;

public class GigahooDbContext(DbContextOptions<GigahooDbContext> options) : DbContext(options)
{
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<BusinessCategory> BusinessCategories => Set<BusinessCategory>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<FeatureSettings> FeatureSettings => Set<FeatureSettings>();
    public DbSet<Call> Calls => Set<Call>();
    public DbSet<CallCollectedInfo> CallCollectedInfo => Set<CallCollectedInfo>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");

        // Plan
        modelBuilder.Entity<Plan>().ToTable("Plans").HasKey(e => e.Id);

        // BusinessCategory
        modelBuilder.Entity<BusinessCategory>().ToTable("BusinessCategories").HasKey(e => e.Id);

        // Country
        modelBuilder.Entity<Country>(e =>
        {
            e.ToTable("Countries");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).IsFixedLength().HasMaxLength(2);
        });

        // Language
        modelBuilder.Entity<Language>().ToTable("Languages").HasKey(e => e.Id);

        // Region
        modelBuilder.Entity<Region>(e =>
        {
            e.ToTable("Regions");
            e.HasKey(x => x.Id);
            e.HasOne<Country>().WithMany().HasForeignKey(x => x.CountryId);
            e.HasIndex(x => new { x.CountryId, x.Code }).IsUnique();
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
            e.HasIndex(x => x.NormalizedPhone).IsUnique().HasFilter("[NormalizedPhone] IS NOT NULL");
            e.HasIndex(x => x.GoogleSubjectId).IsUnique().HasFilter("[GoogleSubjectId] IS NOT NULL");
            e.Property(x => x.NormalizedEmail).HasMaxLength(256);
            e.Property(x => x.NormalizedPhone).HasMaxLength(50);
        });

        // Account
        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("Accounts");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User).WithOne(x => x.Account).HasForeignKey<Account>(x => x.UserId);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
            e.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryId);
            e.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId);
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasIndex(x => x.StripeCustomerId).HasFilter("[StripeCustomerId] IS NOT NULL");
            e.Property(x => x.PhoneCountryCode).IsFixedLength().HasMaxLength(2);
        });

        // FeatureSettings
        modelBuilder.Entity<FeatureSettings>(e =>
        {
            e.ToTable("FeatureSettings");
            e.HasKey(x => x.AccountId);
            e.HasOne(x => x.Account).WithOne(x => x.FeatureSettings).HasForeignKey<FeatureSettings>(x => x.AccountId);
        });

        // Call
        modelBuilder.Entity<Call>(e =>
        {
            e.ToTable("Calls");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Account).WithMany(x => x.Calls).HasForeignKey(x => x.AccountId);
            e.HasOne(x => x.Language).WithMany().HasForeignKey(x => x.LanguageId);
            e.HasIndex(x => new { x.AccountId, x.DateTimeUtc }).IsDescending(false, true);
            e.HasIndex(x => new { x.AccountId, x.Status });
        });

        // CallCollectedInfo
        modelBuilder.Entity<CallCollectedInfo>(e =>
        {
            e.ToTable("CallCollectedInfo");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Call).WithMany(x => x.CollectedInfo).HasForeignKey(x => x.CallId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CallId);
        });

        // Invoice
        modelBuilder.Entity<Invoice>(e =>
        {
            e.ToTable("Invoices");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Account).WithMany(x => x.Invoices).HasForeignKey(x => x.AccountId);
            e.HasIndex(x => new { x.AccountId, x.DateUtc }).IsDescending(false, true);
            e.Property(x => x.Amount).HasColumnType("decimal(10,2)");
        });

        // PaymentMethod
        modelBuilder.Entity<PaymentMethod>(e =>
        {
            e.ToTable("PaymentMethods");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Account).WithMany(x => x.PaymentMethods).HasForeignKey(x => x.AccountId);
            e.HasIndex(x => x.AccountId);
            e.Property(x => x.Last4).IsFixedLength().HasMaxLength(4);
        });

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        // OtpCode
        modelBuilder.Entity<OtpCode>(e =>
        {
            e.ToTable("OtpCodes");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Identifier, x.Type }).HasFilter("[IsUsed] = 0");
        });

        // ContactSubmission
        modelBuilder.Entity<ContactSubmission>(e =>
        {
            e.ToTable("ContactSubmissions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CreatedAt).IsDescending();
        });
    }
}
