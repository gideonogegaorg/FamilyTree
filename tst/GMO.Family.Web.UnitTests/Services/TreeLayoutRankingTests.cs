using GMO.Family.Web.Data;
using GMO.Family.Web.Models;
using GMO.Family.Web.Services;

using Xunit;

namespace GMO.Family.Web.UnitTests.Services;

public class TreeLayoutRankingTests
{
    private static FamilyMemberCardViewModel Card(long id, bool isMale, List<long> parentIds, List<long> partnerIds)
    {
        return new FamilyMemberCardViewModel
        {
            Id = id,
            Name = "Member " + id,
            IsMale = isMale,
            ParentIds = parentIds,
            PartnerIds = partnerIds,
            ChildIds = new List<long>()
        };
    }

    [Fact]
    public void ComputeRowByMember_single_root_has_row_zero()
    {
        var cards = new List<FamilyMemberCardViewModel>
        {
            Card(1, true, new List<long>(), new List<long>())
        };
        var rowById = TreeLayoutRanking.ComputeRowByMember(cards);
        Assert.Equal(0, rowById[1]);
    }

    [Fact]
    public void ComputeRowByMember_child_has_row_one()
    {
        var cards = new List<FamilyMemberCardViewModel>
        {
            Card(1, true, new List<long>(), new List<long>()),
            Card(2, false, new List<long> { 1 }, new List<long>())
        };
        var rowById = TreeLayoutRanking.ComputeRowByMember(cards);
        Assert.Equal(0, rowById[1]);
        Assert.Equal(1, rowById[2]);
    }

    [Fact]
    public void ComputeVisualRank_Paternal_multi_partner_male_partners_without_parents_get_half_rank()
    {
        // Male (id=1) row 0, two partners (id=2, 3) no parents -> rank 0.5 each. Child (id=4) of partner 2 -> row 1, rank 1.
        var cards = new List<FamilyMemberCardViewModel>
        {
            Card(1, true, new List<long>(), new List<long> { 2, 3 }),
            Card(2, false, new List<long>(), new List<long> { 1 }),
            Card(3, false, new List<long>(), new List<long> { 1 }),
            Card(4, true, new List<long> { 1, 2 }, new List<long>())
        };
        var rowById = TreeLayoutRanking.ComputeRowByMember(cards);
        var rankById = TreeLayoutRanking.ComputeVisualRank(cards, rowById, LineageMode.Paternal);

        Assert.Equal(0.0, rankById[1]);
        Assert.Equal(0.5, rankById[2]);
        Assert.Equal(0.5, rankById[3]);
        Assert.Equal(1.0, rankById[4]);
    }

    [Fact]
    public void ComputeVisualRank_Paternal_multi_partner_female_partners_keep_integer_rank()
    {
        // Female (id=1) with two partners (id=2, 3). Paternal = male primary; female is NOT primary so no half-ranks for her partners.
        var cards = new List<FamilyMemberCardViewModel>
        {
            Card(1, false, new List<long>(), new List<long> { 2, 3 }),
            Card(2, true, new List<long>(), new List<long> { 1 }),
            Card(3, true, new List<long>(), new List<long> { 1 })
        };
        var rowById = TreeLayoutRanking.ComputeRowByMember(cards);
        var rankById = TreeLayoutRanking.ComputeVisualRank(cards, rowById, LineageMode.Paternal);

        Assert.Equal(0.0, rankById[1]);
        Assert.Equal(0.0, rankById[2]);
        Assert.Equal(0.0, rankById[3]);
    }

    [Fact]
    public void ComputeVisualRank_Maternal_multi_partner_female_partners_without_parents_get_half_rank()
    {
        // Female (id=1) row 0, two partners (id=2, 3) no parents -> rank 0.5 each. Maternal = female primary.
        var cards = new List<FamilyMemberCardViewModel>
        {
            Card(1, false, new List<long>(), new List<long> { 2, 3 }),
            Card(2, true, new List<long>(), new List<long> { 1 }),
            Card(3, true, new List<long>(), new List<long> { 1 })
        };
        var rowById = TreeLayoutRanking.ComputeRowByMember(cards);
        var rankById = TreeLayoutRanking.ComputeVisualRank(cards, rowById, LineageMode.Maternal);

        Assert.Equal(0.0, rankById[1]);
        Assert.Equal(0.5, rankById[2]);
        Assert.Equal(0.5, rankById[3]);
    }

    [Fact]
    public void ComputeVisualRank_Maternal_multi_partner_male_partners_keep_integer_rank()
    {
        // Male (id=1) with two partners. Maternal = female primary; male is NOT primary so no half-ranks.
        var cards = new List<FamilyMemberCardViewModel>
        {
            Card(1, true, new List<long>(), new List<long> { 2, 3 }),
            Card(2, false, new List<long>(), new List<long> { 1 }),
            Card(3, false, new List<long>(), new List<long> { 1 })
        };
        var rowById = TreeLayoutRanking.ComputeRowByMember(cards);
        var rankById = TreeLayoutRanking.ComputeVisualRank(cards, rowById, LineageMode.Maternal);

        Assert.Equal(0.0, rankById[1]);
        Assert.Equal(0.0, rankById[2]);
        Assert.Equal(0.0, rankById[3]);
    }

    [Fact]
    public void ComputeVisualRank_Paternal_partner_with_parents_does_not_get_half_rank()
    {
        // Male (id=1) row 1, partners: (id=2) has parent 5 -> keeps integer rank; (id=3) no parents -> gets 0.5.
        var cards = new List<FamilyMemberCardViewModel>
        {
            Card(10, true, new List<long>(), new List<long>()),
            Card(5, true, new List<long>(), new List<long>()),
            Card(1, true, new List<long> { 10 }, new List<long> { 2, 3 }),
            Card(2, false, new List<long> { 5 }, new List<long> { 1 }),
            Card(3, false, new List<long>(), new List<long> { 1 })
        };
        var rowById = TreeLayoutRanking.ComputeRowByMember(cards);
        var rankById = TreeLayoutRanking.ComputeVisualRank(cards, rowById, LineageMode.Paternal);

        Assert.Equal(1.0, rankById[1]);
        Assert.Equal(1.0, rankById[2]); // has parents -> no half-rank, keeps row
        Assert.Equal(1.5, rankById[3]); // no parents -> gets primaryRank + 0.5
        Assert.Equal(0.0, rankById[5]);
        Assert.Equal(0.0, rankById[10]);
    }

    [Fact]
    public void ComputeVisualRank_single_partner_primary_no_half_ranks()
    {
        var cards = new List<FamilyMemberCardViewModel>
        {
            Card(1, true, new List<long>(), new List<long> { 2 }),
            Card(2, false, new List<long>(), new List<long> { 1 })
        };
        var rowById = TreeLayoutRanking.ComputeRowByMember(cards);
        var rankById = TreeLayoutRanking.ComputeVisualRank(cards, rowById, LineageMode.Paternal);

        Assert.Equal(0.0, rankById[1]);
        Assert.Equal(0.0, rankById[2]);
    }
}