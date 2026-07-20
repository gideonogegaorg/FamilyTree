namespace GMO.FamilyTree.Web.Models;

public sealed class LandingPageViewModel
{
    public string DemoNodesJson { get; init; } = "[]";
    public string DemoEdgesJson { get; init; } = "[]";
}

public sealed class LandingDemoTreeDto
{
    public List<LandingDemoNodeDto>? Nodes { get; init; }
    public List<LandingDemoEdgeDto>? Edges { get; init; }
}

public sealed class LandingDemoNodeDto
{
    public long Id { get; init; }
    public string Label { get; init; } = "";
    public bool IsMe { get; init; }
    public bool IsMale { get; init; }
    public string? Dob { get; init; }
    public int Row { get; init; }
    public double VisualRank { get; init; }
    public List<long> ParentIds { get; init; } = [];
    public List<long> ChildIds { get; init; } = [];
    public List<long> PartnerIds { get; init; } = [];
    public int? BirthOrder { get; init; }
    public string? PhotoUrl { get; init; }
}

public sealed class LandingDemoEdgeDto
{
    public long Id { get; init; }
    public long Source { get; init; }
    public long Target { get; init; }
    public string Type { get; init; } = "";
}