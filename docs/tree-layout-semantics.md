# Tree Layout Semantics (Canonical Definitions)

This document is the source of truth for the *meaning* of the layout modes and
the dominance/half-rank rules. Mechanical details (CSS class names, enum
persistence, toggle endpoints) live in
[`tree-layout-orientation.md`](tree-layout-orientation.md); if that document
ever disagrees with this one on semantics, this one wins.

---

## Orientation: Horizontal vs Vertical

Orientation controls which screen axis a *generation* occupies. A generation is
the set of members sharing the same **visual rank**.

| Mode | Rule |
|---|---|
| **Horizontal** | Members with the same visual rank are on the same level **vertically** — they share a **column** (same X). The tree flows left → right: earliest generation at the left, latest at the right. |
| **Vertical** | Members with the same visual rank are on the same level **horizontally** — they share a **row** (same Y). The tree flows top → down: earliest generation at the top, latest at the bottom. |

Horizontal is a strict 90° rotation of Vertical. No relationship or rank
changes between the two modes — only the axis mapping.

---

## Lineage: Paternal vs Maternal

Lineage mode selects which member of a couple is **dominant** — the person who
anchors the couple's position in the layout. The other partner is placed
relative to them.

| Mode | Rule |
|---|---|
| **Paternal** | The dominant person is the **male** person **with blood ties to the root tree**. |
| **Maternal** | The dominant person is the **female** person **with blood ties to the root tree**. |

Two conditions combine, and blood ties outrank gender:

1. **Blood tie first.** A member connected to the tree by bloodline dominates a
   partner who was inserted via marriage. This holds in both lineage modes — 
   e.g. in Maternal mode, a blood-tied male ("Fathers Brother") still dominates
   his non-blood wives.
2. **Gender as tie-breaker.** When both partners have the same bloodline status,
   the lineage mode's gender wins: male in Paternal, female in Maternal.

### Same-sex couples

Gender cannot break the tie, so lineage mode is irrelevant: the blood-tied
partner is dominant, and the inserted-by-marriage partner is secondary. No
special casing is required — rule 1 alone decides.

---

## Multiple spouses and half-ranks

When a blood-tied member has **multiple spouses**, that member is dominant and
each of their (non-blood) spouses is **half-ranked**: the spouse renders at
`dominant's rank + 0.5`, visually bumped to a lane between the dominant
member's generation and the next one.

- Half-rank lanes only exist when needed; a tree with no multi-spouse
  households has only integer ranks.
- A spouse who has their own parents in the tree (i.e. is themselves
  blood-tied) is **never** half-ranked — they keep their own integer
  generation rank.
- Example (3-gen test tree, either lineage mode): "Fathers Brother" (blood,
  rank 1) has two non-blood wives → "FB Wife 1" and "FB Wife 2" both render at
  rank 1.5.

### Children of half-ranked spouses

A child of the dominant member *and* a half-ranked spouse belongs to that
spouse's sub-column under the dominant member (e.g. "Cousin 3" under FB Wife
2's slot).

A child of **only** a half-ranked spouse (no blood tie to the dominant member,
e.g. "Wife2 Only Child" whose parents are FB Wife 2 and someone outside the
household) is **not** part of the dominant member's family: rendering them
inside it would falsely read as the dominant member's child. They form an
**island column** at the far right of the tree, with the spouse's card
duplicated above them (DOM id `member-{id}-island`) so the island reads as
"this spouse's other family". The spouse's primary card stays in the main
tree. This mirrors production's per-marriage island behavior.

---

## Where these rules are implemented

| Concern | Location |
|---|---|
| Row (generation depth) + visual rank incl. half-ranks | `src/GMO.Family.Web/Services/TreeLayoutRanking.cs` (server-side, ground truth) |
| Marriage-tree forest placement | `src/GMO.Family.Web/wwwroot/js/family-tree.js` (`buildBranchBottomUp`, `getPartnerFamilies`) |
| Card rendering, orientation wiring | `src/GMO.Family.Web/wwwroot/js/family-tree.js` |

The client **must not** re-derive dominance in a way that can disagree with
the server's `visualRank`. The server's half-rank assignment is the ground
truth for who is dominant within a couple; client heuristics (gender, etc.)
may only be used where the ranks alone cannot answer the question (e.g.
left-right ordering within a lane).

---

## Edge cases validated against production (family-dev.goom.life, 2026-07-04)

When in doubt, production is the reference. The 3-gen test tree was inspected
on family-dev in both lineage modes; observed `data-visual-rank` values are
the ruling.

1. **"Blood-tied" literally means *has parents in the tree***
   (`ParentIds.Count > 0`). Root-generation members are never blood-tied, so
   for a multi-spouse **root** dominance falls back to gender alone.
   Confirmed: in **Paternal** mode every root member renders at vrank 0 — no
   half-ranks at the root generation at all. Multi-spouse "Maternal Grandma"
   does *not* dominate her husbands in Paternal mode; each of her marriages
   renders as its own couple unit. In **Maternal** mode the grandmas dominate
   and the grandpas drop to vrank 0.5.

2. **Same-sex spouse of a multi-spouse root is never half-ranked.** Gender
   cannot break the root-generation tie, so "Paternal Grandma Wife" and
   "Maternal Grandma Wife 1/2" stay at vrank 0 in **both** modes. Production
   renders each such marriage as a separate couple unit beside the others
   (the legacy engine duplicates the hub's card into each unit; the branch
   engine places spouses adjacent to the single hub card instead).

3. **A blood-tied spouse is never half-ranked, in any mode.** Confirmed:
   "Mother" (has parents) renders at vrank 1 in Paternal mode, not 1.5, even
   though Father is the dominant partner. Half-ranks are exclusively for
   spouses married into the tree (no parents) whose partner is a dominant
   multi-partner member.

4. **Blood-tie dominance for non-root generations works for both genders.**
   Confirmed in both modes: "FB Wife 1/2" (wives of blood-tied Fathers
   Brother) and "HalfSib Husband 1/2" (husbands of blood-tied Mothers
   HalfSib) all render at vrank 1.5 regardless of lineage mode.

### Both partners blood-tied (agreed direction, general case deferred)

Since both partners keep integer ranks (a blood-tied spouse is never
half-ranked), dominance here only decides *couple anchoring* — whose column
the pair sits in — never ranks.

- **Different-sex couple**: gender decides — male dominates in Paternal,
  female in Maternal. This is what the existing `dominates()` fallback
  already does (bloodline status ties, gender breaks the tie); e.g. Father ×
  Mother, confirmed on production.
- **Same-sex couple**: treated as leaf nodes. A childless couple needs no
  dominance decision; if they have a child, the child has exactly one blood
  parent within the couple and follows that parent's column, so no
  couple-level ruling is needed either.
- **Deferred**: multigenerational cross-links (e.g. cousins marrying), where
  both partners drag their own ancestor subtrees into the layout. This is a
  connector/rendering problem; do not rely on any current behavior. Construct
  a test tree before implementing.

### Still open (not answerable from the 3-gen tree)

- **Left-right ordering within a lane**: the client's `sortWithinLane`
  prefers the lineage-primary gender first, then birth order, then id. Not
  yet validated against production ordering.

---

See [`tree-layout-reference-tables.md`](tree-layout-reference-tables.md) for all four
orientation × lineage ASCII tables (3-Gen Test Tree).

## Reference render — marriage-tree forest (Horizontal × Paternal, 3-gen "Default" tree)

Validated against production (family-dev.goom.life, 2026-07-05). The tree is a
**forest of marriage trees**. Each marriage (or single-parent family) renders
exactly once as a couple/branch unit; a person who participates in several
marriages is **duplicated** into each one (`*` below, DOM id
`member-{id}-ref{n}`). Cards whose children render elsewhere get a
**▾ children** jump button.

Columns are the global visual-rank tracks (in Horizontal mode: same rank =
same X). A tree that starts at a later rank (e.g. the island tree T5) leaves
its earlier rank cells **empty** — empty cells are required to preserve the
rank alignment. Trees stack along the cross axis (downward in Horizontal,
rightward in Vertical).

```
| Tree | Rank 0                  | Rank 1               | Rank 1.5             | Rank 2           |
|------|-------------------------|----------------------|----------------------|------------------|
| T1   | Paternal Grandpa   =    |                      |                      |                  |
| T1   | Paternal Grandma   =    | Father          =    |                      |                  |
| T1   |                         | Mother          =    |                      | Me               |
| T1   |                         | Fathers Brother -----| FB Wife 1 -----------| Cousin 1         |
| T1   |                         |                      |                      | Cousin 2         |
| T1   |                         |                      | FB Wife 2 -----------| Cousin 3         |
| T2   | Maternal Grandpa 1 =    |                      |                      |                  |
| T2   | Maternal Grandma   =    | Mother* [v]          |                      |                  |
| T3   | Maternal Grandpa 2 =    | Mothers HalfSib -----| HalfSib Husband 1    |                  |
| T3   | Maternal Grandma*  =    |                      | HalfSib Husband 2    |                  |
| T4   | SingleOwnOther     =    |                      |                      |                  |
| T4   | SingleOwnWife      =    | SingleOwnChild       |                      |                  |
| T5   |                         |                      | FB Wife 2* [v] ------| Wife2 Only Child |
| T6   | SingleOwnHusband   =    |                      |                      |                  |
| T6   | SingleOwnWife*     =    |                      |                      |                  |
| T7   | Paternal Grandma Wife = |                      |                      |                  |
| T7   | Paternal Grandma*  =    |                      |                      |                  |
| T8   | Maternal Grandma Wife 1 |                      |                      |                  |
| T8   | Maternal Grandma*  =    |                      |                      |                  |
| T9   | Maternal Grandma Wife 2 |                      |                      |                  |
| T9   | Maternal Grandma*  =    |                      |                      |                  |
```

Legend: `=` couple link (stacked same-rank partners); `-----` parent→child /
half-rank spouse connector; `*` duplicated card; `[v]` ▾ children jump.

Forest construction rules (client, `family-tree.js` / `buildBranchBottomUp`):

1. **Marriage units.** Every partner pair (plus co-parent pairs) is a unit
   with its shared children. Children of only one in-tree parent form a
   single-parent unit.
2. **Nesting.** A unit nests under its dominant partner's *owned* card —
   their child slot (if they have parents in the tree) or the root tree they
   own. A spouse-slot or duplicate card owns nothing, so units anchored by
   such a person become **new root trees** with that person duplicated
   (Maternal Grandma in T3/T8/T9; FB Wife 2's island T5).
3. **Same-rank spouse** (integer = integer): stacked couple in one cell with
   a couple link. Only the first same-rank marriage stacks at an owned card;
   each additional one splits into its own couple tree with the shared
   person duplicated (T6–T9).
4. **Half-rank spouse**: renders as a branch head card in the half-rank
   column, children continuing to the next integer rank (FB wives, HalfSib
   husbands).
5. **Ordering.** Root trees: marriages with children first, then islands,
   then childless marriages (deterministic by member id). Within a stack the
   main (non-duplicate) card renders before the duplicate. "Me" gets no
   special placement — use **Jump to you** on the toolbar to pan/zoom to "Me".
