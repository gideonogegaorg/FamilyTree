using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GMO.FamilyTree.Web.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<FamilyTree> FamilyTrees => Set<FamilyTree>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<FamilyMemberRelationship> FamilyMemberRelationships => Set<FamilyMemberRelationship>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<FamilyTreeAccess> FamilyTreeAccesses => Set<FamilyTreeAccess>();
    public DbSet<FamilyTreeInvite> FamilyTreeInvites => Set<FamilyTreeInvite>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<FamilyTree>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Uid).IsUnique();
            e.Property(x => x.Name).HasMaxLength(500);
            e.Property(x => x.OwnerId).HasMaxLength(450).IsRequired();
            e.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<FamilyMember>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyTreeId);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.FamilyTreeId, x.UserId }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.NickName).HasMaxLength(100);
            e.Property(x => x.UserId).HasMaxLength(450);
            e.ToTable(t => t.HasCheckConstraint(
                "CK_FamilyMember_DOD_After_DOB",
                "\"DOD\" IS NULL OR \"DOB\" IS NULL OR \"DOD\" >= \"DOB\""));
            e.HasOne(x => x.FamilyTree)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.FamilyTreeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<FamilyMemberRelationship>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyTreeId);
            e.HasIndex(x => new { x.FromMemberId, x.ToMemberId, x.RelationshipType }).IsUnique();
            e.Property(x => x.RelationshipType).HasConversion<int>();
            e.HasOne(x => x.FamilyTree)
                .WithMany()
                .HasForeignKey(x => x.FamilyTreeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.FromMember)
                .WithMany(x => x.OutgoingRelationships)
                .HasForeignKey(x => x.FromMemberId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ToMember)
                .WithMany(x => x.IncomingRelationships)
                .HasForeignKey(x => x.ToMemberId)
                .OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_FamilyMemberRelationship_FromNotTo", "\"FromMemberId\" != \"ToMemberId\""));
        });
        builder.Entity<UserProfile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.PhotoUrl).HasMaxLength(500);
            e.Property(x => x.TreeViewOrientation).HasConversion<int>();
            e.Property(x => x.LineageMode).HasConversion<int>();
        });
        builder.Entity<FamilyTreeAccess>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FamilyTreeId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.Property(x => x.UserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.GrantedByUserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.Role).HasConversion<int>();
            e.HasOne(x => x.FamilyTree)
                .WithMany(x => x.AccessGrants)
                .HasForeignKey(x => x.FamilyTreeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<FamilyTreeInvite>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.FamilyTreeId);
            e.Property(x => x.Token).HasMaxLength(64).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
            e.Property(x => x.AcceptedByUserId).HasMaxLength(450);
            e.Property(x => x.Role).HasConversion<int>();
            e.HasOne(x => x.FamilyTree)
                .WithMany(x => x.Invites)
                .HasForeignKey(x => x.FamilyTreeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}