using GMO.Family.Web.Data;
using GMO.Family.Web.Models;

namespace GMO.Family.Web.Services;

/// <summary>
/// Computes row (generation depth) and visual rank for family tree layout.
/// Used by HomeController to build nodes JSON; extracted for unit testing.
/// </summary>
public static class TreeLayoutRanking
{
    /// <summary>Row 0 = roots (no incoming parent); row k+1 = 1 + max(parent rows). Partners without parents inherit row from couple partner.</summary>
    public static Dictionary<long, int> ComputeRowByMember(IReadOnlyList<FamilyMemberCardViewModel> cards)
    {
        var cardById = cards.ToDictionary(c => c.Id);
        var rowById = new Dictionary<long, int>();

        foreach (var c in cards)
            if (c.ParentIds.Count == 0)
                rowById[c.Id] = 0;

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var c in cards)
            {
                if (rowById.ContainsKey(c.Id)) continue;
                if (c.ParentIds.Count == 0) continue;
                var parentRows = c.ParentIds.Where(pid => rowById.TryGetValue(pid, out _)).Select(pid => rowById[pid]).ToList();
                if (parentRows.Count != c.ParentIds.Count) continue;
                rowById[c.Id] = 1 + parentRows.Max();
                changed = true;
            }
        }

        foreach (var c in cards)
        {
            if (c.ParentIds.Count > 0) continue;
            foreach (var pid in c.PartnerIds)
                if (rowById.TryGetValue(pid, out var pr) && pr > rowById.GetValueOrDefault(c.Id, 0))
                {
                    rowById[c.Id] = pr;
                    break;
                }
        }

        // Re-propagate: partner alignment may have bumped parentless members,
        // so their children need updated rows.
        changed = true;
        while (changed)
        {
            changed = false;
            foreach (var c in cards)
            {
                if (c.ParentIds.Count == 0) continue;
                var parentRows = c.ParentIds.Where(pid => rowById.ContainsKey(pid)).Select(pid => rowById[pid]).ToList();
                if (parentRows.Count != c.ParentIds.Count) continue;
                var newRow = 1 + parentRows.Max();
                if (newRow > rowById.GetValueOrDefault(c.Id, 0))
                {
                    rowById[c.Id] = newRow;
                    changed = true;
                }
            }
        }

        foreach (var c in cards)
            if (!rowById.ContainsKey(c.Id))
                rowById[c.Id] = 0;

        return rowById;
    }

    /// <summary>
    /// Visual rank: same as row for most members. Partners of a multi-partner primary who have
    /// no parents of their own get rank = primary's rank + 0.5 (a half-level between the primary
    /// and their children). Primary is male when Paternal, female when Maternal.
    /// </summary>
    public static Dictionary<long, double> ComputeVisualRank(
        IReadOnlyList<FamilyMemberCardViewModel> cards,
        Dictionary<long, int> rowById,
        TreePathMode pathMode)
    {
        var cardById = cards.ToDictionary(c => c.Id);
        var rankById = rowById.ToDictionary(kv => kv.Key, kv => (double)kv.Value);

        bool isPrimary(FamilyMemberCardViewModel c) => pathMode == TreePathMode.Paternal ? c.IsMale : !c.IsMale;

        var multiPartnerPrimaries = cards.Where(c => isPrimary(c) && c.PartnerIds.Count > 1).ToList();

        foreach (var primary in multiPartnerPrimaries)
        {
            var primaryRank = rankById.GetValueOrDefault(primary.Id, 0);
            foreach (var pid in primary.PartnerIds)
            {
                if (!cardById.TryGetValue(pid, out var partner)) continue;
                if (partner.ParentIds.Count > 0) continue;
                rankById[pid] = primaryRank + 0.5;
            }
        }

        return rankById;
    }
}
