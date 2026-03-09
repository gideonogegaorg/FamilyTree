# Tree Layout Orientation: Horizontal vs Vertical

The family tree supports two display orientations, toggled from the **user menu** under **Tree layout**.

---

## Overview

| Aspect | Vertical (default before) | Horizontal (current default) |
|---|---|---|
| Tree flow | Top → Down | Left → Right |
| Ranks | Rows (stacked vertically) | Columns (stacked horizontally) |
| Alignment axis | **Same Y** per generation | **Same X** per generation |
| Grandparents | Top of the page | Left side of the page |
| Children / Cousins | Bottom of the page | Right side of the page |
| Couple link (red line) | Horizontal line between spouses | Vertical line between spouses |
| Sibling connector bar | Horizontal bar across siblings | Vertical bar across siblings |

> [!IMPORTANT]
> **Core rule**: Horizontal mode is literally a 90° rotation of Vertical. Rows become columns, `height` becomes `width`, `top` becomes `left`, etc. Every CSS, JS, and connector change follows this axis swap.

### Visual Coordinate System

**Vertical Layout:**
```
Y=105  [Grandparents] ← Same Y (row alignment)
Y=241  [Parents]      ← Same Y (row alignment)  
Y=519  [Children]    ← Same Y (row alignment)
       ↑
       Y increases downward
```

**Horizontal Layout:**
```
X=40   [Grandparents] ← Same X (column alignment)
X=272  [Parents]      ← Same X (column alignment)
X=690  [Children]    ← Same X (column alignment)
       →
       X increases rightward
```

---

## Enum & Persistence

Defined in [`TreeViewOrientation.cs`](../src/GMO.Family.Web/Data/TreeViewOrientation.cs):

```csharp
public enum TreeViewOrientation
{
    Horizontal = 0,  // Default
    Vertical = 1
}
```

- **Stored** on `UserProfile.TreeViewOrientation` (nullable `int` in DB)
- **Session fallback**: also cached in session key `"TreeViewOrientation"`
- **Service**: `ITreeViewOrientationService` / `TreeViewOrientationService` — reads from session first, then DB
- **Toggle endpoint**: `POST /Account/SetTreeViewOrientation?orientation={0|1}`

---

## How It Works

### 1. Server-Side (Razor)

In [`Index.cshtml`](../src/GMO.Family.Web/Views/Home/Index.cshtml), the container receives the CSS class and data attribute:

```html
<div id="family-tree-graph"
     class="family-tree-graph @(isHorizontal ? "ft-orientation-horizontal" : "")"
     data-orientation="@Model.TreeViewOrientation"
     ...>
```

The `ft-orientation-horizontal` CSS class triggers all the horizontal overrides.

### 2. CSS Overrides ([`site.css`](../src/GMO.Family.Web/wwwroot/css/site.css))

All horizontal-specific rules are scoped under `.ft-orientation-horizontal`. Key transformations:

| Element | Vertical (default) | Horizontal override |
|---|---|---|
| `.ft-roots` | `flex-direction: row` | `flex-direction: column` |
| `.ft-unit` | `flex-direction: column` | `flex-direction: row` |
| `.ft-parents` | `flex-direction: row` | `flex-direction: column` |
| `.ft-couple-link` | `border-top` (horizontal red line) | `border-left` (vertical red line) |
| `.ft-children` | `flex-direction: row`, `padding-top/margin-top` | `flex-direction: column`, `padding-left/margin-left` |
| `.ft-branch` | `flex-direction: column`, `padding-top` | `flex-direction: row`, `padding-left` |
| `.ft-branch::before` | Vertical drop line (`border-left`) | Horizontal drop line (`border-top`) |
| `.ft-branch::after` | Horizontal sibling bar (`border-top`) | Vertical sibling bar (`border-left`) |
| `.ft-partner-units` | `flex-direction: row`, `margin-top` | `flex-direction: column`, `margin-left` |
| `.ft-partner-unit` | `flex-direction: column` | `flex-direction: row` |
| `.ft-rank-spacer` | Expands via `height` | Expands via `width` |
| `.ft-rank-spacer::after` | Vertical line (`border-left`) | Horizontal line (`border-top`) |

### 3. JavaScript Spacer Alignment ([`family-tree.js`](../src/GMO.Family.Web/wwwroot/js/family-tree.js))

The half-rank spacer alignment logic uses three orientation-aware variables:

```javascript
var isHorizontal = container.classList.contains('ft-orientation-horizontal');
var rankDim    = isHorizontal ? 'width'      : 'height';     // spacer size dimension
var rankPos    = isHorizontal ? 'left'       : 'top';        // getBoundingClientRect() position
var rankMargin = isHorizontal ? 'marginLeft' : 'marginTop';  // root branch offset margin
```

These are used in two phases:
- **Phase 2a** — Aligns satellite root branches by measuring `getBoundingClientRect()[rankPos]` and setting `style[rankMargin]`
- **Phase 2b** — Adjusts spacer sizes by setting `spacer.style[rankDim]` so same-rank members line up

---

## Column / Row Alignment by Rank

Members are assigned a `visualRank` that determines which column (horizontal) or row (vertical) they appear in:

| Column/Row | Rank | Members |
|---|---|---|
| 0 | 0 | Grandparents |
| 1 | 1 | Parents, Uncles/Aunts |
| 1.5 | 1.5 | Partners of Uncles/Aunts (half-rank) |
| 2 | 2 | Me, Cousins, Siblings |

### Coordinate Alignment Details

**Vertical Mode - Row Alignment (Same Y):**
- All grandparents share the same Y coordinate (row 0)
- All parents share the same Y coordinate (row 1) 
- All children share the same Y coordinate (row 2)
- X coordinates vary horizontally within each row

**Horizontal Mode - Column Alignment (Same X):**
- All grandparents share the same X coordinate (column 0)
- All parents share the same X coordinate (column 1)
- All children share the same X coordinate (column 2)
- Y coordinates vary vertically within each column

### Practical Examples

**Structure and coordinates:** Given grandparents (rank 0), parents (rank 1), children (rank 2), and a half-rank partner (rank 1.5). Vertical: same Y per rank (e.g. Y=105 grandparents, Y=241 parents, Y=519 children; half-rank at Y=383). Horizontal: same X per rank (e.g. X=40, X=272, X=690; half-rank at X=458). When a half-rank exists, the JS inserts `.ft-rank-spacer` in branches that skip it so children align.

**Paternal mode (primary male):**
```
Paternal Grandpa (Row 0, Visual Rank 0.0) ← Primary male
├── Paternal Grandma (Row 0, Visual Rank 0.5) ← Secondary partner
├── Father (Row 1, Visual Rank 1.0)
└── Fathers Brother (Row 1, Visual Rank 1.0)
    ├── FB Wife 1 (Row 1, Visual Rank 1.5) ← half-rank
    └── FB Wife 2 (Row 1, Visual Rank 1.5)
```

**Maternal mode (primary female):**
```
Maternal Grandma (Row 0, Visual Rank 0.0) ← Primary female
├── Maternal Grandpa 1 (Row 0, Visual Rank 0.5)
├── Maternal Grandpa 2 (Row 0, Visual Rank 0.5)
└── Mother (Row 1, Visual Rank 1.0)
    ├── Father (Row 1, Visual Rank 1.5)
    └── Mothers HalfSib (Row 1, Visual Rank 1.0)
```

---

## Tree Layout Ranking System

The family tree uses a two-level ranking system to determine node positioning:

### Row vs Visual Rank

| Concept | Type | Purpose | Examples |
|---|---|---|---|
| **Row** | Integer (0, 1, 2, ...) | Generation depth based on parent-child relationships | Row 0 = grandparents, Row 1 = parents, Row 2 = children |
| **Visual Rank** | Double (0, 0.5, 1, 1.5, 2, ...) | Extended row system with half-ranks for secondary partners | Rank 0.5 = secondary partners, Rank 1.5 = half-rank spouses |

### How the Ranking System Works

#### 1. Row Calculation (`TreeLayoutRanking.ComputeRowByMember`)

- **Root members** (no parents) get Row 0
- **Children** get Row = 1 + max(parent rows)
- **Partners without parents** inherit their partner's row
- **Propagation**: Changes cascade through the family tree to maintain consistency

#### 2. Visual Rank Calculation (`TreeLayoutRanking.ComputeVisualRank`)

- **Base**: Starts with row values (converts to double)
- **Primary determination**: 
  - Paternal mode: Males are primary
  - Maternal mode: Females are primary
- **Half-rank assignment**: Partners of multi-partner primary members who have no parents get `primaryRank + 0.5`
- **Bloodline domination**: Members with parents in the tree dominate those inserted via marriage

### Implementation Details

The ranking system is implemented in `TreeLayoutRanking.cs` with two main methods:

1. **`ComputeRowByMember`**: Calculates generation depth using parent relationships
2. **`ComputeVisualRank`**: Extends rows with half-ranks based on lineage mode and partnership patterns

Both methods are used by `HomeController` to generate the `data-visual-rank` attributes that the JavaScript layout engine uses for positioning.

**Rank summary:**

| Rank | Description | Typical Members |
|---|---|---|
| 0 | Primary root nodes | Paternal Grandpa (Paternal mode), Maternal Grandma (Maternal mode) |
| 0.5 | Secondary partners | Paternal Grandma, Maternal Grandpa 1/2 |
| 1 | Primary children | Father, Fathers Brother, Mothers HalfSib |
| 1.5 | Secondary children & half-rank spouses | Mother, FB Wife 1/2, Wife2 Only Child |
| 2 | Grandchildren generation | Me, Cousins, Siblings |

### Key Principle

**Nodes with the same visual rank align on the same axis:**
- **Vertical Layout**: Same Y coordinate for each rank (row alignment)
- **Horizontal Layout**: Same X coordinate for each rank (column alignment)

This means nodes within the same "generation" but different visual ranks (e.g., Father vs Mother in Paternal mode) will not be aligned, which is the correct behavior according to the layout algorithm.

### Data Attributes

Each node receives a `data-visual-rank` attribute that the UI tests read to verify correct positioning:

```html
<div class="family-tree-card" data-visual-rank="1.0">Father</div>
<div class="family-tree-card" data-visual-rank="1.5">Mother</div>
```

---

## User Menu Toggle

The toggle is rendered in the user dropdown menu ([`UserMenu/Default.cshtml`](../src/GMO.Family.Web/Views/Shared/Components/UserMenu/Default.cshtml)):

```
Tree layout
[Horizontal] [Vertical]
```

Each button submits a `POST` to `AccountController.SetTreeViewOrientation` with `orientation=0` (Horizontal) or `orientation=1` (Vertical). The page reloads with the new layout applied.

---

## Key Design Decisions

1. **CSS-first approach**: The bulk of the horizontal layout is pure CSS, scoped under `.ft-orientation-horizontal`. No DOM structure changes — the same HTML works for both orientations.
2. **Axis swap principle**: Every geometric property is swapped consistently — `row↔column`, `top↔left`, `height↔width`, `border-top↔border-left`.
3. **JS alignment is orientation-aware**: The spacer logic doesn't duplicate code for each orientation; it uses `rankDim`/`rankPos`/`rankMargin` variables to handle both modes with the same algorithm.

---

## Testing Validation

The UI tests validate the documented behavior using a **relative positioning approach**. For detailed testing strategy and implementation, see [`ui-testing-approach.md`](ui-testing-approach.md).

### Test Coverage Overview

- **Visual Rank-Based Testing**: Tests read `data-visual-rank` attributes and validate relative positioning
- **Orientation Validation**: Confirms horizontal/vertical layout switching works correctly
- **Lineage Mode Testing**: Validates paternal/maternal rank assignments and positioning
- **Robust Tolerance**: Uses 50px tolerance for browser rendering variations

### Key Test Files

- **Primary**: `tst/GMO.Family.Web.UiTests/LayoutOrientationTests.cs`
- **Documentation**: `docs/ui-testing-approach.md` (detailed testing strategy)
- **Coverage**: All layout orientations, lineage modes, and positioning validation

---

## Tree Lineage Mode: Paternal vs Maternal

The family tree also supports two lineage modes that determine which lineage is emphasized:

| Aspect | Paternal (default) | Maternal |
|---|---|---|
| Primary gender | **Male** (fathers, grandfathers) | **Female** (mothers, grandmothers) |
| Focus lineage | Paternal side (father's family) | Maternal side (mother's family) |
| Layout emphasis | Father's branch is primary/featured | Mother's branch is primary/featured |
| Half-rank logic | Multi-partner **males** get half-rank for partners | Multi-partner **females** get half-rank for partners |

### Primary Selection and Bloodline Domination

When dealing with multi-partner relationships, determining which partner anchors the layout evaluates their bloodline depth first (for same-sex couples: the **dominant** partner is the one connected to the tree by bloodline):

```csharp
bool isPrimary(FamilyMemberCardViewModel c) => 
    pathMode == LineageMode.Paternal ? c.IsMale : !c.IsMale;

bool dominates(FamilyMemberCardViewModel nodeA, FamilyMemberCardViewModel nodeB)
{
    // 1. Bloodline depth: nodes with parents in the tree dominate those inserted via marriage (dominant = bloodline-connected)
    bool bloodlineA = nodeA.ParentIds.Count > 0;
    bool bloodlineB = nodeB.ParentIds.Count > 0;
    if (bloodlineA && !bloodlineB) return true;
    if (!bloodlineA && bloodlineB) return false;

    // 2. Fallback: Lineage mode gender logic (primary gender)
    return isPrimary(nodeA) && !isPrimary(nodeB);
}
```

This ensures that the member natively connected to the family tree topology (e.g. "Fathers Brother" who has parents) anchors the visual tree, even if the tree is rendering in a Lineage mode (like Maternal) where their gender isn't typically primary.

#### Same-Sex Couples Support
The "bloodline domination" rule naturally supports same-sex couples (e.g., Male/Male or Female/Female) without requiring special casing. If both partners share the same gender, the Lineage mode's gender preference yields a tie. The tie is broken by the topological connection: the **dominant** partner (connected to the tree by bloodline, has parents in the data) dominates the partner who was inserted via marriage (has no parents).
- **In Paternal mode**: A bloodline Male dominates an inserted Male partner. The inserted Male partner will be correctly recognized as a secondary partner.
- **In Maternal mode**: A bloodline Female dominates an inserted Female partner identically.
Both C# and JS layout engines share this exact logic to keep rendering consistent.

### Visual Layout Differences

**Paternal Mode:**
- Father's lineage (Paternal Grandparents → Father → Me) gets primary positioning
- Mother's lineage is secondary
- Half-rank spouses: Father's Brother's wives (FB Wife 1/2) get rank 1.5

**Maternal Mode:**
- Mother's lineage (Maternal Grandparents → Mother → Me) gets primary positioning  
- Father's lineage is secondary
- Half-rank spouses: Maternal Grandpa 2's wife (Wife2 Only Child) gets rank 1.5

### Toggle Controls

Lineage mode is toggled from the **user menu** under **Lineage**:
```
Lineage
[Paternal] [Maternal]
```

Each button submits a `POST` to `AccountController.SetLineageMode` with `mode=0` (Paternal) or `mode=1` (Maternal).
