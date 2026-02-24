# Tree Layout Orientation: Horizontal vs Vertical

The family tree supports two display orientations, toggled from the **user menu** under **Tree layout**.

---

## Overview

| Aspect | Vertical (default before) | Horizontal (current default) |
|---|---|---|
| Tree flow | Top → Down | Left → Right |
| Ranks | Rows (stacked vertically) | Columns (stacked horizontally) |
| Grandparents | Top of the page | Left side of the page |
| Children / Cousins | Bottom of the page | Right side of the page |
| Couple link (red line) | Horizontal line between spouses | Vertical line between spouses |
| Sibling connector bar | Horizontal bar across siblings | Vertical bar across siblings |

> [!IMPORTANT]
> **Core rule**: Horizontal mode is literally a 90° rotation of Vertical. Rows become columns, `height` becomes `width`, `top` becomes `left`, etc. Every CSS, JS, and connector change follows this axis swap.

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

When a half-rank exists (e.g., rank 1.5 for a partners), the JS inserts `.ft-rank-spacer` elements in branches that skip that half-rank, pushing their children down/right to align with children in branches that do have a half-rank member.

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
