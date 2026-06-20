using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Requests;
using NOOSE_Website.Data.Entities.Announcements;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Watchlist;
using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data;

/// <summary>Central EF-Core context.</summary>
public class AppDbContext : IdentityDbContext<Agent>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();

    // person records
    public DbSet<Person> People => Set<Person>();
    public DbSet<PersonDoc> PersonDocs => Set<PersonDoc>();
    public DbSet<PersonPhoto> PersonPhotos => Set<PersonPhoto>();
    public DbSet<ClassificationHistory> ClassificationHistory => Set<ClassificationHistory>();
    public DbSet<PersonAlias> PersonAliases => Set<PersonAlias>();
    public DbSet<PersonPhone> PersonPhones => Set<PersonPhone>();
    public DbSet<PersonVehicle> PersonVehicles => Set<PersonVehicle>();
    public DbSet<PersonLocation> PersonLocations => Set<PersonLocation>();
    public DbSet<PersonWeapon> PersonWeapons => Set<PersonWeapon>();
    public DbSet<ProfileSuggestion> ProfileSuggestions => Set<ProfileSuggestion>();
    public DbSet<CaseNumberCounter> CaseNumberCounter => Set<CaseNumberCounter>();

    // ---- cross-cutting: tags, comments, sources ----
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TagMapping> TagMappings => Set<TagMapping>();
    public DbSet<Comment> Comments => Set<Comment>();

    // ---- links and relations ----
    public DbSet<Link> Links => Set<Link>();
    public DbSet<PersonRelation> PersonRelations => Set<PersonRelation>();

    public DbSet<SavedSearch> SavedSearch => Set<SavedSearch>();

    // factions
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<FactionRank> FactionRanks => Set<FactionRank>();
    public DbSet<FactionWeaponStock> FactionWeaponStocks => Set<FactionWeaponStock>();
    public DbSet<FactionInventory> FactionInventories => Set<FactionInventory>();
    public DbSet<FactionDrugRoute> FactionDrugRoutes => Set<FactionDrugRoute>();
    public DbSet<FactionMember> FactionMembers => Set<FactionMember>();
    public DbSet<FactionAgent> FactionAgents => Set<FactionAgent>();
    public DbSet<FactionPhoto> FactionPhotos => Set<FactionPhoto>();
    public DbSet<FactionActivity> FactionActivities => Set<FactionActivity>();

    // person groups
    public DbSet<PersonGroup> PersonGroups => Set<PersonGroup>();
    public DbSet<PersonGroupMember> PersonGroupMembers => Set<PersonGroupMember>();
    public DbSet<PersonGroupAgent> PersonGroupAgents => Set<PersonGroupAgent>();

    // parties
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartyMember> PartyMembers => Set<PartyMember>();
    public DbSet<PartyAgent> PartyAgents => Set<PartyAgent>();

    // operations
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<OperationAgent> OperationAgents => Set<OperationAgent>();

    // cases
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseAgent> CaseAgents => Set<CaseAgent>();

    // taskforces
    public DbSet<Taskforce> Taskforces => Set<Taskforce>();
    public DbSet<TaskforceAgent> TaskforceAgents => Set<TaskforceAgent>();
    public DbSet<TaskforceMessage> TaskforceMessages => Set<TaskforceMessage>();

    // ---- observations ----
    public DbSet<Observation> Observations => Set<Observation>();

    // per-agent personnel file
    public DbSet<AgentRankHistory> AgentRankHistories => Set<AgentRankHistory>();
    public DbSet<AgentNote> AgentNotes => Set<AgentNote>();
    public DbSet<AgentPromotionRequest> AgentPromotionRequests => Set<AgentPromotionRequest>();

    // training modules + per-agent completion
    public DbSet<TrainingModule> TrainingModules => Set<TrainingModule>();
    public DbSet<AgentModuleCompletion> AgentModuleCompletions => Set<AgentModuleCompletion>();

    // request/inbox workflow
    public DbSet<Request> Requests => Set<Request>();

    // in-app notifications
    public DbSet<Notification> Notifications => Set<Notification>();

    // watchlist (followed records)
    public DbSet<WatchlistEntry> Watchlists => Set<WatchlistEntry>();

    // jobs/to-dos & assignments
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobAssignment> JobAssignments => Set<JobAssignment>();

    // calendar appointments & participants
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentAssignment> AppointmentAssignments => Set<AppointmentAssignment>();

    // ---- announcements ----
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementAcknowledgment> AnnouncementAcknowledgments => Set<AnnouncementAcknowledgment>();

    // doc templates (admin-defined entry masks)
    public DbSet<DocTemplate> DocTemplates => Set<DocTemplate>();

    // configurable custom fields per record type
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();

    // document library + document templates
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();

    // ---- recency + followups ----
    public DbSet<RecencyThreshold> RecencyThresholds => Set<RecencyThreshold>();
    public DbSet<ThreatScoreConfig> ThreatScoreConfigs => Set<ThreatScoreConfig>();
    public DbSet<Followup> Followups => Set<Followup>();

    // archived monthly situation reports
    public DbSet<SituationReport> SituationReports => Set<SituationReport>();

    // system settings, law book, file library
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Law> Laws => Set<Law>();
    public DbSet<LibraryFile> LibraryFiles => Set<LibraryFile>();

    // ---- partner record releases ----
    public DbSet<PartnerShare> PartnerShares => Set<PartnerShare>();
    public DbSet<DocumentAccessExclusion> DocumentAccessExclusions => Set<DocumentAccessExclusion>();

    // ---- recruiting (applications, invites, tests) ----
    public DbSet<AgentInvite> AgentInvites => Set<AgentInvite>();
    public DbSet<Bewerbung> Bewerbungen => Set<Bewerbung>();
    public DbSet<BewerbungMessage> BewerbungMessages => Set<BewerbungMessage>();
    public DbSet<BewerbungTest> BewerbungTests => Set<BewerbungTest>();
    public DbSet<BewerbungTestQuestion> BewerbungTestQuestions => Set<BewerbungTestQuestion>();
    public DbSet<BewerbungTestOption> BewerbungTestOptions => Set<BewerbungTestOption>();
    public DbSet<BewerbungTestAssignment> BewerbungTestAssignments => Set<BewerbungTestAssignment>();
    public DbSet<BewerbungTestAnswer> BewerbungTestAnswers => Set<BewerbungTestAnswer>();
    public DbSet<Bewerbungssperre> Bewerbungssperren => Set<Bewerbungssperre>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Agent>(b =>
        {
            b.Property(a => a.Codename).HasMaxLength(128);
            b.Property(a => a.RealName).HasMaxLength(128);
            b.Property(a => a.BadgeNumber).HasMaxLength(32);
            b.Property(a => a.DiscordId).HasMaxLength(32);
            b.Property(a => a.DiscordUsername).HasMaxLength(64);
            b.Property(a => a.AvatarUrl).HasMaxLength(512);
            b.Property(a => a.BlockedReason).HasMaxLength(512);
            // longtext for JSON
            b.Property(a => a.NavPreferencesJson).HasColumnType("longtext");
            b.HasIndex(a => a.DiscordId).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.Property(a => a.EntityType).HasMaxLength(128);
            b.Property(a => a.EntityId).HasMaxLength(64);
            b.Property(a => a.AgentId).HasMaxLength(64);
            b.Property(a => a.AgentName).HasMaxLength(128);
            b.HasIndex(a => new { a.EntityType, a.EntityId });
            b.HasIndex(a => a.Timestamp);
        });

        modelBuilder.Entity<AccessLog>(b =>
        {
            b.Property(a => a.EntityType).HasMaxLength(128);
            b.Property(a => a.EntityId).HasMaxLength(64);
            b.Property(a => a.AgentId).HasMaxLength(64);
            b.Property(a => a.AgentName).HasMaxLength(128);
            b.HasIndex(a => new { a.EntityType, a.EntityId });
        });

        modelBuilder.Entity<Person>(b =>
        {
            b.Property(p => p.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.ThreatDetailJson).HasColumnType("longtext");
            b.HasIndex(p => p.CaseNumber).IsUnique();
            b.HasIndex(p => p.Name);
            b.HasIndex(p => p.IsClassified);

            b.HasMany(p => p.Aliases).WithOne(a => a.Person!)
                .HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.PhoneNumbers).WithOne(t => t.Person!)
                .HasForeignKey(t => t.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Vehicles).WithOne(f => f.Person!)
                .HasForeignKey(f => f.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Locations).WithOne(o => o.Person!)
                .HasForeignKey(o => o.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Weapons).WithOne(w => w.Person!)
                .HasForeignKey(w => w.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Photos).WithOne(f => f.Person!)
                .HasForeignKey(f => f.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Docs).WithOne(d => d.Person!)
                .HasForeignKey(d => d.PersonId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Observations).WithOne(o => o.Person!)
                .HasForeignKey(o => o.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PersonAlias>(b => b.Property(a => a.AliasName).HasMaxLength(200));
        modelBuilder.Entity<PersonPhone>(b =>
        {
            b.Property(t => t.Number).HasMaxLength(40);
            b.Property(t => t.Designation).HasMaxLength(100);
            b.HasIndex(t => t.Number);
        });
        modelBuilder.Entity<PersonVehicle>(b =>
        {
            b.Property(f => f.Designation).HasMaxLength(200);
            b.Property(f => f.LicensePlate).HasMaxLength(40);
        });
        modelBuilder.Entity<PersonLocation>(b =>
        {
            b.Property(o => o.Text).HasMaxLength(300);
            b.Property(o => o.Note).HasMaxLength(500);
        });
        modelBuilder.Entity<PersonWeapon>(b => b.Property(w => w.Text).HasMaxLength(200));

        modelBuilder.Entity<ProfileSuggestion>(b =>
        {
            b.Property(v => v.Value).HasMaxLength(300).IsRequired();
            b.HasIndex(v => new { v.Type, v.Value }).IsUnique();
        });

        modelBuilder.Entity<PersonPhoto>(b =>
        {
            b.Property(f => f.FileNameSaved).HasMaxLength(128);
            b.Property(f => f.OriginalName).HasMaxLength(260);
            b.Property(f => f.ContentType).HasMaxLength(100);
            b.Property(f => f.CreatedById).HasMaxLength(64);
        });

        modelBuilder.Entity<ClassificationHistory>(b =>
        {
            b.Property(e => e.EntityType).HasMaxLength(128);
            b.Property(e => e.EntityId).HasMaxLength(64);
            b.Property(e => e.Justification).HasMaxLength(1000);
            b.Property(e => e.AgentId).HasMaxLength(64);
            b.Property(e => e.AgentName).HasMaxLength(128);
            b.Property(e => e.RequestId).HasMaxLength(64);
            b.HasIndex(e => new { e.EntityType, e.EntityId });
        });

        modelBuilder.Entity<PersonDoc>(b =>
        {
            b.Property(d => d.Faction).HasMaxLength(200);
            b.Property(d => d.OrgType).HasMaxLength(128);
            b.Property(d => d.OrgId).HasMaxLength(64);
            b.HasIndex(d => d.PersonId);
            b.HasIndex(d => new { d.OrgType, d.OrgId });
        });

        modelBuilder.Entity<Observation>(b =>
        {
            b.Property(o => o.Location).HasMaxLength(300);
            b.Property(o => o.Sighting).HasMaxLength(4000);
            b.Property(o => o.Result).HasMaxLength(4000);
            b.Property(o => o.OrgType).HasMaxLength(128);
            b.Property(o => o.OrgId).HasMaxLength(64);
            b.HasIndex(o => o.PersonId);
            b.HasIndex(o => o.Start);
            b.HasIndex(o => new { o.OrgType, o.OrgId });
            b.HasOne(o => o.ObservingAgent).WithMany()
                .HasForeignKey(o => o.ObservingAgentId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(o => o.ObservingAgentId);
        });

        modelBuilder.Entity<CaseNumberCounter>(b =>
        {
            b.HasKey(z => new { z.Prefix, z.Year });
            b.Property(z => z.Prefix).HasMaxLength(8);
            b.Property(z => z.Year).ValueGeneratedNever();
        });

        modelBuilder.Entity<Source>(b =>
        {
            b.Property(q => q.EntityType).HasMaxLength(128);
            b.Property(q => q.EntityId).HasMaxLength(64);
            b.Property(q => q.Title).HasMaxLength(300).IsRequired();
            b.Property(q => q.Url).HasMaxLength(2048);
            b.Property(q => q.TargetType).HasMaxLength(128);
            b.Property(q => q.TargetId).HasMaxLength(64);
            b.Property(q => q.FileNameSaved).HasMaxLength(128);
            b.Property(q => q.OriginalName).HasMaxLength(260);
            b.Property(q => q.ContentType).HasMaxLength(100);
            b.HasIndex(q => new { q.EntityType, q.EntityId });
        });

        modelBuilder.Entity<Tag>(b =>
        {
            b.Property(t => t.Name).HasMaxLength(60).IsRequired();
            b.Property(t => t.Colour).HasMaxLength(32);
            b.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<DocTemplate>(b =>
        {
            b.Property(v => v.Name).HasMaxLength(120).IsRequired();
            b.Property(v => v.Description).HasMaxLength(500);
            b.Property(v => v.DefaultReason).HasMaxLength(2000);
            b.Property(v => v.DefaultFaction).HasMaxLength(200);
            b.Property(v => v.DefaultReceivedInformation).HasMaxLength(4000);
            // no unique: soft-delete safe
            b.HasIndex(v => v.Name);
            b.HasIndex(v => v.IsActive);
        });

        modelBuilder.Entity<CustomFieldDefinition>(b =>
        {
            b.Property(d => d.EntityType).HasMaxLength(128).IsRequired();
            b.Property(d => d.Name).HasMaxLength(120).IsRequired();
            b.Property(d => d.Options).HasMaxLength(2000);
            // no unique: soft-delete safe
            b.HasIndex(d => new { d.EntityType, d.IsActive });
        });

        modelBuilder.Entity<CustomFieldValue>(b =>
        {
            b.Property(w => w.CustomFieldDefinitionId).HasMaxLength(64).IsRequired();
            b.Property(w => w.EntityType).HasMaxLength(128).IsRequired();
            b.Property(w => w.EntityId).HasMaxLength(64).IsRequired();
            b.Property(w => w.Value).HasColumnType("longtext");
            b.HasIndex(w => new { w.EntityType, w.EntityId });
            // one value per field per record
            b.HasIndex(w => new { w.CustomFieldDefinitionId, w.EntityType, w.EntityId }).IsUnique();
        });

        modelBuilder.Entity<Document>(b =>
        {
            b.Property(d => d.Title).HasMaxLength(300).IsRequired();
            b.Property(d => d.Category).HasMaxLength(120);
            b.Property(d => d.ContentHtml).HasColumnType("longtext");
            b.Property(d => d.OwnerTaskforceId).HasMaxLength(40);
            b.HasIndex(d => d.Title);
            b.HasIndex(d => d.Category);
            b.HasIndex(d => d.IsClassified);
            b.HasIndex(d => d.OwnerTaskforceId);
        });

        modelBuilder.Entity<DocumentTemplate>(b =>
        {
            b.Property(v => v.Name).HasMaxLength(120).IsRequired();
            b.Property(v => v.Description).HasMaxLength(500);
            b.Property(v => v.Category).HasMaxLength(120);
            b.Property(v => v.ContentHtml).HasColumnType("longtext");
            // no unique: soft-delete safe
            b.HasIndex(v => v.Name);
            b.HasIndex(v => v.IsActive);
        });

        modelBuilder.Entity<RecencyThreshold>(b =>
        {
            // one row per type
            b.HasKey(s => s.RecordsType);
            b.Property(s => s.RecordsType).HasMaxLength(64);
        });

        // admin-tunable threat-score config (single JSON row)
        modelBuilder.Entity<ThreatScoreConfig>(b =>
        {
            b.HasKey(k => k.Id);
            b.Property(k => k.Id).HasMaxLength(32);
            b.Property(k => k.Json).HasColumnType("longtext");
        });

        modelBuilder.Entity<SituationReport>(b =>
        {
            b.Property(l => l.Title).HasMaxLength(200).IsRequired();
            b.Property(l => l.SnapshotJson).HasColumnType("longtext");
            b.HasIndex(l => new { l.Year, l.Month });
        });

        modelBuilder.Entity<Followup>(b =>
        {
            b.Property(w => w.EntityType).HasMaxLength(128);
            b.Property(w => w.EntityId).HasMaxLength(64);
            b.Property(w => w.Note).HasMaxLength(500);
            b.Property(w => w.ResponsibleAgentId).HasMaxLength(64);
            b.Property(w => w.DoneById).HasMaxLength(64);
            b.HasIndex(w => new { w.EntityType, w.EntityId });
            b.HasIndex(w => new { w.DueAt, w.Done, w.NotifiedAt });
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(w => w.ResponsibleAgentId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TagMapping>(b =>
        {
            b.Property(z => z.EntityType).HasMaxLength(128);
            b.Property(z => z.EntityId).HasMaxLength(64);
            b.HasOne(z => z.Tag).WithMany().HasForeignKey(z => z.TagId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(z => new { z.EntityType, z.EntityId });
            // no duplicate tags
            b.HasIndex(z => new { z.TagId, z.EntityType, z.EntityId }).IsUnique();
        });

        modelBuilder.Entity<Comment>(b =>
        {
            b.Property(k => k.EntityType).HasMaxLength(128);
            b.Property(k => k.EntityId).HasMaxLength(64);
            b.Property(k => k.AuthorName).HasMaxLength(128);
            b.HasIndex(k => new { k.EntityType, k.EntityId });
        });

        modelBuilder.Entity<Link>(b =>
        {
            b.Property(v => v.SourceType).HasMaxLength(128);
            b.Property(v => v.SourceId).HasMaxLength(64);
            b.Property(v => v.TargetType).HasMaxLength(128);
            b.Property(v => v.TargetId).HasMaxLength(64);
            b.Property(v => v.Label).HasMaxLength(200);
            // both directions quickly findable
            b.HasIndex(v => new { v.SourceType, v.SourceId });
            b.HasIndex(v => new { v.TargetType, v.TargetId });
        });

        modelBuilder.Entity<PersonRelation>(b =>
        {
            b.Property(x => x.Note).HasMaxLength(500);
            // restrict: avoid cascade
            b.HasOne(x => x.PersonA).WithMany()
                .HasForeignKey(x => x.PersonAId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PersonB).WithMany()
                .HasForeignKey(x => x.PersonBId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.PersonAId);
            b.HasIndex(x => x.PersonBId);
        });

        modelBuilder.Entity<SavedSearch>(b =>
        {
            b.Property(g => g.AgentId).HasMaxLength(64);
            b.Property(g => g.Name).HasMaxLength(120).IsRequired();
            b.HasOne<Agent>().WithMany().HasForeignKey(g => g.AgentId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(g => g.AgentId);
        });

        modelBuilder.Entity<Faction>(b =>
        {
            b.Property(f => f.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(f => f.Name).HasMaxLength(200).IsRequired();
            b.Property(f => f.Kind).HasMaxLength(100);
            b.Property(f => f.Radio).HasMaxLength(100);
            b.Property(f => f.Darkchat).HasMaxLength(100);
            b.Property(f => f.IssuingTimes).HasMaxLength(300);
            b.Property(f => f.Estate).HasMaxLength(500);
            b.Property(f => f.RecognitionColor).HasMaxLength(32);
            b.Property(f => f.Targets).HasMaxLength(2000);
            b.Property(f => f.Description).HasMaxLength(2000);
            b.Property(f => f.ThreatDetailJson).HasColumnType("longtext");
            b.HasIndex(f => f.CaseNumber).IsUnique();
            b.HasIndex(f => f.Name);
            b.HasIndex(f => f.IsClassified);

            b.HasMany(f => f.Ranks).WithOne(r => r.Faction!)
                .HasForeignKey(r => r.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.WeaponStock).WithOne(w => w.Faction!)
                .HasForeignKey(w => w.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Inventory).WithOne(l => l.Faction!)
                .HasForeignKey(l => l.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.DrugRoutes).WithOne(d => d.Faction!)
                .HasForeignKey(d => d.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Members).WithOne(m => m.Faction!)
                .HasForeignKey(m => m.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Agents).WithOne(a => a.Faction!)
                .HasForeignKey(a => a.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Photos).WithOne(x => x.Faction!)
                .HasForeignKey(x => x.FactionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Activities).WithOne(a => a.Faction!)
                .HasForeignKey(a => a.FactionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FactionActivity>(b =>
        {
            b.Property(a => a.Title).HasMaxLength(200).IsRequired();
            b.Property(a => a.Kind).HasMaxLength(100);
            b.Property(a => a.Location).HasMaxLength(200);
            b.Property(a => a.Description).HasMaxLength(4000);
            b.HasIndex(a => a.FactionId);
            b.HasIndex(a => a.Kind);
        });

        modelBuilder.Entity<FactionPhoto>(b =>
        {
            b.Property(f => f.FileNameSaved).HasMaxLength(128);
            b.Property(f => f.OriginalName).HasMaxLength(260);
            b.Property(f => f.ContentType).HasMaxLength(100);
            b.Property(f => f.CreatedById).HasMaxLength(64);
            b.HasIndex(f => f.FactionId);
        });

        modelBuilder.Entity<FactionAgent>(b =>
        {
            // Restrict FK to identity Agent (no cascade from the user table)
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.FactionId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        modelBuilder.Entity<FactionRank>(b =>
        {
            b.Property(r => r.Designation).HasMaxLength(100).IsRequired();
        });
        modelBuilder.Entity<FactionWeaponStock>(b =>
        {
            b.Property(w => w.Designation).HasMaxLength(200).IsRequired();
            b.Property(w => w.Quantity).HasMaxLength(50);
        });
        modelBuilder.Entity<FactionInventory>(b =>
        {
            b.Property(l => l.Designation).HasMaxLength(200).IsRequired();
            b.Property(l => l.Quantity).HasMaxLength(50);
        });
        modelBuilder.Entity<FactionDrugRoute>(b =>
        {
            b.Property(d => d.Designation).HasMaxLength(200).IsRequired();
            b.Property(d => d.Note).HasMaxLength(200);
        });

        modelBuilder.Entity<FactionMember>(b =>
        {
            b.Property(m => m.Rank).HasMaxLength(100);
            // restrict: avoid cascade
            b.HasOne(m => m.Person).WithMany()
                .HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Restrict);
            // unique check in service
            b.HasIndex(m => new { m.FactionId, m.PersonId });
            b.HasIndex(m => m.PersonId);
        });

        modelBuilder.Entity<PersonGroup>(b =>
        {
            b.Property(g => g.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(g => g.Name).HasMaxLength(200).IsRequired();
            b.Property(g => g.Description).HasMaxLength(2000);
            b.Property(g => g.Targets).HasMaxLength(2000);
            b.HasIndex(g => g.CaseNumber).IsUnique();
            b.HasIndex(g => g.Name);
            b.HasIndex(g => g.IsClassified);

            b.HasMany(g => g.Members).WithOne(m => m.PersonGroup!)
                .HasForeignKey(m => m.PersonGroupId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(g => g.Agents).WithOne(a => a.PersonGroup!)
                .HasForeignKey(a => a.PersonGroupId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PersonGroupMember>(b =>
        {
            b.Property(m => m.Role).HasMaxLength(100);
            // restrict: avoid cascade
            b.HasOne(m => m.Person).WithMany()
                .HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Restrict);
            // no unique: soft-delete safe
            b.HasIndex(m => new { m.PersonGroupId, m.PersonId });
            b.HasIndex(m => m.PersonId);
        });

        modelBuilder.Entity<PersonGroupAgent>(b =>
        {
            // Restrict FK to identity Agent (no cascade from the user table)
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.PersonGroupId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        modelBuilder.Entity<Party>(b =>
        {
            b.Property(p => p.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Description).HasMaxLength(2000);
            b.Property(p => p.Targets).HasMaxLength(2000);
            b.Property(p => p.Remarks).HasMaxLength(2000);
            b.HasIndex(p => p.CaseNumber).IsUnique();
            b.HasIndex(p => p.Name);
            b.HasIndex(p => p.IsClassified);

            b.HasMany(p => p.Members).WithOne(m => m.Party!)
                .HasForeignKey(m => m.PartyId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(p => p.Agents).WithOne(a => a.Party!)
                .HasForeignKey(a => a.PartyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PartyMember>(b =>
        {
            b.Property(m => m.Role).HasMaxLength(100);
            // restrict: avoid cascade
            b.HasOne(m => m.Person).WithMany()
                .HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Restrict);
            // no unique: soft-delete safe
            b.HasIndex(m => new { m.PartyId, m.PersonId });
            b.HasIndex(m => m.PersonId);
        });

        modelBuilder.Entity<PartyAgent>(b =>
        {
            // Restrict FK to identity Agent (no cascade from the user table)
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.PartyId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        modelBuilder.Entity<Operation>(b =>
        {
            b.Property(o => o.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(o => o.Title).HasMaxLength(200).IsRequired();
            b.Property(o => o.Type).HasMaxLength(100);
            b.Property(o => o.Location).HasMaxLength(200);
            b.Property(o => o.Expiry).HasMaxLength(4000);
            b.Property(o => o.Result).HasMaxLength(4000);
            b.Property(o => o.Remarks).HasMaxLength(2000);
            b.HasIndex(o => o.CaseNumber).IsUnique();
            b.HasIndex(o => o.Title);
            b.HasIndex(o => o.Status);
            b.HasIndex(o => o.IsClassified);

            b.HasMany(o => o.Agents).WithOne(a => a.Operation!)
                .HasForeignKey(a => a.OperationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OperationAgent>(b =>
        {
            // Restrict FK to identity Agent (no cascade from the user table)
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.OperationId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        modelBuilder.Entity<Case>(b =>
        {
            b.Property(v => v.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(v => v.Title).HasMaxLength(200).IsRequired();
            b.Property(v => v.Type).HasMaxLength(100);
            b.Property(v => v.Description).HasMaxLength(4000);
            b.Property(v => v.Summary).HasMaxLength(4000);
            b.Property(v => v.ClosingNote).HasMaxLength(4000);
            b.HasIndex(v => v.CaseNumber).IsUnique();
            b.HasIndex(v => v.Title);
            b.HasIndex(v => v.Status);
            b.HasIndex(v => v.IsClassified);

            b.HasMany(v => v.Agents).WithOne(a => a.Case!)
                .HasForeignKey(a => a.CaseId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CaseAgent>(b =>
        {
            // Restrict FK to identity Agent (no cascade from the user table)
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.CaseId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        modelBuilder.Entity<Job>(b =>
        {
            b.Property(a => a.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(a => a.Title).HasMaxLength(200).IsRequired();
            b.Property(a => a.Description).HasMaxLength(4000);
            b.HasIndex(a => a.CaseNumber).IsUnique();
            b.HasIndex(a => a.Title);
            b.HasIndex(a => a.Status);
            b.HasIndex(a => a.DueDate);
            b.HasIndex(a => a.IsRestricted);

            b.HasMany(a => a.Assignments).WithOne(z => z.Job!)
                .HasForeignKey(z => z.JobId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobAssignment>(b =>
        {
            // Restrict FK to identity Agent (no cascade from the user table)
            b.HasOne(z => z.Agent).WithMany()
                .HasForeignKey(z => z.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(z => new { z.JobId, z.AgentId }).IsUnique();
            b.HasIndex(z => z.AgentId);
        });

        modelBuilder.Entity<Appointment>(b =>
        {
            b.Property(t => t.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(t => t.Title).HasMaxLength(200).IsRequired();
            b.Property(t => t.Location).HasMaxLength(200);
            b.Property(t => t.Description).HasMaxLength(4000);
            b.HasIndex(t => t.CaseNumber).IsUnique();
            b.HasIndex(t => t.Title);
            b.HasIndex(t => t.Category);
            b.HasIndex(t => t.Status);
            b.HasIndex(t => t.Start);
            b.HasIndex(t => t.Visibility);

            b.HasMany(t => t.Participant).WithOne(z => z.Appointment!)
                .HasForeignKey(z => z.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppointmentAssignment>(b =>
        {
            // Restrict FK to identity Agent (no cascade from the user table)
            b.HasOne(z => z.Agent).WithMany()
                .HasForeignKey(z => z.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(z => new { z.AppointmentId, z.AgentId }).IsUnique();
            b.HasIndex(z => z.AgentId);
        });

        // ---- announcements ----
        modelBuilder.Entity<Announcement>(b =>
        {
            b.Property(a => a.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(a => a.Title).HasMaxLength(200).IsRequired();
            b.Property(a => a.TargetId).HasMaxLength(64);
            b.HasIndex(a => a.CaseNumber).IsUnique();
            b.HasIndex(a => new { a.Important, a.CreatedAt });
            b.HasIndex(a => a.Audience);

            b.HasMany(a => a.Acknowledgments).WithOne(q => q.Announcement!)
                .HasForeignKey(q => q.AnnouncementId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnnouncementAcknowledgment>(b =>
        {
            // Restrict FK to identity Agent (no cascade from the user table)
            b.HasOne(q => q.Agent).WithMany()
                .HasForeignKey(q => q.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(q => new { q.AnnouncementId, q.AgentId }).IsUnique();
            b.HasIndex(q => new { q.AgentId, q.AcknowledgedAt });
        });

        modelBuilder.Entity<Request>(b =>
        {
            b.Property(a => a.TargetType).HasMaxLength(128);
            b.Property(a => a.TargetId).HasMaxLength(64);
            b.Property(a => a.TargetDesignation).HasMaxLength(256);
            b.Property(a => a.Justification).HasMaxLength(2000);
            b.Property(a => a.DecisionNote).HasMaxLength(2000);
            b.Property(a => a.RequesterName).HasMaxLength(100);
            b.Property(a => a.DeciderName).HasMaxLength(100);
            b.Property(a => a.CreatedById).HasMaxLength(64);
            b.HasIndex(a => new { a.Type, a.Status });
            b.HasIndex(a => new { a.TargetType, a.TargetId });
            b.HasIndex(a => a.CreatedById);
        });

        modelBuilder.Entity<Notification>(b =>
        {
            b.Property(n => n.RecipientId).HasMaxLength(64);
            b.Property(n => n.Title).HasMaxLength(300);
            b.Property(n => n.Href).HasMaxLength(512);
            b.Property(n => n.CreatedById).HasMaxLength(64);
            b.HasIndex(n => new { n.RecipientId, n.ReadAt });
        });

        modelBuilder.Entity<WatchlistEntry>(b =>
        {
            b.Property(w => w.AgentId).HasMaxLength(64);
            b.Property(w => w.EntityType).HasMaxLength(128);
            b.Property(w => w.EntityId).HasMaxLength(64);
            b.Property(w => w.CreatedById).HasMaxLength(64);
            // Restrict FK to identity Agent (no cascade from the user table)
            b.HasOne<Agent>().WithMany().HasForeignKey(w => w.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(w => new { w.EntityType, w.EntityId });
            b.HasIndex(w => new { w.AgentId, w.IsDeleted });
        });

        modelBuilder.Entity<Taskforce>(b =>
        {
            b.Property(t => t.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(t => t.Name).HasMaxLength(200).IsRequired();
            b.Property(t => t.Purpose).HasMaxLength(4000);
            b.Property(t => t.Remarks).HasMaxLength(2000);
            b.HasIndex(t => t.CaseNumber).IsUnique();
            b.HasIndex(t => t.Name);
            b.HasIndex(t => t.Status);
            b.HasIndex(t => t.IsClassified);

            b.HasMany(t => t.Agents).WithOne(a => a.Taskforce!)
                .HasForeignKey(a => a.TaskforceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskforceAgent>(b =>
        {
            // Restrict FK to identity Agent (no cascade from the user table)
            b.HasOne(a => a.Agent).WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => new { a.TaskforceId, a.AgentId }).IsUnique();
            b.HasIndex(a => a.AgentId);
        });

        modelBuilder.Entity<TaskforceMessage>(b =>
        {
            b.Property(n => n.Text).HasMaxLength(4000).IsRequired();
            b.Property(n => n.AuthorName).HasMaxLength(128);
            b.HasIndex(n => new { n.TaskforceId, n.CreatedAt });
            b.HasOne(n => n.Taskforce).WithMany()
                .HasForeignKey(n => n.TaskforceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentRankHistory>(b =>
        {
            b.Property(v => v.ActorName).HasMaxLength(128);
            b.Property(v => v.Reason).HasMaxLength(500);
            b.HasIndex(v => new { v.AgentId, v.Timestamp });
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(v => v.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgentNote>(b =>
        {
            b.Property(v => v.Text).HasMaxLength(4000).IsRequired();
            b.Property(v => v.AuthorName).HasMaxLength(128);
            b.HasIndex(v => new { v.AgentId, v.Kind });
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(v => v.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgentPromotionRequest>(b =>
        {
            b.Property(a => a.Justification).HasMaxLength(2000);
            b.Property(a => a.RequesterName).HasMaxLength(128);
            b.Property(a => a.DeciderName).HasMaxLength(128);
            b.Property(a => a.DecisionNote).HasMaxLength(2000);
            b.HasIndex(a => new { a.AgentId, a.Status });
            b.HasIndex(a => a.Status);
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TrainingModule>(b =>
        {
            b.Property(m => m.Name).HasMaxLength(160).IsRequired();
            b.Property(m => m.Description).HasMaxLength(2000);
            b.HasIndex(m => m.Name);
        });

        modelBuilder.Entity<AgentModuleCompletion>(b =>
        {
            b.Property(c => c.CompleterName).HasMaxLength(128);
            b.Property(c => c.Note).HasMaxLength(2000);
            b.HasIndex(c => new { c.AgentId, c.ModuleId });
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(c => c.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(c => c.Module).WithMany()
                .HasForeignKey(c => c.ModuleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SystemSetting>(b =>
        {
            b.HasKey(e => e.Key);
            b.Property(e => e.Key).HasMaxLength(128);
        });

        modelBuilder.Entity<Law>(b =>
        {
            b.Property(g => g.LawBook).HasMaxLength(128).IsRequired();
            b.Property(g => g.Paragraph).HasMaxLength(32).IsRequired();
            b.Property(g => g.Title).HasMaxLength(256).IsRequired();
            b.Property(g => g.Sentence).HasMaxLength(512);
            b.HasIndex(g => g.LawBook);
            b.HasIndex(g => g.Title);
        });

        modelBuilder.Entity<LibraryFile>(b =>
        {
            b.Property(d => d.Title).HasMaxLength(300).IsRequired();
            b.Property(d => d.Category).HasMaxLength(120);
            b.Property(d => d.OriginalName).HasMaxLength(260).IsRequired();
            b.Property(d => d.FileNameSaved).HasMaxLength(128).IsRequired();
            b.Property(d => d.ContentType).HasMaxLength(100).IsRequired();
            b.HasIndex(d => d.Title);
            b.HasIndex(d => d.Category);
            b.HasIndex(d => d.IsClassified);
        });

        modelBuilder.Entity<PartnerShare>(b =>
        {
            b.Property(s => s.EntityType).HasMaxLength(128).IsRequired();
            b.Property(s => s.EntityId).HasMaxLength(64).IsRequired();
            b.Property(s => s.PartnerAgentId).HasMaxLength(64);
            b.HasIndex(s => new { s.EntityType, s.EntityId });
            b.HasIndex(s => new { s.Agency, s.EntityType, s.EntityId });
        });

        modelBuilder.Entity<DocumentAccessExclusion>(b =>
        {
            b.Property(x => x.DocumentId).HasMaxLength(64).IsRequired();
            b.Property(x => x.AgentId).HasMaxLength(64).IsRequired();
            // no unique: soft-delete safe; dedupe in service among active rows
            b.HasIndex(x => new { x.DocumentId, x.AgentId });
            b.HasIndex(x => x.AgentId);
            b.HasOne<Document>().WithMany()
                .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AgentInvite>(b =>
        {
            b.Property(i => i.Token).HasMaxLength(64).IsRequired();
            b.Property(i => i.Label).HasMaxLength(200);
            b.Property(i => i.CreatedByName).HasMaxLength(128);
            b.Property(i => i.UsedByUserId).HasMaxLength(64);
            b.HasIndex(i => i.Token).IsUnique();
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(i => i.UsedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Bewerbung>(b =>
        {
            b.Property(v => v.CaseNumber).HasMaxLength(32).IsRequired();
            b.Property(v => v.AcademicDegree).HasMaxLength(64);
            b.Property(v => v.Name).HasMaxLength(200).IsRequired();
            b.Property(v => v.Employer).HasMaxLength(200);
            b.Property(v => v.PriorExperience).HasColumnType("longtext");
            b.Property(v => v.CoverLetter).HasColumnType("longtext");
            b.Property(v => v.AttachmentFileNameSaved).HasMaxLength(128);
            b.Property(v => v.AttachmentOriginalName).HasMaxLength(260);
            b.Property(v => v.AttachmentContentType).HasMaxLength(100);
            b.Property(v => v.AssignedAgentName).HasMaxLength(128);
            b.Property(v => v.LinkedPersonId).HasMaxLength(64);
            b.Property(v => v.DecisionNote).HasColumnType("longtext");
            b.Property(v => v.DecidedByName).HasMaxLength(128);
            b.HasIndex(v => v.CaseNumber).IsUnique();
            b.HasIndex(v => v.Status);
            b.HasIndex(v => new { v.ApplicantUserId, v.Status });
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(v => v.ApplicantUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(v => v.AssignedAgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Person>().WithMany()
                .HasForeignKey(v => v.LinkedPersonId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Bewerbungssperre>(b =>
        {
            b.Property(s => s.AgentId).HasMaxLength(64).IsRequired();
            b.Property(s => s.DiscordId).HasMaxLength(64);
            b.Property(s => s.ApplicantName).HasMaxLength(200);
            b.Property(s => s.BewerbungId).HasMaxLength(64);
            b.Property(s => s.Reason).HasColumnType("longtext");
            b.Property(s => s.CreatedByName).HasMaxLength(128);
            b.HasIndex(s => s.AgentId);
            b.HasOne<Agent>().WithMany()
                .HasForeignKey(s => s.AgentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Bewerbung>().WithMany()
                .HasForeignKey(s => s.BewerbungId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BewerbungMessage>(b =>
        {
            b.Property(m => m.Text).HasColumnType("longtext");
            b.Property(m => m.AuthorName).HasMaxLength(128);
            b.HasIndex(m => new { m.BewerbungId, m.Audience });
            b.HasIndex(m => new { m.BewerbungId, m.CreatedAt });
            b.HasOne(m => m.Bewerbung).WithMany()
                .HasForeignKey(m => m.BewerbungId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BewerbungTest>(b =>
        {
            b.Property(t => t.Title).HasMaxLength(200).IsRequired();
            b.Property(t => t.Description).HasColumnType("longtext");
            b.HasIndex(t => t.IsActive);
        });

        modelBuilder.Entity<BewerbungTestQuestion>(b =>
        {
            b.Property(q => q.Prompt).HasColumnType("longtext");
            b.Property(q => q.Points).HasDefaultValue(1);
            b.Property(q => q.Keywords).HasColumnType("longtext");
            b.HasIndex(q => new { q.TestId, q.Sorting });
            b.HasOne(q => q.Test).WithMany()
                .HasForeignKey(q => q.TestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BewerbungTestOption>(b =>
        {
            b.Property(o => o.Label).HasMaxLength(500).IsRequired();
            b.HasIndex(o => new { o.QuestionId, o.Sorting });
            b.HasOne(o => o.Question).WithMany()
                .HasForeignKey(o => o.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BewerbungTestAssignment>(b =>
        {
            b.Property(a => a.AssignedByName).HasMaxLength(128);
            b.HasIndex(a => a.BewerbungId).IsUnique();
            b.HasOne(a => a.Bewerbung).WithMany()
                .HasForeignKey(a => a.BewerbungId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(a => a.Test).WithMany()
                .HasForeignKey(a => a.TestId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BewerbungTestAnswer>(b =>
        {
            b.Property(a => a.SelectedOptionId).HasMaxLength(64);
            b.Property(a => a.FreeTextAnswer).HasColumnType("longtext");
            b.HasIndex(a => new { a.AssignmentId, a.QuestionId });
            b.HasOne(a => a.Assignment).WithMany()
                .HasForeignKey(a => a.AssignmentId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(a => a.Question).WithMany()
                .HasForeignKey(a => a.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        // global soft-delete filter
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ISoftDelete).IsAssignableFrom(t.ClrType))
                     .ToList())
        {
            SetSoftDeleteFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(null, new object[] { modelBuilder });
        }
    }

    private static readonly MethodInfo SetSoftDeleteFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(SetSoftDeleteFilter),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ISoftDelete
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }
}
