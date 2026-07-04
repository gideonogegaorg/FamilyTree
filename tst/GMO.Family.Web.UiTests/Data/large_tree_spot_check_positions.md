# Large Tree spot-check positions (all 4 combos, with half-ranks)

Collected from live app via Playwright. Flow axis: **Vertical => Y**, **Horizontal => X**.  
Names in **visual rank order** (Paternal: Gen0A, Gen0B, Gen1a, Gen1n, …; Maternal: Gen0B, Gen0A, Gen1n, Gen1a, …).

---

## Vertical, Paternal (flow axis = Y)

| Name  | data-visual-rank | x   | y   |
|-------|------------------|-----|-----|
| Gen0A | 0                | 1112| 105 |
| Gen0B | 0                | 1296| 105 |
| Gen1a | 1                | 428 | 241 |
| Gen1n | 1                | 612 | 241 |
| Gen2a | 2                | 332 | 377 |
| Gen2l | 2                | 516 | 377 |
| Gen3a | 3                | 236 | 513 |
| Gen3n | 3.5              | 1332| 785 |
| Gen4a | 4                | 140 | 649 |
| Gen4k | 4                | 324 | 649 |
| Gen5a | 5                | 136 | 785 |

**Y (flow) by rank order:** 105 → 105 → 241 → 241 → 377 → 377 → 513 → 785 → 649 → 649 → 785. Distinct rank levels (0, 1, 2, 3, 3.5, 4, 5) advance down the page (Y increasing).

---

## Vertical, Maternal (flow axis = Y)

| Name  | data-visual-rank | x   | y   |
|-------|------------------|-----|-----|
| Gen0B | 0                | 1112| 105 |
| Gen0A | 0                | 1296| 105 |
| Gen1n | 1                | 612 | 241 |
| Gen1a | 1                | 428 | 241 |
| Gen2l | 2                | 516 | 377 |
| Gen2a | 2                | 332 | 377 |
| Gen3a | 3                | 236 | 513 |
| Gen3n | 3.5              | 1332| 785 |
| Gen4k | 4                | 324 | 649 |
| Gen4a | 4                | 140 | 649 |
| Gen5a | 5                | 136 | 785 |

**Y (flow) by rank order:** same pattern as Vertical Paternal (couples share Y; rank levels increase down).

---

## Horizontal, Paternal (flow axis = X)

| Name  | data-visual-rank | x    | y   |
|-------|------------------|------|-----|
| Gen0A | 0                | 40   | 553 |
| Gen0B | 0                | 40   | 633 |
| Gen1a | 1                | 272  | 273 |
| Gen1n | 1                | 272  | 353 |
| Gen2a | 2                | 504  | 233 |
| Gen2l | 2                | 504  | 313 |
| Gen3a | 3                | 736  | 193 |
| Gen3n | 3.5              | 1108 | 649 |
| Gen4a | 4                | 968  | 153 |
| Gen4k | 4                | 968  | 233 |
| Gen5a | 5                | 1200 | 153 |

**X (flow) by rank order:** 40 → 40 → 272 → 272 → 504 → 504 → 736 → 1108 → 968 → 968 → 1200. Couples share X; rank levels advance right (X increasing).

---

## Horizontal, Maternal (flow axis = X)

| Name  | data-visual-rank | x    | y   |
|-------|------------------|------|-----|
| Gen0B | 0                | 40   | 553 |
| Gen0A | 0                | 40   | 633 |
| Gen1n | 1                | 458  | 353 |
| Gen1a | 1                | 458  | 273 |
| Gen2l | 2                | 876  | 313 |
| Gen2a | 2                | 876  | 233 |
| Gen3a | 3                | 1294 | 193 |
| Gen3n | 3.5              | 1480 | 649 |
| Gen4k | 4                | 1712 | 233 |
| Gen4a | 4                | 1712 | 153 |
| Gen5a | 5                | 1944 | 153 |

**X (flow) by rank order:** 40 → 40 → 458 → 458 → 876 → 876 → 1294 → 1480 → 1712 → 1712 → 1944.

---

## Test fix

- Use **Paternal list** (11 names): `Gen0A`, `Gen0B`, `Gen1a`, `Gen1n`, `Gen2a`, `Gen2l`, `Gen3a`, `Gen3n`, `Gen4a`, `Gen4k`, `Gen5a`.
- Use **Maternal list** (11 names): `Gen0B`, `Gen0A`, `Gen1n`, `Gen1a`, `Gen2l`, `Gen2a`, `Gen3a`, `Gen3n`, `Gen4k`, `Gen4a`, `Gen5a`.
- For the chosen list, read each card’s **flow-axis** coord (X for Horizontal, Y for Vertical).
- Assert **strict monotonicity** of flow-axis coords in list order: either all `flowCoords[i] <= flowCoords[i+1]` with at least one strict increase between rank levels, or require that when sorted by `data-visual-rank`, flow-axis values are strictly increasing (couples share the same flow position, so consecutive pairs can be equal).
- **Card resolution:** use main card only (id `member-{id}`), not `-ref` / `-leaf`.
- **Viewport:** e.g. 1280×720; scroll each card into view before measuring.
- **Timing:** wait for graph layout to settle before measuring.
