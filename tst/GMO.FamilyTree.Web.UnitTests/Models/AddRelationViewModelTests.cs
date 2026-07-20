using System.ComponentModel.DataAnnotations;

using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Models;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Models;

public class AddRelationViewModelTests
{
    [Fact]
    public void Validate_rejects_death_before_birth()
    {
        var model = new AddRelationViewModel
        {
            ContextMemberId = 1,
            FamilyTreeId = 1,
            Name = "Test Person",
            RelationshipType = RelationshipType.Couple,
            IsChild = false,
            IsMale = true,
            SetAsMe = false,
            DOB = new DateOnly(2000, 1, 1),
            DOD = new DateOnly(1999, 12, 31)
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        Assert.False(valid);
        var result = Assert.Single(results);
        Assert.Contains(nameof(AddRelationViewModel.DOD), result.MemberNames);
    }

    [Fact]
    public void Validate_accepts_death_on_or_after_birth()
    {
        var model = new AddRelationViewModel
        {
            ContextMemberId = 1,
            FamilyTreeId = 1,
            Name = "Test Person",
            RelationshipType = RelationshipType.Couple,
            IsChild = false,
            IsMale = true,
            SetAsMe = false,
            DOB = new DateOnly(1950, 7, 19),
            DOD = new DateOnly(2020, 3, 2)
        };

        var valid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            validationResults: null,
            validateAllProperties: true);

        Assert.True(valid);
    }
}