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
    public DbSet<AgentVoice> AgentVoices => Set<AgentVoice>();
    public DbSet<Domain> Domains => Set<Domain>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<PhoneNumberStatus> PhoneNumberStatuses => Set<PhoneNumberStatus>();
    public DbSet<ConversationStatus> ConversationStatuses => Set<ConversationStatus>();
    public DbSet<ConversationType> ConversationTypes => Set<ConversationType>();
    public DbSet<InvoiceStatus> InvoiceStatuses => Set<InvoiceStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Plan
        modelBuilder.Entity<Plan>().ToTable("Plan").HasKey(e => e.PlanId);

        // Status / type lookups (normalized from former varchar columns)
        modelBuilder.Entity<PhoneNumberStatus>(e =>
        {
            e.ToTable("PhoneNumberStatus");
            e.HasKey(x => x.PhoneNumberStatusId);
            e.Property(x => x.PhoneNumberStatusId).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(30);
            e.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<ConversationStatus>(e =>
        {
            e.ToTable("ConversationStatus");
            e.HasKey(x => x.ConversationStatusId);
            e.Property(x => x.ConversationStatusId).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(30);
            e.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<ConversationType>(e =>
        {
            e.ToTable("ConversationType");
            e.HasKey(x => x.ConversationTypeId);
            e.Property(x => x.ConversationTypeId).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(30);
            e.HasIndex(x => x.Name).IsUnique();
        });
        modelBuilder.Entity<InvoiceStatus>(e =>
        {
            e.ToTable("InvoiceStatus");
            e.HasKey(x => x.InvoiceStatusId);
            e.Property(x => x.InvoiceStatusId).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(30);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ProviderType (LLM / Payment / Phone / SMS / Email lookup)
        modelBuilder.Entity<ProviderType>(e =>
        {
            e.ToTable("ProviderType");
            e.HasKey(x => x.ProviderTypeId);
            e.Property(x => x.ProviderTypeId).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(20);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // Provider (concrete provider rows: Stripe, Qwen, Twilio, SendGrid, ...)
        modelBuilder.Entity<Provider>(e =>
        {
            e.ToTable("Provider");
            e.HasKey(x => x.ProviderId);
            e.Property(x => x.Name).HasMaxLength(50);
            e.Property(x => x.Code).HasMaxLength(30);
            e.HasIndex(x => new { x.Code, x.ProviderTypeId }).IsUnique();
            e.HasOne(x => x.ProviderType).WithMany(t => t.Providers).HasForeignKey(x => x.ProviderTypeId);
        });

        // AgentVoice (selectable AI-agent voices, owned by an LLM Provider)
        modelBuilder.Entity<AgentVoice>(e =>
        {
            e.ToTable("AgentVoice");
            e.HasKey(x => x.AgentVoiceId);
            e.Property(x => x.ApiName).HasMaxLength(64);
            e.Property(x => x.Label).HasMaxLength(128);
            e.Property(x => x.Gender).HasMaxLength(10);
            e.HasIndex(x => new { x.ProviderId, x.ApiName }).IsUnique();
            e.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId);
            e.HasOne(x => x.Language).WithMany().HasForeignKey(x => x.LanguageId);
        });

        // PlanPrice (one row per plan x currency x provider)
        modelBuilder.Entity<PlanPrice>(e =>
        {
            e.ToTable("PlanPrice");
            e.HasKey(x => x.PlanPriceId);
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
            e.HasKey(x => x.PaymentCustomerId);
            e.Property(x => x.CustomerId).HasMaxLength(255);
            e.HasIndex(x => new { x.AccountId, x.ProviderId }).IsUnique();
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId);
            e.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId);
        });

        // BusinessCategory
        modelBuilder.Entity<BusinessCategory>().ToTable("BusinessCategory").HasKey(e => e.BusinessCategoryId);

        // Country
        modelBuilder.Entity<Country>(e =>
        {
            e.ToTable("Country");
            e.HasKey(x => x.CountryId);
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
        modelBuilder.Entity<Language>().ToTable("Language").HasKey(e => e.LanguageId);

        // Region
        modelBuilder.Entity<Region>(e =>
        {
            e.ToTable("Region");
            e.HasKey(x => x.RegionId);
            e.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryId).HasConstraintName("FK_Region_Country").OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.CountryId, x.Code }).IsUnique();
        });

        // Account
        modelBuilder.Entity<Account>(e =>
        {
            // The Account table has an AFTER UPDATE trigger (TR_Account_UpdatedAt),
            // so EF must not use the SQL OUTPUT clause for inserts/updates.
            e.ToTable("Account", tb => tb.HasTrigger("TR_Account_UpdatedAt"));
            e.HasKey(x => x.AccountId);
            e.HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
            e.HasIndex(x => x.GoogleSubjectId).IsUnique().HasFilter("[GoogleSubjectId] IS NOT NULL");
            e.Property(x => x.Email).HasMaxLength(256);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).HasConstraintName("FK_Account_Plan").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.BusinessCategoryId).HasConstraintName("FK_Account_Category").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.AgentVoice).WithMany().HasForeignKey(x => x.AgentVoiceId).HasConstraintName("FK_Account_AgentVoice").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.AssignedPhoneNumber).WithMany().HasForeignKey(x => x.AssignedPhoneNumberId).HasConstraintName("FK_Account_AssignedPhoneNumber").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).HasConstraintName("FK_Account_Region").OnDelete(DeleteBehavior.NoAction);
            e.Property(a => a.CountryCodeId).HasColumnName("CountryId");
            e.HasIndex(x => x.StripeCustomerId).HasFilter("[StripeCustomerId] IS NOT NULL");
            e.Property(x => x.PhoneCountryCode).IsFixedLength().HasMaxLength(2);
            e.Property(x => x.AccountLanguage).HasMaxLength(10);
        });

        // Conversation
        modelBuilder.Entity<Conversation>(e =>
        {
            e.ToTable("Conversation");
            e.HasKey(x => x.ConversationId);
            e.HasOne(x => x.Account).WithMany(x => x.Conversations).HasForeignKey(x => x.AccountId);
            e.HasOne(x => x.Language).WithMany().HasForeignKey(x => x.LanguageId);
            e.HasIndex(x => new { x.AccountId, x.DateTimeUtc }).IsDescending(false, true);
            e.HasIndex(x => new { x.AccountId, x.ConversationStatusId });
            e.HasOne(x => x.ConversationStatus).WithMany(s => s.Conversations).HasForeignKey(x => x.ConversationStatusId);
            e.HasOne(x => x.ConversationType).WithMany(t => t.Conversations).HasForeignKey(x => x.ConversationTypeId);
        });

        // Invoice
        modelBuilder.Entity<Invoice>(e =>
        {
            e.ToTable("Invoice");
            e.HasKey(x => x.InvoiceId);
            e.HasOne(x => x.Account).WithMany(x => x.Invoices).HasForeignKey(x => x.AccountId);
            e.HasIndex(x => new { x.AccountId, x.DateUtc }).IsDescending(false, true);
            e.Property(x => x.Amount).HasColumnType("decimal(10,2)");
            e.HasOne(x => x.InvoiceStatus).WithMany(s => s.Invoices).HasForeignKey(x => x.InvoiceStatusId);
        });

        // OtpCode
        modelBuilder.Entity<OtpCode>(e =>
        {
            e.ToTable("OtpCode");
            e.HasKey(x => x.OtpCodeId);
            e.HasIndex(x => new { x.Identifier, x.Type }).HasFilter("[IsUsed] = 0");
        });

        // ContactSubmission
        modelBuilder.Entity<ContactSubmission>(e =>
        {
            e.ToTable("ContactSubmission");
            e.HasKey(x => x.ContactSubmissionId);
            e.HasIndex(x => x.CreatedAt).IsDescending();
        });

        // PhoneNumber
        modelBuilder.Entity<PhoneNumber>(e =>
        {
            e.ToTable("PhoneNumber");
            e.HasKey(x => x.PhoneNumberId);
            e.HasIndex(x => x.Sid).IsUnique();
            e.HasIndex(x => x.PhoneNumberStatusId);
            e.HasIndex(x => new { x.CountryId, x.PhoneNumberStatusId });
            e.HasIndex(x => x.AssignedAccountId);
            e.Property(x => x.MonthlyCost).HasColumnType("decimal(10,2)");
            e.HasOne(x => x.AssignedAccount).WithMany().HasForeignKey(x => x.AssignedAccountId);
            e.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryId);
            e.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId);
            e.HasOne(x => x.PhoneNumberStatus).WithMany(s => s.PhoneNumbers).HasForeignKey(x => x.PhoneNumberStatusId);
        });
    }
}
