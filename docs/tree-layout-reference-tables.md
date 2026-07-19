# 3-Gen Test Tree — Layout Reference Tables

Persistent ASCII reference for the **3-Gen Test Tree** seed data. Each table lists
the marriage-tree forest (T1–T9) and which **visual rank slot** each member
occupies. Rank assignments come from server-side `TreeLayoutRanking`; tree
nesting follows production `family-tree.js` (`buildBranchBottomUp`).

Legend: `=` couple link; `-----` parent→child / half-rank connector; `*`
duplicated card; `[v]` ▾ children jump.

---

## Horizontal × Paternal

**On screen:** same rank = same **column** (X). Tree flows left → right.

```
| Tree | Rank 0                  | Rank 1               | Rank 1.5             | Rank 2           |
|------|-------------------------|----------------------|----------------------|------------------|
| T1   | Paternal Grandpa   =    |                      |                      |                  |
| T1   | Paternal Grandma   =    | Father          =    |                      |                  |
| T1   |                         | Mother          =    |                      | Me               |
| T1   |                         | Fathers Brother -----| FB Wife 1 -----------| Cousin 1         |
| T1   |                         |                      |                      | Cousin 2         |
| T1   |                         |                      | FB Wife 2 -----------| Cousin 3         |
| T1   |                         | Fathers Brother* ----|                      | FB Solo Child    |
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

`FB Solo Child` (76) is Fathers Brother's only child with no listed second
parent — a card-less `.ft-partner-unit-single`, rendered alongside his two
coupled `.ft-partner-unit`s. It must land in the same Rank 2 column/row as
Cousin 1/2/3 (see `family-tree.js` `alignSingleParentBranches`).

---

## Vertical × Paternal

**On screen:** same rank = same **row** (Y). Tree flows top → bottom.

Rank slots and marriage-tree membership are identical to Horizontal × Paternal;
only the screen axis mapping differs (columns become rows).

```
| Tree | Rank 0 (row)            | Rank 1               | Rank 1.5             | Rank 2           |
|------|-------------------------|----------------------|----------------------|------------------|
| T1   | Paternal Grandpa   =    |                      |                      |                  |
| T1   | Paternal Grandma   =    | Father          =    |                      |                  |
| T1   |                         | Mother          =    |                      | Me               |
| T1   |                         | Fathers Brother -----| FB Wife 1 -----------| Cousin 1         |
| T1   |                         |                      |                      | Cousin 2         |
| T1   |                         |                      | FB Wife 2 -----------| Cousin 3         |
| T1   |                         | Fathers Brother* ----|                      | FB Solo Child    |
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

---

## Horizontal × Maternal

**On screen:** same rank = same **column** (X). Female bloodline dominates
within couples; root-generation grandmas dominate their grandpas (vrank 0.5).

```
| Tree | Rank 0                  | Rank 0.5             | Rank 1               | Rank 1.5             | Rank 2           |
|------|-------------------------|----------------------|----------------------|----------------------|------------------|
| T1   | Maternal Grandma   =    | Maternal Grandpa 1   |                      |                      |                  |
| T1   |                         | Maternal Grandpa 2   |                      |                      |                  |
| T1   | Paternal Grandma   =    | Paternal Grandpa     |                      |                      |                  |
| T1   |                         |                      | Mother          =    |                      |                  |
| T1   |                         |                      | Father          =    |                      | Me               |
| T1   |                         |                      | Mothers HalfSib -----| HalfSib Husband 1    |                  |
| T1   |                         |                      |                      | HalfSib Husband 2    |                  |
| T1   |                         |                      | Fathers Brother -----| FB Wife 1 -----------| Cousin 1         |
| T1   |                         |                      |                      |                      | Cousin 2         |
| T1   |                         |                      |                      | FB Wife 2 -----------| Cousin 3         |
| T1   |                         |                      | Fathers Brother* ----|                      | FB Solo Child    |
| T2   | Maternal Grandma Wife 1 |                      |                      |                      |                  |
| T2   | Maternal Grandma*  =    |                      |                      |                      |                  |
| T3   | Maternal Grandma Wife 2 |                      |                      |                      |                  |
| T3   | Maternal Grandma*  =    |                      |                      |                      |                  |
| T4   | SingleOwnOther     =    |                      |                      |                      |                  |
| T4   | SingleOwnWife      =    |                      | SingleOwnChild       |                      |                  |
| T5   |                         |                      |                      | FB Wife 2* [v] ------| Wife2 Only Child |
| T6   | SingleOwnHusband   =    |                      |                      |                      |                  |
| T6   | SingleOwnWife*     =    |                      |                      |                      |                  |
| T7   | Paternal Grandma Wife = |                      |                      |                      |                  |
| T7   | Paternal Grandma*  =    |                      |                      |                      |                  |
```

---

## Vertical × Maternal

**On screen:** same rank = same **row** (Y). Rank slots match Horizontal ×
Maternal; axis mapping only differs.

```
| Tree | Rank 0 (row)            | Rank 0.5             | Rank 1               | Rank 1.5             | Rank 2           |
|------|-------------------------|----------------------|----------------------|----------------------|------------------|
| T1   | Maternal Grandma   =    | Maternal Grandpa 1   |                      |                      |                  |
| T1   |                         | Maternal Grandpa 2   |                      |                      |                  |
| T1   | Paternal Grandma   =    | Paternal Grandpa     |                      |                      |                  |
| T1   |                         |                      | Mother          =    |                      |                  |
| T1   |                         |                      | Father          =    |                      | Me               |
| T1   |                         |                      | Mothers HalfSib -----| HalfSib Husband 1    |                  |
| T1   |                         |                      |                      | HalfSib Husband 2    |                  |
| T1   |                         |                      | Fathers Brother -----| FB Wife 1 -----------| Cousin 1         |
| T1   |                         |                      |                      |                      | Cousin 2         |
| T1   |                         |                      |                      | FB Wife 2 -----------| Cousin 3         |
| T1   |                         |                      | Fathers Brother* ----|                      | FB Solo Child    |
| T2   | Maternal Grandma Wife 1 |                      |                      |                      |                  |
| T2   | Maternal Grandma*  =    |                      |                      |                      |                  |
| T3   | Maternal Grandma Wife 2 |                      |                      |                      |                  |
| T3   | Maternal Grandma*  =    |                      |                      |                      |                  |
| T4   | SingleOwnOther     =    |                      |                      |                      |                  |
| T4   | SingleOwnWife      =    |                      | SingleOwnChild       |                      |                  |
| T5   |                         |                      |                      | FB Wife 2* [v] ------| Wife2 Only Child |
| T6   | SingleOwnHusband   =    |                      |                      |                      |                  |
| T6   | SingleOwnWife*     =    |                      |                      |                      |                  |
| T7   | Paternal Grandma Wife = |                      |                      |                      |                  |
| T7   | Paternal Grandma*  =    |                      |                      |                      |                  |
```

---

---

## Half-Sibling Alignment Tree (tree ID 5)

Isolated regression fixture for the production Ray/Eve/Gideon misalignment bug — see
[`database-setup.md`](database-setup.md) for seed details. Same rank slot
(Rank 1) in both orientations; no half-ranks exist anywhere in this tree, so
`insertHalfRankSpacers` in `family-tree.js` never activates and only
`alignSingleParentBranches` can keep `HS Half Child` aligned with its full siblings.

```
| Rank 0                | Rank 1        |
|-----------------------|----------------|
| HS Father = HS Mother | HS Child A     |
|                       | HS Child B     |
| HS Father* -----------| HS Half Child  |
```

`*` = duplicated card (HS Father rendered again as the sole parent of the card-less
`.ft-partner-unit-single` holding `HS Half Child`, alongside his coupled `.ft-partner-unit`
with HS Mother holding Child A/B).

---

See also [`tree-layout-semantics.md`](tree-layout-semantics.md) for dominance
rules and forest construction.
