using LeaseLedger.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LeaseLedger.Infrastructure;

public sealed class LeaseLedgerDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public LeaseLedgerDbContext(DbContextOptions<LeaseLedgerDbContext> options) : base(options)
    {
    }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Tenancy> Tenancies => Set<Tenancy>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<MaintenanceEvent> MaintenanceEvents => Set<MaintenanceEvent>();
    public DbSet<NoticeRecord> NoticeRecords => Set<NoticeRecord>();
    public DbSet<UnitInspection> UnitInspections => Set<UnitInspection>();
    public DbSet<NoticeServiceRecord> NoticeServices => Set<NoticeServiceRecord>();
    public DbSet<NoticeAcknowledgement> NoticeAcknowledgements => Set<NoticeAcknowledgement>();
    public DbSet<NoticeReferral> NoticeReferrals => Set<NoticeReferral>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<InsuranceRecord> InsuranceRecords => Set<InsuranceRecord>();
    public DbSet<LegalContact> LegalContacts => Set<LegalContact>();
    public DbSet<PadAuthorization> PadAuthorizations => Set<PadAuthorization>();
    public DbSet<CardCheckout> CardCheckouts => Set<CardCheckout>();
    public DbSet<LandlordPlan> LandlordPlans => Set<LandlordPlan>();
    public DbSet<RentGuideline> RentGuidelines => Set<RentGuideline>();
    public DbSet<PdfFormTemplate> PdfFormTemplates => Set<PdfFormTemplate>();
    public DbSet<PdfFormField> PdfFormFields => Set<PdfFormField>();
    public DbSet<AgentHistory> AgentHistories => Set<AgentHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RentGuideline>(entity =>
        {
            entity.HasKey(guideline => guideline.Year);
            entity.Property(guideline => guideline.Year).ValueGeneratedNever();
            entity.Property(guideline => guideline.Percent).HasPrecision(5, 2);
        });

        builder.Entity<Property>(entity =>
        {
            entity.Property(property => property.Street).HasMaxLength(200);
            entity.Property(property => property.City).HasMaxLength(100);
            entity.Property(property => property.Province).HasMaxLength(2);
            entity.Property(property => property.PostalCode).HasMaxLength(16);
            entity.HasIndex(property => property.LandlordUserId);
            entity.HasOne<AppUser>().WithMany().HasForeignKey(property => property.LandlordUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(property => property.Units).WithOne(unit => unit.Property).HasForeignKey(unit => unit.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Unit>(entity =>
        {
            entity.Property(unit => unit.Label).HasMaxLength(40);
            entity.HasMany(unit => unit.Tenancies).WithOne(tenancy => tenancy.Unit).HasForeignKey(tenancy => tenancy.UnitId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(unit => unit.Inspections).WithOne(inspection => inspection.Unit).HasForeignKey(inspection => inspection.UnitId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Tenancy>(entity =>
        {
            entity.Property(tenancy => tenancy.TenantName).HasMaxLength(200);
            entity.Property(tenancy => tenancy.TenantEmail).HasMaxLength(256);
            entity.Property(tenancy => tenancy.RentAmount).HasPrecision(18, 2);
            entity.Property(tenancy => tenancy.Frequency).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(tenancy => tenancy.TenantEmail);
            entity.HasIndex(tenancy => tenancy.TenantUserId);
            entity.HasOne<AppUser>().WithMany().HasForeignKey(tenancy => tenancy.TenantUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(tenancy => tenancy.LedgerEntries).WithOne(entry => entry.Tenancy).HasForeignKey(entry => entry.TenancyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(tenancy => tenancy.MaintenanceRequests).WithOne(request => request.Tenancy).HasForeignKey(request => request.TenancyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(tenancy => tenancy.Notices).WithOne(notice => notice.Tenancy).HasForeignKey(notice => notice.TenancyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NoticeRecord>(entity =>
        {
            entity.Property(notice => notice.Kind).HasConversion<string>().HasMaxLength(8);
            entity.Property(notice => notice.Amount).HasPrecision(18, 2);
            entity.Property(notice => notice.Summary).HasMaxLength(1000);
            entity.HasOne<AppUser>().WithMany().HasForeignKey(notice => notice.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(notice => notice.Services).WithOne(service => service.Notice).HasForeignKey(service => service.NoticeRecordId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(notice => notice.Acknowledgements).WithOne(item => item.Notice).HasForeignKey(item => item.NoticeRecordId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(notice => notice.Referrals).WithOne(item => item.Notice).HasForeignKey(item => item.NoticeRecordId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NoticeServiceRecord>(entity =>
        {
            entity.Property(service => service.Method).HasConversion<string>().HasMaxLength(16);
            entity.Property(service => service.Note).HasMaxLength(1000);
        });

        builder.Entity<NoticeAcknowledgement>(entity => entity.Property(item => item.SignedName).HasMaxLength(200));

        builder.Entity<NoticeReferral>(entity =>
        {
            entity.Property(item => item.ContactName).HasMaxLength(200);
            entity.Property(item => item.ContactEmail).HasMaxLength(256);
        });

        builder.Entity<WorkOrder>(entity =>
        {
            entity.Property(order => order.TradeName).HasMaxLength(200);
            entity.Property(order => order.Phone).HasMaxLength(40);
            entity.Property(order => order.Trade).HasMaxLength(80);
            entity.Property(order => order.Notes).HasMaxLength(2000);
            entity.HasOne(order => order.Request).WithMany(request => request.WorkOrders).HasForeignKey(order => order.MaintenanceRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InsuranceRecord>(entity =>
        {
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.BrokerName).HasMaxLength(200);
            entity.Property(item => item.PolicyNumber).HasMaxLength(80);
            entity.Property(item => item.Note).HasMaxLength(1000);
            entity.HasOne(item => item.Tenancy).WithMany().HasForeignKey(item => item.TenancyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LegalContact>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(200);
            entity.Property(item => item.Email).HasMaxLength(256);
            entity.Property(item => item.Firm).HasMaxLength(200);
            entity.HasIndex(item => item.LandlordUserId);
        });

        builder.Entity<PadAuthorization>(entity =>
        {
            entity.Property(item => item.SignedName).HasMaxLength(200);
            entity.HasIndex(item => item.TenancyId).IsUnique();
            entity.HasOne(item => item.Tenancy).WithMany().HasForeignKey(item => item.TenancyId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CardCheckout>(entity =>
        {
            entity.Property(item => item.Kind).HasMaxLength(32);
            entity.Property(item => item.StripeSessionId).HasMaxLength(128);
            entity.Property(item => item.Status).HasMaxLength(16);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.ReceiptNumber).HasMaxLength(32);
            entity.HasIndex(item => item.StripeSessionId).IsUnique().HasFilter("[StripeSessionId] IS NOT NULL");
        });

        builder.Entity<LandlordPlan>(entity =>
        {
            entity.HasKey(item => item.LandlordUserId);
            entity.Property(item => item.Status).HasMaxLength(16);
            entity.Property(item => item.StripeCustomerId).HasMaxLength(128);
            entity.Property(item => item.StripeSubscriptionId).HasMaxLength(128);
        });

        builder.Entity<UnitInspection>(entity =>
        {
            entity.Property(inspection => inspection.Summary).HasMaxLength(200);
            entity.Property(inspection => inspection.Notes).HasMaxLength(4000);
            entity.HasOne<AppUser>().WithMany().HasForeignKey(inspection => inspection.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LedgerEntry>(entity =>
        {
            entity.Property(entry => entry.Amount).HasPrecision(18, 2);
            entity.Property(entry => entry.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(entry => entry.ChargeCategory).HasConversion<string>().HasMaxLength(16);
            entity.Property(entry => entry.PaymentMethod).HasConversion<string>().HasMaxLength(16);
            entity.Property(entry => entry.Description).HasMaxLength(200);
            entity.Property(entry => entry.Note).HasMaxLength(1000);
            entity.Property(entry => entry.ReceiptNumber).HasMaxLength(32);
            entity.HasIndex(entry => entry.ReceiptNumber).IsUnique().HasFilter("[ReceiptNumber] IS NOT NULL");
        });

        builder.Entity<MaintenanceRequest>(entity =>
        {
            entity.Property(request => request.Title).HasMaxLength(160);
            entity.Property(request => request.Description).HasMaxLength(4000);
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasMany(request => request.Events).WithOne(evt => evt.Request).HasForeignKey(evt => evt.RequestId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MaintenanceEvent>(entity =>
        {
            entity.Property(evt => evt.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(evt => evt.Note).HasMaxLength(1000);
            entity.HasOne<AppUser>().WithMany().HasForeignKey(evt => evt.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PdfFormTemplate>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(200);
            entity.Property(item => item.FileName).HasMaxLength(260);
            entity.Property(item => item.NoticeKind).HasConversion<string>().HasMaxLength(8);
            entity.HasIndex(item => item.LandlordUserId);
            entity.HasIndex(item => new { item.LandlordUserId, item.NoticeKind })
                .IsUnique()
                .HasFilter("[NoticeKind] IS NOT NULL");
            entity.HasOne<AppUser>().WithMany().HasForeignKey(item => item.LandlordUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(item => item.Fields).WithOne(field => field.Template).HasForeignKey(field => field.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AgentHistory>(entity =>
        {
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserId).ValueGeneratedNever();
            entity.Property(item => item.Payload).IsRequired();
            entity.HasOne<AppUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PdfFormField>(entity =>
        {
            entity.Property(field => field.Name).HasMaxLength(1024);
            entity.Property(field => field.Label).HasMaxLength(200);
            entity.Property(field => field.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(field => field.Options).HasMaxLength(4000);
            entity.Property(field => field.DataKey).HasMaxLength(64);
            entity.Property(field => field.ConstantValue).HasMaxLength(500);
            entity.Property(field => field.Format).HasConversion<string>().HasMaxLength(20);
        });
    }
}
