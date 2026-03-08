-- 3-Generation Test Tree (tree 9): orientation + Paternal/Maternal layout testing.
-- Covers: multi-partner primary (Paternal + Maternal), half-siblings, single parent,
--         children with both parents vs one parent only.
-- Run with: psql -h localhost -p 5432 -U family -d family -f working/seed_3gen.sql
-- Idempotent: safe to re-run.

-- Tree 9
INSERT INTO "FamilyTrees" ("Id", "Uid", "Name", "OwnerId")
SELECT 9, gen_random_uuid(), '3-Gen Test Tree', "Id" FROM "AspNetUsers" LIMIT 1
ON CONFLICT ("Id") DO NOTHING;

-- Members 50-65 (16 total): Gen1=5, Gen2=6, Gen3=5
-- 50=Paternal Grandma(f), 51=Paternal Grandpa(m), 52=Maternal Grandma(f), 53=Maternal Grandpa 1(m), 54=Maternal Grandpa 2(m)
-- 55=Father(m), 56=Me(m), 57=Mother(f), 58=Father's Brother(m), 59=FB Wife 1(f), 60=FB Wife 2(f), 61=Mother's Half-Sibling(m), 62=Cousin 1, 63=Cousin 2, 64=Cousin 3, 65=Wife2 Only Child
INSERT INTO "FamilyMembers" ("Id", "FamilyTreeId", "Name", "IsMale", "UserId")
OVERRIDING SYSTEM VALUE
SELECT v.id, 9::bigint, v.name, v.ismale, NULL::character varying
FROM (VALUES
  (50::bigint, 'Paternal Grandma', false),
  (51, 'Paternal Grandpa', true),
  (52, 'Maternal Grandma', false),
  (53, 'Maternal Grandpa 1', true),
  (54, 'Maternal Grandpa 2', true),
  (55, 'Father', true),
  (56, 'Me', true),
  (57, 'Mother', false),
  (58, 'Fathers Brother', true),
  (59, 'FB Wife 1', false),
  (60, 'FB Wife 2', false),
  (61, 'Mothers HalfSib', true),
  (62, 'Cousin 1', true),
  (63, 'Cousin 2', true),
  (64, 'Cousin 3', true),
  (65, 'Wife2 Only Child', true),
  (66, 'HalfSib Husband 1', true),
  (67, 'HalfSib Husband 2', true),
  (68, 'Paternal Grandma Wife', false),
  (69, 'Maternal Grandma Wife 1', false),
  (70, 'Maternal Grandma Wife 2', false)
) AS v(id, name, ismale)
WHERE NOT EXISTS (SELECT 1 FROM "FamilyMembers" m WHERE m."FamilyTreeId" = 9 AND m."Id" = v.id);

-- Link "Me" (56) to first user
UPDATE "FamilyMembers" SET "UserId" = (SELECT "Id" FROM "AspNetUsers" LIMIT 1) WHERE "Id" = 56 AND "FamilyTreeId" = 9;

-- Relationships: Parent = 0, Couple = 2. Idempotent via ON CONFLICT.
-- Couples
INSERT INTO "FamilyMemberRelationships" ("FamilyTreeId", "FromMemberId", "ToMemberId", "RelationshipType")
SELECT 9, a, b, 2 FROM (VALUES (50,51), (52,53), (52,54), (55,57), (58,59), (58,60), (61,66), (61,67), (50,68), (52,69), (52,70)) AS v(a,b)
ON CONFLICT ("FromMemberId", "ToMemberId", "RelationshipType") DO NOTHING;

-- Parents (From=parent, To=child)
INSERT INTO "FamilyMemberRelationships" ("FamilyTreeId", "FromMemberId", "ToMemberId", "RelationshipType")
SELECT 9, parent, child, 0
FROM (VALUES
  (50, 55), (51, 55),
  (50, 58), (51, 58),
  (52, 57), (53, 57),
  (52, 61), (54, 61),
  (55, 56), (57, 56),
  (58, 62), (59, 62),
  (58, 63), (59, 63),
  (58, 64), (60, 64),
  (60, 65)
) AS v(parent, child)
ON CONFLICT ("FromMemberId", "ToMemberId", "RelationshipType") DO NOTHING;

-- Advance sequence
SELECT setval(pg_get_serial_sequence('"FamilyMembers"', 'Id'), (SELECT COALESCE(MAX("Id"), 1) FROM "FamilyMembers"));
