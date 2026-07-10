-- Seed trees: 3-gen test, empty, single-member, and large (6-gen) trees.
-- Run with: psql -h localhost -p 5432 -U family -d family -f tst/GMO.FamilyTree.Web.UiTests/Data/seed_trees.sql
-- Idempotent: safe to re-run.

-- ========== Variables (change these for a different environment) ==========
CREATE TEMP TABLE IF NOT EXISTS _seed_vars (key text PRIMARY KEY, val text);
DELETE FROM _seed_vars WHERE key IN ('tree_primary', 'tree_empty', 'tree_single', 'tree_large', 'owner_id');
INSERT INTO _seed_vars (key, val) VALUES
  ('tree_primary', '1'),
  ('tree_empty', '2'),
  ('tree_single', '3'),
  ('tree_large', '4');
-- Owner: prefer test user so seed trees appear for test@example.com; else first AspNetUser.
INSERT INTO _seed_vars (key, val)
SELECT 'owner_id', COALESCE(
  (SELECT "Id" FROM "AspNetUsers" WHERE "Email" = 'test@example.com' LIMIT 1),
  (SELECT "Id" FROM "AspNetUsers" LIMIT 1)
);
-- Or set explicitly: INSERT INTO _seed_vars (key, val) VALUES ('owner_id', 'your-aspnet-user-id-here');
-- ========== End variables ==========

-- Tree: primary (3-gen), empty, single-member
INSERT INTO "FamilyTrees" ("Id", "Uid", "Name", "OwnerId")
SELECT (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_primary'), gen_random_uuid(), '3-Gen Test Tree', (SELECT val FROM _seed_vars WHERE key = 'owner_id')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "FamilyTrees" ("Id", "Uid", "Name", "OwnerId")
SELECT (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_empty'), gen_random_uuid(), 'Empty Tree', (SELECT val FROM _seed_vars WHERE key = 'owner_id')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "FamilyTrees" ("Id", "Uid", "Name", "OwnerId")
SELECT (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_single'), gen_random_uuid(), 'Single Member Tree', (SELECT val FROM _seed_vars WHERE key = 'owner_id')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "FamilyTrees" ("Id", "Uid", "Name", "OwnerId")
SELECT (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_large'), gen_random_uuid(), 'Large Tree (6 Gen)', (SELECT val FROM _seed_vars WHERE key = 'owner_id')
ON CONFLICT ("Id") DO NOTHING;

-- Keep ownership in sync on re-run (idempotent)
UPDATE "FamilyTrees" SET "OwnerId" = (SELECT val FROM _seed_vars WHERE key = 'owner_id')
WHERE "Id" IN (
  (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_primary'),
  (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_empty'),
  (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_single'),
  (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_large')
);

-- ========== 3-Gen primary tree members ==========
INSERT INTO "FamilyMembers" ("Id", "FamilyTreeId", "Name", "IsMale", "UserId")
OVERRIDING SYSTEM VALUE
SELECT v.id, (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_primary'), v.name, v.ismale, NULL::character varying
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
  (70, 'Maternal Grandma Wife 2', false),
  (71, 'SingleOwnWife', false),
  (72, 'SingleOwnHusband', true),
  (73, 'SingleOwnOther', true),
  (74, 'SingleOwnChild', true)
) AS v(id, name, ismale)
WHERE NOT EXISTS (SELECT 1 FROM "FamilyMembers" m WHERE m."FamilyTreeId" = (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_primary') AND m."Id" = v.id);

-- Single-member tree: one member, no relationships
INSERT INTO "FamilyMembers" ("Id", "FamilyTreeId", "Name", "IsMale", "UserId")
OVERRIDING SYSTEM VALUE
SELECT 75, (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_single'), 'Lone Member', true, NULL
WHERE NOT EXISTS (SELECT 1 FROM "FamilyMembers" m WHERE m."FamilyTreeId" = (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_single') AND m."Id" = 75);

-- Link "Me" (56) to owner; set DOB for createCard branch coverage
UPDATE "FamilyMembers" SET "UserId" = (SELECT val FROM _seed_vars WHERE key = 'owner_id'), "DOB" = '1990-01-15' WHERE "Id" = 56 AND "FamilyTreeId" = (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_primary');

-- 3-Gen: Relationships
INSERT INTO "FamilyMemberRelationships" ("FamilyTreeId", "FromMemberId", "ToMemberId", "RelationshipType")
SELECT (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_primary'), a, b, 2 FROM (VALUES (50,51), (52,53), (52,54), (55,57), (58,59), (58,60), (61,66), (61,67), (50,68), (52,69), (52,70)) AS v(a,b)
ON CONFLICT ("FromMemberId", "ToMemberId", "RelationshipType") DO NOTHING;

INSERT INTO "FamilyMemberRelationships" ("FamilyTreeId", "FromMemberId", "ToMemberId", "RelationshipType")
SELECT (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_primary'), parent, child, 0
FROM (VALUES
  (50, 55), (51, 55), (50, 58), (51, 58),
  (52, 57), (53, 57), (52, 61), (54, 61),
  (55, 56), (57, 56),
  (58, 62), (59, 62), (58, 63), (59, 63), (58, 64), (60, 64), (60, 65),
  (71, 74), (73, 74)
) AS v(parent, child)
ON CONFLICT ("FromMemberId", "ToMemberId", "RelationshipType") DO NOTHING;

INSERT INTO "FamilyMemberRelationships" ("FamilyTreeId", "FromMemberId", "ToMemberId", "RelationshipType")
SELECT (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_primary'), a, b, 2 FROM (VALUES (71, 72), (71, 73)) AS v(a, b)
ON CONFLICT ("FromMemberId", "ToMemberId", "RelationshipType") DO NOTHING;

-- ========== Large tree (6 generations): varied siblings, half/step, multiple partners, same-sex ==========
INSERT INTO "FamilyMembers" ("Id", "FamilyTreeId", "Name", "IsMale", "UserId")
OVERRIDING SYSTEM VALUE
SELECT v.id, (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_large'), v.name, v.ismale, NULL
FROM (VALUES
  (100::bigint, 'Gen0A', true),
  (101, 'Gen0B', false),
  (102, 'Gen0C', false),
  (103, 'Gen0D', true),
  (104, 'Gen0E', true),
  (105, 'Gen0F', true),
  (106, 'Gen0G', true),
  (107, 'Gen0H', false),
  (108, 'Gen0I', true),
  (109, 'Gen0J', false),
  (110, 'Gen1a', true),
  (111, 'Gen1b', false),
  (112, 'Gen1c', true),
  (113, 'Gen1d', false),
  (114, 'Gen1e', true),
  (115, 'Gen1f', false),
  (116, 'Gen1g', true),
  (117, 'Gen1h', false),
  (118, 'Gen1i', false),
  (119, 'Gen1j', true),
  (120, 'Gen1k', false),
  (121, 'Gen1l', true),
  (122, 'Gen1m', true),
  (123, 'Gen1n', false),
  (124, 'Gen1o', true),
  (125, 'Gen1p', false),
  (126, 'Gen1q', false),
  (127, 'Gen1r', false),
  (128, 'Gen1s', true),
  (129, 'Gen1t', true),
  (130, 'Gen2a', true),
  (131, 'Gen2b', false),
  (132, 'Gen2c', true),
  (133, 'Gen2d', false),
  (134, 'Gen2e', true),
  (135, 'Gen2f', true),
  (136, 'Gen2g', true),
  (137, 'Gen2h', false),
  (138, 'Gen2i', false),
  (139, 'Gen2j', false),
  (140, 'Gen2k', true),
  (141, 'Gen2l', false),
  (142, 'Gen2m', false),
  (143, 'Gen2n', true),
  (144, 'Gen2o', true),
  (145, 'Gen2p', true),
  (146, 'Gen2q', true),
  (147, 'Gen2r', false),
  (148, 'Gen2s', false),
  (160, 'Gen3a', false),
  (161, 'Gen3b', true),
  (162, 'Gen3c', false),
  (163, 'Gen3d', true),
  (164, 'Gen3e', false),
  (165, 'Gen3f', true),
  (166, 'Gen3g', true),
  (167, 'Gen3h', false),
  (168, 'Gen3i', true),
  (169, 'Gen3j', false),
  (170, 'Gen3k', true),
  (171, 'Gen3l', true),
  (172, 'Gen3m', false),
  (173, 'Gen3n', true),
  (174, 'Gen3o', false),
  (175, 'Gen3p', true),
  (176, 'Gen3q', false),
  (177, 'Gen3r', true),
  (180, 'Gen4a', true),
  (181, 'Gen4b', false),
  (182, 'Gen4c', true),
  (183, 'Gen4d', false),
  (184, 'Gen4e', true),
  (185, 'Gen4f', false),
  (186, 'Gen4g', true),
  (187, 'Gen4h', true),
  (188, 'Gen4i', false),
  (189, 'Gen4j', true),
  (190, 'Gen4k', false),
  (191, 'Gen4l', false),
  (192, 'Gen4m', true),
  (200, 'Gen5a', true),
  (201, 'Gen5b', false),
  (202, 'Gen5c', true),
  (203, 'Gen5d', false),
  (204, 'Gen5e', true),
  (205, 'Gen5f', false),
  (206, 'Gen5g', true),
  (207, 'Gen5h', false),
  (208, 'Gen3extra', true)
) AS v(id, name, ismale)
WHERE NOT EXISTS (SELECT 1 FROM "FamilyMembers" m WHERE m."FamilyTreeId" = (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_large') AND m."Id" = v.id);

-- Large tree: couples (Gen0: traditional, multi-partner, same-sex; single parent 107)
INSERT INTO "FamilyMemberRelationships" ("FamilyTreeId", "FromMemberId", "ToMemberId", "RelationshipType")
SELECT (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_large'), a, b, 2
FROM (VALUES (100,101), (102,103), (102,104), (105,106), (108,109),
  (110,123), (111,124), (111,125), (113,126), (116,127), (118,128), (118,129), (122,140),
  (130,141), (132,142), (135,143), (135,144), (136,145), (136,146), (138,147), (171,177),
  (160,172), (162,173), (162,174), (163,175), (166,176), (187,192),
  (180,190), (182,191)) AS v(a,b)
ON CONFLICT ("FromMemberId", "ToMemberId", "RelationshipType") DO NOTHING;

-- Large tree: parents (RelationshipType 0 = parent; From=parent, To=child)
INSERT INTO "FamilyMemberRelationships" ("FamilyTreeId", "FromMemberId", "ToMemberId", "RelationshipType")
SELECT (SELECT val::bigint FROM _seed_vars WHERE key = 'tree_large'), parent, child, 0
FROM (VALUES
  (100,110), (101,110), (100,111), (101,111), (100,112), (101,112),
  (102,113), (103,113), (102,114), (104,114), (102,115), (104,115),
  (105,116), (106,116), (105,117), (106,117),
  (107,118), (107,119), (107,120), (107,121),
  (108,122), (109,122),
  (110,130), (123,130), (110,131), (123,131),
  (111,132), (124,132), (111,133), (125,133), (111,134), (125,134),
  (113,135), (126,135),
  (116,136), (127,136), (116,137), (127,137),
  (118,138), (128,138), (118,139), (129,139),
  (122,171), (140,171),
  (130,160), (141,160), (130,161), (141,161),
  (132,162), (142,162),
  (135,163), (143,163), (135,164), (144,164), (135,165), (144,165),
  (136,166), (145,166), (136,167), (146,167),
  (138,168), (147,168), (138,169), (147,169), (138,170), (147,170), (138,208), (147,208),
  (171,187), (177,187), (171,188), (177,188), (171,189), (177,189),
  (160,180), (172,180), (160,181), (172,181),
  (162,182), (173,182), (162,183), (174,183),
  (163,184), (175,184),
  (166,185), (176,185), (166,186), (176,186),
  (187,203), (192,203), (187,204), (192,204), (187,205), (192,205), (187,206), (192,206), (187,207), (192,207),
  (180,200), (190,200), (180,201), (190,201),
  (182,202), (191,202)
) AS v(parent, child)
ON CONFLICT ("FromMemberId", "ToMemberId", "RelationshipType") DO NOTHING;

-- Advance identity sequences after explicit Id inserts (keeps new registrations/trees from colliding).
SELECT setval(pg_get_serial_sequence('"FamilyTrees"', 'Id'), COALESCE((SELECT MAX("Id") FROM "FamilyTrees"), 1));
SELECT setval(pg_get_serial_sequence('"FamilyMembers"', 'Id'), COALESCE((SELECT MAX("Id") FROM "FamilyMembers"), 1));
