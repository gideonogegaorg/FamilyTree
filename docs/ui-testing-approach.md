# UI Testing Approach: Layout Orientation & Visual Rank Validation

This document describes the testing strategy and expected behavior validation for the family tree UI, specifically focusing on layout orientation and visual rank positioning.

---

## Overview

The UI testing strategy uses **relative positioning validation** rather than pixel-perfect alignment to ensure robust, maintainable tests that catch actual regressions while accommodating browser rendering variations.

### Key Principles

1. **Visual Rank-Based Testing**: Tests validate the actual visual rank system used by the production layout algorithm
2. **Relative Positioning**: Tests check relative positioning within tolerance ranges rather than exact pixel coordinates
3. **DOM Independence**: Tests validate visual output against expected behavior, not implementation details
4. **Browser Tolerance**: Tests accommodate rendering differences across browsers and environments

---

## Prerequisites

See [`testing-environment.md`](testing-environment.md) for database, test account, and environment setup. Required for layout UI tests: database running, seed data (**25** members on tree **1**), test account linked to **"Me" (56)**, app server running.

**Critical**: Without the primary 25-member tree, layout tests cannot validate visual ranks, orientation, or lineage modes.

### UI Test Files

| File | Focus |
|---|---|
| `LayoutOrientationTests.cs` | Visual ranks, orientation, lineage |
| `LandingPageTests.cs` | Public landing + demo tree + mobile overflow |
| `MemberDetailsHoverTests.cs` | View-first details / siblings |
| `MemberActionPopupTests.cs` | Manage / action menu |
| `MobileTreeViewportTests.cs` | Touch pan / pinch zoom |
| `FlexDateInputTests.cs` | Flexible DOB/DOD entry |

### Test Categories

| Category | Purpose | Key Validations |
|---|---|---|
| **Layout Orientation** | Horizontal vs Vertical layout | Axis alignment, rank ordering, half-rank positioning |
| **Lineage Mode** | Paternal vs Maternal lineage | Primary side positioning, rank assignments |
| **Member UX** | Details, manage, dates | Hover/tap details, siblings, flex dates |
| **Mobile viewport** | Tree gestures | One-finger pan, two-finger pinch |
| **Landing** | Anonymous marketing page | Demo tree, auth CTAs, no horizontal overflow |
| **User Interaction** | Toolbar / menus | CSS classes, DOM attributes, reloads |

---

## Visual Rank Testing Strategy

### Data-Driven Approach

Tests use DOM `data-visual-rank` attributes to group nodes by their actual visual rank:

```javascript
// Each node has this attribute in production
<div class="family-tree-card" data-visual-rank="1.0">Father</div>
<div class="family-tree-card" data-visual-rank="1.5">Mother</div>
```

### Test Flow

1. **Group nodes by visual rank** (0, 0.5, 1, 1.5, 2)
2. **Validate alignment within each rank group**
3. **Validate rank ordering** (ascending sequence)
4. **Validate half-rank positioning** (between integer ranks)
5. **Validate spread** (nodes distributed along perpendicular axis)

---

## Expected Behavior Validation

### 1. Visual Rank Alignment

**Vertical Layout (Same Y per rank):**
```
Rank 0.0: [Paternal Grandpa]    ← Same Y coordinate
Rank 0.5: [Paternal Grandma]    ← Same Y coordinate  
Rank 1.0: [Father, Fathers Brother] ← Same Y coordinate
Rank 1.5: [Mother, FB Wife 1/2] ← Same Y coordinate
Rank 2.0: [Me, Cousins]         ← Same Y coordinate
```

**Horizontal Layout (Same X per rank):**
```
Rank 0.0: [Paternal Grandpa]    ← Same X coordinate
Rank 0.5: [Paternal Grandma]    ← Same X coordinate
Rank 1.0: [Father, Fathers Brother] ← Same X coordinate
Rank 1.5: [Mother, FB Wife 1/2] ← Same X coordinate
Rank 2.0: [Me, Cousins]         ← Same X coordinate
```

### 2. Rank Ordering Validation

- **Ascending order**: Rank 0.0 < 0.5 < 1.0 < 1.5 < 2.0
- **Position validation**: Lower ranks appear before higher ranks in layout direction
- **Half-rank positioning**: 0.5 between 0.0 and 1.0, 1.5 between 1.0 and 2.0

### 3. Lineage Mode Differences

**Paternal Mode:**
- Primary gender: Male
- Primary lineage: Father's side
- Half-rank partners: Multi-partner males' spouses

**Maternal Mode:**
- Primary gender: Female  
- Primary lineage: Mother's side
- Half-rank partners: Multi-partner females' spouses

### 4. Orientation Switching

Tests toggle layout and lineage via the **tree toolbar** (`.ft-subbar .ft-pills`), not the user menu.

**CSS Class Validation:**
- Vertical: No `ft-orientation-horizontal` class
- Horizontal: `ft-orientation-horizontal` class present

**Layout Rotation:**
- 90° rotation principle: rows become columns
- Axis swap: Y alignment → X alignment, X alignment → Y alignment

---

## Test Implementation Details

### AssertAlignment Method

**Relative Positioning Validation:**
```csharp
private void AssertAlignment(List<object> boxes, string generation, string axis, float tolerance)
{
    if (boxes.Count <= 1) return;
    
    var values = new List<float>();
    foreach (var box in boxes) {
        dynamic dynamicBox = box;
        float value = axis == "X" ? dynamicBox.X : dynamicBox.Y;
        values.Add(value);
    }
    
    values.Sort();
    float min = values[0];
    float max = values[values.Count - 1];
    
    Assert.True(max - min <= tolerance, 
        $"{generation} {axis} positions vary too much: range [{min}, {max}] (max difference: {max - min}, tolerance: {tolerance})");
}
```

**Key Features:**
- **Range-based validation**: Checks min/max difference instead of exact values
- **Tolerance-based**: 50px tolerance accommodates browser rendering differences
- **Skip single nodes**: No alignment check for groups with only one node

### Test Data Structure

**Static Family Test Data:**
```csharp
// Illustrative only — real seed IDs are 50–74 on tree 1 (Me = 56). See seed_trees.sql.
private static readonly Dictionary<string, FamilyMemberTestData> FamilyTestData = new()
{
    { "Paternal Grandpa", new FamilyMemberTestData { Id = 51 } },
    { "Father", new FamilyMemberTestData { Id = 55 } },
    // ... more family members
};
```

**Purpose:**
- Provides known family structure for test validation
- Defines expected visual ranks for both lineage modes
- Ensures test consistency and reliability

---

## Validation Criteria

### Alignment Validation

- **Same rank nodes**: Must align within 50px tolerance on correct axis
- **Vertical mode**: Same Y coordinate for same visual rank
- **Horizontal mode**: Same X coordinate for same visual rank

### Ordering Validation

- **Rank sequence**: Must be in ascending order (0.0 → 0.5 → 1.0 → 1.5 → 2.0)
- **Position direction**: Lower ranks before higher ranks in layout direction
- **Half-rank placement**: Between appropriate integer ranks

### Spread Validation

- **Perpendicular axis**: Nodes must be distributed along spread axis
- **Minimum spread**: 50px minimum spread within each rank group
- **Multiple nodes**: Only validate spread when multiple nodes exist in rank

### Half-Rank Validation

- **Positioning**: Half-rank nodes positioned between integer ranks
- **Vertical layout**: Y coordinate between rank groups
- **Horizontal layout**: X coordinate between rank groups

---

## Test Coverage Matrix

| Test | Orientation | Lineage Mode | Alignment | Ordering | Half-Rank | Spread |
|---|---|---|---|---|---|---|
| `VerticalLayout_PositionsEveryNodeAndRank` | Vertical | Auto | Yes | Yes | No | Yes |
| `HorizontalLayout_PositionsEveryNodeAndRank` | Horizontal | Auto | Yes | Yes | No | Yes |
| `VerticalLayout_Paternal_PositionsEveryNodeAndRank` | Vertical | Paternal | Yes | Yes | Yes | Yes |
| `HorizontalLayout_Paternal_PositionsEveryNodeAndRank` | Horizontal | Paternal | Yes | Yes | Yes | Yes |
| `VerticalLayout_Maternal_PositionsEveryNodeAndRank` | Vertical | Maternal | Yes | Yes | Yes | Yes |
| `HorizontalLayout_Maternal_PositionsEveryNodeAndRank` | Horizontal | Maternal | Yes | Yes | Yes | Yes |

---

## Regression Prevention

### What Tests Catch

1. **Visual Layout Changes**: Any change to visual rank positioning
2. **Orientation Bugs**: Issues with horizontal/vertical switching
3. **Lineage Mode Issues**: Problems with paternal/maternal rank assignment
4. **CSS Regression**: Changes that break axis alignment
5. **JavaScript Layout Issues**: Problems with dynamic positioning

### What Tests Don't Catch

1. **Minor Pixel Variations**: < 50px rendering differences (intentional)
2. **Browser-Specific Rendering**: Cross-browser rendering variations
3. **Font/Size Changes**: Minor visual differences that don't affect layout logic

### Test Robustness

- **Browser Independent**: Works across different browsers and rendering engines
- **Environment Stable**: Consistent across different test environments
- **Maintainable**: Easy to understand and modify test expectations
- **Reliable**: Low false positive rate due to tolerance-based validation

---

## Best Practices

### Test Design

1. **Use relative positioning** instead of absolute coordinates
2. **Validate behavior** not implementation details
3. **Group by visual rank** for accurate layout validation
4. **Use reasonable tolerances** for browser rendering differences

### Maintenance

1. **Update test data** when family structure changes
2. **Adjust tolerances** if rendering behavior changes significantly
3. **Add new test cases** for new layout features
4. **Review test coverage** regularly for completeness

### Debugging

1. **Check visual rank attributes** when tests fail
2. **Verify DOM structure** matches expected layout
3. **Validate CSS classes** for orientation switching
4. **Review tolerance values** for environmental differences

---

## Future Enhancements

Possible additions: visual regression (screenshot comparison), broader cross-browser runs, deeper accessibility (screen reader / keyboard). Responsive/mobile tree and landing coverage already exists in `MobileTreeViewportTests` / `LandingPageTests`.
