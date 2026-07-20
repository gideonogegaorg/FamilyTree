using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Models;

namespace GMO.FamilyTree.Web.Services;

/// <summary>
/// Computes the complete ranking system for family tree layout, including both row (generation depth) and visual rank calculations.
/// 
/// The ranking system consists of two related concepts:
/// <list type="bullet">
/// <item><description><b>Row</b>: Integer generation depth (0 = roots, 1 = children, 2 = grandchildren, etc.)</description></item>
/// <item><description><b>Visual Rank</b>: Extended row system with half-ranks (0.5, 1.5, etc.) for partners of multi-partner primary members</description></item>
/// </list>
/// 
/// <para>
/// <b>Row Calculation</b>: Determines generation depth based on parent relationships. Partners without parents inherit the row of their partner.
/// </para>
/// 
/// <para>
/// <b>Visual Rank Calculation</b>: Extends rows with half-ranks for secondary partners in multi-partner relationships.
/// The primary gender is determined by LineageMode (male for Paternal, female for Maternal).
/// Partners of multi-partner primary members who have no parents get rank = primary's rank + 0.5.
/// </para>
/// 
/// Used by HomeController to build the nodes JSON for the family tree visualization. Extracted for unit testing.
/// </summary>
public static class TreeLayoutRanking
{
    /// <summary>
    /// Calculates generation depth (row) for each family member based on parent-child relationships.
    /// 
    /// Algorithm:
    /// 1. Root members (no parents) get Row 0
    /// 2. Children get Row = 1 + max(parent rows)  
    /// 3. Partners without parents inherit their partner's row (alignment)
    /// 4. Changes propagate through the tree to maintain consistency
    /// 
    /// This provides the foundation for visual rank calculation.
    /// </summary>
    public static Dictionary<long, int> ComputeRowByMember(IReadOnlyList<FamilyMemberCardViewModel> cards)
    {
        var rowById = new Dictionary<long, int>();

        foreach (var c in cards)
            if (c.ParentIds.Count == 0)
                rowById[c.Id] = 0;

        PropagateChildRows(cards, rowById, onlyWhenMissing: true);
        AlignPartnerRows(cards, rowById);
        PropagateChildRows(cards, rowById, onlyWhenMissing: false);

        foreach (var c in cards.Where(c => !rowById.ContainsKey(c.Id)))
            rowById[c.Id] = 0;

        return rowById;
    }

    /// <summary>
    /// Extends row calculations with half-ranks for secondary partners in multi-partner relationships.
    /// 
    /// Algorithm:
    /// 1. Start with row values as base visual ranks (convert to double)
    /// 2. Determine primary gender based on LineageMode (male for Paternal, female for Maternal)
    /// 3. For each multi-partner primary member:
    ///    - Find partners who are dominated by the primary (no parents of their own; dominant = bloodline-connected in same-sex case)
    ///    - Assign them rank = primary's rank + 0.5 (half-rank positioning)
    /// 4. Bloodline domination ensures family tree topology anchors the visual structure
    /// 
    /// This creates the precise positioning needed for the JavaScript layout engine.
    /// </summary>
    public static Dictionary<long, double> ComputeVisualRank(
        IReadOnlyList<FamilyMemberCardViewModel> cards,
        Dictionary<long, int> rowById,
        LineageMode pathMode)
    {
        var cardById = cards.ToDictionary(c => c.Id);
        var rankById = rowById.ToDictionary(kv => kv.Key, kv => (double)kv.Value);
        AssignHalfRanksForMultiPartnerNodes(cards, cardById, rankById, pathMode);
        return rankById;
    }

    private static void PropagateChildRows(
        IReadOnlyList<FamilyMemberCardViewModel> cards,
        Dictionary<long, int> rowById,
        bool onlyWhenMissing)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var c in cards)
            {
                if (c.ParentIds.Count == 0) continue;
                if (onlyWhenMissing && rowById.ContainsKey(c.Id)) continue;

                var parentRows = c.ParentIds.Where(pid => rowById.ContainsKey(pid)).Select(pid => rowById[pid]).ToList();
                if (parentRows.Count != c.ParentIds.Count) continue;

                var newRow = 1 + parentRows.Max();
                if (onlyWhenMissing)
                {
                    rowById[c.Id] = newRow;
                    changed = true;
                    continue;
                }

                if (newRow > rowById.GetValueOrDefault(c.Id, 0))
                {
                    rowById[c.Id] = newRow;
                    changed = true;
                }
            }
        }
    }

    private static void AlignPartnerRows(IReadOnlyList<FamilyMemberCardViewModel> cards, Dictionary<long, int> rowById)
    {
        foreach (var c in cards)
        {
            if (c.ParentIds.Count > 0) continue;
            foreach (var pid in c.PartnerIds)
            {
                if (rowById.TryGetValue(pid, out var pr) && pr > rowById.GetValueOrDefault(c.Id, 0))
                {
                    rowById[c.Id] = pr;
                    break;
                }
            }
        }
    }

    private static void AssignHalfRanksForMultiPartnerNodes(
        IReadOnlyList<FamilyMemberCardViewModel> cards,
        Dictionary<long, FamilyMemberCardViewModel> cardById,
        Dictionary<long, double> rankById,
        LineageMode pathMode)
    {
        bool isPrimary(FamilyMemberCardViewModel c) => pathMode == LineageMode.Paternal ? c.IsMale : !c.IsMale;

        bool dominates(FamilyMemberCardViewModel nodeA, FamilyMemberCardViewModel nodeB)
        {
            if (nodeB == null) return true;

            bool bloodlineA = nodeA.ParentIds.Count > 0;
            bool bloodlineB = nodeB.ParentIds.Count > 0;
            if (bloodlineA && !bloodlineB) return true;
            if (bloodlineA && !bloodlineB) return true;
            if (bloodlineB && !bloodlineA) return false;

            return isPrimary(nodeA) && !isPrimary(nodeB);
        }

        foreach (var primary in cards.Where(c => c.PartnerIds.Count > 1))
        {
            var primaryRank = rankById.GetValueOrDefault(primary.Id, 0);
            foreach (var pid in primary.PartnerIds)
            {
                if (!cardById.TryGetValue(pid, out var partner)) continue;
                if (!dominates(primary, partner) || partner.ParentIds.Count > 0) continue;
                rankById[pid] = primaryRank + 0.5;
            }
        }
    }
}