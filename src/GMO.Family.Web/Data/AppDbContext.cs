using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GMO.Family.Web.Data;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<FamilyTree>(e =>
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
        modelBuilder.Entity<FamilyMember>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FamilyTreeId);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.FamilyTreeId, x.UserId }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.NickName).HasMaxLength(100);
            e.Property(x => x.UserId).HasMaxLength(450);
            e.HasOne(x => x.FamilyTree)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.FamilyTreeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<FamilyMemberRelationship>(e =>
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
        modelBuilder.Entity<UserProfile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.PhotoUrl).HasMaxLength(500);
            e.Property(x => x.TreeViewOrientation).HasConversion<int>();
            e.Property(x => x.TreePathMode).HasConversion<int>();
        });
    }
}