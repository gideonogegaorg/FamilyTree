using System.ComponentModel.DataAnnotations;

using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.Models;

public sealed class ShareManageViewModel
{
    public long TreeId { get; set; }
    public string TreeName { get; set; } = string.Empty;
    public IReadOnlyList<ShareCollaboratorViewModel> Collaborators { get; set; } = [];
    public IReadOnlyList<ShareInviteViewModel> PendingInvites { get; set; } = [];
    public string? CreatedLinkUrl { get; set; }
    public string? StatusMessage { get; set; }
    public CreateLinkInviteInput CreateLink { get; set; } = new();
    public CreateEmailInviteInput CreateEmail { get; set; } = new();
}

public sealed class ShareCollaboratorViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public TreeShareRole Role { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
}

public sealed class ShareInviteViewModel
{
    public long Id { get; set; }
    public bool IsLink { get; set; }
    public string? Email { get; set; }
    public TreeShareRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string AcceptUrl { get; set; } = string.Empty;
}

public sealed class CreateLinkInviteInput
{
    public TreeShareRole Role { get; set; } = TreeShareRole.Readonly;
    public int? ExpiresInDays { get; set; } = 30;
}

public sealed class CreateEmailInviteInput
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public TreeShareRole Role { get; set; } = TreeShareRole.Readonly;
    public int? ExpiresInDays { get; set; } = 14;
}