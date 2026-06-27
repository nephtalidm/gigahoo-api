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
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();
    public DbSet<PhoneNumber> PhoneNumbers => Set<PhoneNumber>();
    public DbSet<PlanPrice> PlanPrices => Set<PlanPrice>();
    public DbSet<PaymentCustomer> PaymentCustomers => Set<PaymentCustomer>();
    public DbSet<ProviderType> ProviderTypes => Set<ProviderType>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Domain> Domains => Set<Domain>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Plan
        modelBuilder.Entity<Plan>().ToTable("Plan").HasKey(e => e.Id);

        // ProviderType (LLM / Payment / Phone / SMS / Email lookup)
        modelBuilder.Entity<ProviderType>(e =>
        {
            e.ToTable("ProviderType");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(20);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // Provider (concrete provider rows: Stripe, Qwen, Twilio, SendGrid, ...)
        modelBuilder.Entity<Provider>(e =>
        {
            e.ToTable("Provider");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(50);
            e.Property(x => x.Code).HasMaxLength(30);
            e.HasIndex(x => new { x.Code, x.ProviderTypeId }).IsUnique();
            e.HasOne(x => x.ProviderType).WithMany(t => t.Providers).HasForeignKey(x => x.ProviderTypeId);
        });

        // PlanPrice (one row per plan x currency x provider)
        modelBuilder.Entity<PlanPrice>(e =>
        {
            e.ToTable("PlanPrice");
            e.HasKey(x => x.Id);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.ProviderPriceId).HasMaxLength(255);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.HasIndex(x => new { x.PlanId, x.Currency, x.ProviderId }).IsUnique();
            e.HasOne(x => x.Plan).WithMany(p => p.Prices).HasForeignKey(x => x.PlanId);
            e.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId);
        });

        // PaymentCustomer (provider customer id per account x provider)
        modelBuilder.Entity<PaymentCustomer>(e =>
        {
            e.ToTable("PaymentCustomer");
            e.HasKey(x => x.Id);
            e.Property(x => x.CustomerId).HasMaxLength(255);
            e.HasIndex(x => new { x.AccountId, x.ProviderId }).IsUnique();
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId);
            e.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId);
        });

        // BusinessCategory
        modelBuilder.Entity<BusinessCategory>().ToTable("BusinessCategory").HasKey(e => e.Id);

        // Country
        modelBuilder.Entity<Country>(e =>
        {
            e.ToTable("Country");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).IsFixedLength().HasMaxLength(2);
            e.Property(x => x.Currency).HasMaxLength(3);
        });

        // Domain (regional domain host -> optional forced country)
        modelBuilder.Entity<Domain>(e =>
        {
            e.ToTable("Domain");
            e.HasKey(x => x.Host);
            e.Property(x => x.Host).HasMaxLength(100);
            e.Property(x => x.CountryCode).HasMaxLength(2);
        });

        // Setting (general website key/value settings)
        modelBuilder.Entity<Setting>(e =>
        {
            e.ToTable("Setting");
            e.HasKey(x => x.SettingKey);
            e.Property(x => x.SettingKey).HasMaxLength(100);
        });

        // Language
        modelBuilder.Entity<Language>().ToTable("Language").HasKey(e => e.Id);

        // Region
        modelBuilder.Entity<Region>(e =>
        {
            e.ToTable("Region");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryId).HasConstraintName("FK_Region_Country").OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.CountryId, x.Code }).IsUnique();
        });

        // Account
        modelBuilder.Entity<Account>(e =>
        {
            // The Account table has an AFTER UPDATE trigger (TR_Account_UpdatedAt),
            // so EF must not use the SQL OUTPUT clause for inserts/updates.
            e.ToTable("Account", tb => tb.HasTrigger("TR_Account_UpdatedAt"));
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
            e.HasIndex(x => x.NormalizedPhone).IsUnique().HasFilter("[NormalizedPhone] IS NOT NULL");
            e.HasIndex(x => x.GoogleSubjectId).IsUnique().HasFilter("[GoogleSubjectId] IS NOT NULL");
            e.Property(x => x.NormalizedEmail).HasMaxLength(256);
            e.Property(x => x.NormalizedPhone).HasMaxLength(50);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).HasConstraintName("FK_Account_Plan").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).HasConstraintName("FK_Account_Category").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).HasConstraintName("FK_Account_Region").OnDelete(DeleteBehavior.NoAction);
            e.Property(a => a.CountryCodeId).HasColumnName("CountryId");
            e.HasIndex(x => x.StripeCustomerId).HasFilter("[StripeCustomerId] IS NOT NULL");
            e.Property(x => x.PhoneCountryCode).IsFixedLength().HasMaxLength(2);
            e.HasOne(x => x.LlmProvider).WithMany().HasForeignKey(x => x.LlmProviderId).HasConstraintName("FK_Account_LlmProvider").OnDelete(DeleteBehavior.NoAction);
        });

        // Conversation
        modelBuilder.Entity<Conversation>(e =>
        {
            e.ToTable("Conversation");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Account).WithMany(x => x.Conversations).HasForeignKey(x => x.AccountId);
            e.HasOne(x => x.Language).WithMany().HasForeignKey(x => x.LanguageId);
            e.HasIndex(x => new { x.AccountId, x.DateTimeUtc }).IsDescending(false, true);
            e.HasIndex(x => new { x.AccountId, x.Status });
        });

        // Invoice
        modelBuilder.Entity<Invoice>(e =>
        {
            e.ToTable("Invoice");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Account).WithMany(x => x.Invoices).HasForeignKey(x => x.AccountId);
            e.HasIndex(x => new { x.AccountId, x.DateUtc }).IsDescending(false, true);
            e.Property(x => x.Amount).HasColumnType("decimal(10,2)");
        });

        // OtpCode
        modelBuilder.Entity<OtpCode>(e =>
        {
            e.ToTable("OtpCode");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Identifier, x.Type }).HasFilter("[IsUsed] = 0");
        });

        // ContactSubmission
        modelBuilder.Entity<ContactSubmission>(e =>
        {
            e.ToTable("ContactSubmission");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CreatedAt).IsDescending();
        });

        // PhoneNumber
        modelBuilder.Entity<PhoneNumber>(e =>
        {
            e.ToTable("PhoneNumber");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Sid).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => new { x.CountryCode, x.Status });
            e.HasIndex(x => x.AssignedAccountId);
            e.Property(x => x.CountryCode).IsFixedLength().HasMaxLength(2);
            e.Property(x => x.MonthlyCost).HasColumnType("decimal(10,2)");
            e.Property(x => x.Status).HasConversion<string>(); // Store enum as string
            e.HasOne(x => x.AssignedAccount).WithMany().HasForeignKey(x => x.AssignedAccountId);
        });
    }
}
