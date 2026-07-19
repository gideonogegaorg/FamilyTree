(function () {
    'use strict';

    var container = document.getElementById('family-tree-graph');
    if (!container) return;

    var isDemo = container.getAttribute('data-demo') === 'true';
    var canEdit = !isDemo && container.getAttribute('data-can-edit') !== 'false';

    var orientation = (container.getAttribute('data-orientation') || 'Horizontal').toString();
    if (orientation === 'Horizontal' || orientation === '0') container.classList.add('ft-orientation-horizontal');

    var rawNodes = JSON.parse(container.getAttribute('data-nodes') || '[]');
    var rawEdges = JSON.parse(container.getAttribute('data-edges') || '[]');

    var lineageMode = (container.getAttribute('data-lineage-mode') || 'Paternal').toString();
    var isPaternal = lineageMode !== '1' && lineageMode !== 'Maternal';
    function isPrimary(node) { return isPaternal ? node.isMale : !node.isMale; }

    var depthById = null; // Will be set later
    function dominates(nodeA, nodeB) {
        if (!nodeA) return false;
        if (!nodeB) return true;

        // 1. Depth: nodes connected to the root bloodline dominate those inserted via marriage
        var depthA = depthById ? (depthById[nodeA.id] || 0) : 0;
        var depthB = depthById ? (depthById[nodeB.id] || 0) : 0;
        if (depthA > 0 && depthB === 0) return true;
        if (depthA === 0 && depthB > 0) return false;

        // 2. Fallback: Paternal vs Maternal mode logic
        return isPrimary(nodeA) && !isPrimary(nodeB);
    }

    function toId(x) { return typeof x === 'number' ? x : parseInt(x, 10); }
    var nodeById = {};
    rawNodes.forEach(function (n) {
        var id = toId(n.id);
        nodeById[id] = {
            id: id,
            name: (n.name || '').toString(),
            nickName: (n.nickName || '').toString(),
            label: (n.label || '').toString(),
            isMe: !!n.isMe,
            isMale: !!n.isMale,
            dob: n.dob,
            dod: n.dod,
            birthOrder: n.birthOrder != null ? toId(n.birthOrder) : null,
            visualRank: n.visualRank != null ? parseFloat(n.visualRank) : 0,
            parentIds: (n.parentIds || []).map(toId),
            childIds: (n.childIds || []).map(toId),
            partnerIds: (n.partnerIds || []).map(toId),
            hasPhoto: !!n.hasPhoto,
            photoUrl: n.photoUrl ? n.photoUrl.toString() : null
        };
    });

    function buildFamilyGroups() {
        var groups = {};
        rawNodes.forEach(function (n) {
            var node = nodeById[toId(n.id)];
            if (!node || node.parentIds.length === 0) return;
            var key = node.parentIds.slice().sort(function (a, b) { return a - b; }).join('-');
            if (!groups[key]) {
                groups[key] = { parentIds: node.parentIds.slice().sort(function (a, b) { return a - b; }), childIds: [] };
            }
            groups[key].childIds.push(node.id);
        });
        Object.keys(groups).forEach(function (key) {
            groups[key].childIds.sort(function (a, b) {
                var na = nodeById[a], nb = nodeById[b];
                var oa = (na && na.birthOrder) || 9999;
                var ob = (nb && nb.birthOrder) || 9999;
                return (oa - ob) || (a - b);
            });
        });
        return groups;
    }

    var familyGroups = buildFamilyGroups();

    function getPartnerFamilies(memberId) {
        var families = [];
        var node = nodeById[memberId];
        Object.keys(familyGroups).forEach(function (key) {
            var g = familyGroups[key];
            if (g.parentIds.indexOf(memberId) >= 0) {
                var partnerId = null;
                g.parentIds.forEach(function (pid) { if (pid !== memberId) partnerId = pid; });
                families.push({ partnerId: partnerId, childIds: g.childIds });
            }
        });
        if (node && node.partnerIds && node.partnerIds.length > 0) {
            var seenPartner = {};
            families.forEach(function (f) { if (f.partnerId != null) seenPartner[f.partnerId] = true; });
            node.partnerIds.forEach(function (pid) {
                if (!seenPartner[pid]) {
                    families.push({ partnerId: pid, childIds: [] });
                    seenPartner[pid] = true;
                }
            });
        }
        if (node && families.length > 1) {
            families = families.map(function (fam) {
                var partner = fam.partnerId ? nodeById[fam.partnerId] : null;
                if (partner && dominates(partner, node)) {
                    return { partnerId: fam.partnerId, childIds: [] };
                }
                return fam;
            });
        }
        return families;
    }

    function findRoots() {
        var roots = [];
        rawNodes.forEach(function (n) {
            var node = nodeById[n.id];
            if (node && node.parentIds.length === 0) roots.push(node.id);
        });
        return roots;
    }

    function getAncestorIds(memberId) {
        var node = nodeById[memberId];
        if (!node || node.parentIds.length === 0) return [];
        var out = [];
        var seen = {};
        function add(id) {
            if (seen[id]) return;
            seen[id] = true;
            out.push(id);
            var n = nodeById[id];
            if (n) n.parentIds.forEach(add);
        }
        node.parentIds.forEach(add);
        return out;
    }

    function rootOrderDepth(rootId, rootSet) {
        var node = nodeById[rootId];
        if (!node) return 0;
        var ancestorSet = {};
        getAncestorIds(rootId).forEach(function (aid) { ancestorSet[aid] = true; });
        node.partnerIds.forEach(function (pid) {
            getAncestorIds(pid).forEach(function (aid) { ancestorSet[aid] = true; });
        });
        var count = 0;
        Object.keys(ancestorSet).forEach(function (aid) {
            if (rootSet[aid] && aid !== rootId) count++;
        });
        return count;
    }

    function getDescendantIds(memberId) {
        var out = [];
        Object.keys(familyGroups).forEach(function (key) {
            var g = familyGroups[key];
            if (g.parentIds.indexOf(memberId) >= 0) {
                g.childIds.forEach(function (cid) {
                    out.push(cid);
                    getDescendantIds(cid).forEach(function (d) { out.push(d); });
                });
            }
        });
        return out;
    }

    function clusterHasPrimaryDescendant(cluster) {
        return cluster.some(function (rid) {
            return getDescendantIds(rid).some(function (did) {
                var n = nodeById[did];
                return n && isPrimary(n);
            });
        });
    }

    function initials(name) {
        var parts = String(name || '').split(/\s+/).filter(Boolean);
        if (parts.length >= 2) {
            return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
        }
        return parts[0] ? parts[0].slice(0, 2).toUpperCase() : '?';
    }

    /** Prefer first nickname word for avatar letters; else legal name (not "Name (Nick)"). */
    function avatarInitials(node) {
        var nick = (node && node.nickName) ? String(node.nickName).trim() : '';
        if (nick) {
            var firstNick = nick.split(/\s+/).filter(Boolean)[0];
            return initials(firstNick || nick);
        }
        var name = (node && node.name) ? String(node.name).trim() : '';
        if (name) return initials(name);
        var label = (node && node.label) ? String(node.label).trim() : '';
        var withoutParen = label.replace(/\s*\([^)]*\)\s*$/, '').trim();
        return initials(withoutParen || label);
    }

    function dateYear(date) {
        if (!date) return null;
        var s = String(date);
        return s.length >= 4 ? s.substring(0, 4) : s;
    }

    function createSeeChildrenButton(scrollToElementId) {
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'ft-expand-down btn btn-sm btn-outline-secondary';
        btn.setAttribute('aria-label', 'See children');
        btn.textContent = '\u25BE children';
        btn.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            focusMemberCard(scrollToElementId);
        });
        return btn;
    }

    function focusMemberCard(elementId) {
        if (window.FamilyTreeViewport) {
            var memberId = elementId.replace(/^member-/, '');
            if (window.FamilyTreeViewport.focusMember(memberId)) return;
        }
        var el = document.getElementById(elementId);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    function createCard(node, idSuffix) {
        var card = document.createElement('div');
        card.className = 'family-tree-card' + (node.isMe ? ' family-tree-card-me' : '');
        card.id = idSuffix ? 'member-' + node.id + '-' + idSuffix : 'member-' + node.id;
        card.setAttribute('data-member-id', node.id);
        card.setAttribute('data-visual-rank', node.visualRank);
        card.setAttribute('aria-label', node.label);

        var content = document.createElement('div');
        content.className = 'family-tree-card-content';

        var avatar = document.createElement(isDemo ? 'div' : 'button');
        if (!isDemo) avatar.type = 'button';
        avatar.className = 'family-tree-card-avatar' + (isDemo ? '' : ' member-details-trigger');
        if (!isDemo) {
            if (canEdit) {
                avatar.classList.add('member-photo-trigger');
                avatar.setAttribute('aria-label', 'Add or change picture for ' + node.label);
                avatar.title = 'Add or change picture';
            } else {
                avatar.setAttribute('aria-label', 'View details for ' + node.label);
            }
        } else {
            avatar.setAttribute('aria-hidden', 'true');
        }
        if (node.photoUrl) {
            var img = document.createElement('img');
            img.src = node.photoUrl;
            img.alt = '';
            img.className = 'family-tree-card-photo';
            avatar.appendChild(img);
        } else if (node.hasPhoto) {
            var img = document.createElement('img');
            img.src = '/photos/members/' + node.id;
            img.alt = '';
            img.className = 'family-tree-card-photo';
            avatar.appendChild(img);
        } else {
            avatar.textContent = avatarInitials(node);
        }

        var text = document.createElement('div');
        text.className = 'family-tree-card-text';

        var nameEl = document.createElement('div');
        nameEl.className = 'family-tree-card-name';
        nameEl.textContent = node.label;
        nameEl.title = node.label;
        text.appendChild(nameEl);

        var birth = dateYear(node.dob);
        var death = dateYear(node.dod);
        if (birth || death) {
            var meta = document.createElement('div');
            meta.className = 'family-tree-card-meta';
            meta.textContent = birth && death
                ? birth + '\u2013' + death
                : birth ? 'b. ' + birth : 'd. ' + death;
            text.appendChild(meta);
        }

        if (node.isMe) {
            var meBadge = document.createElement('span');
            meBadge.className = 'family-tree-card-you-badge';
            meBadge.textContent = 'You';
            text.appendChild(meBadge);
        }

        content.appendChild(avatar);
        content.appendChild(text);
        card.appendChild(content);

        if (!isDemo && canEdit) {
            var trigger = document.createElement('button');
            trigger.type = 'button';
            trigger.className = 'member-action-trigger btn btn-sm btn-link p-0';
            trigger.setAttribute('data-member-id', node.id);
            trigger.setAttribute('aria-label', 'Actions');
            trigger.innerHTML = '\u22EE';
            card.appendChild(trigger);
        }
        return card;
    }

    function memberPhotoUrl(memberId) {
        return '/photos/members/' + toId(memberId) + '?t=' + Date.now();
    }

    function findMemberCards(memberId) {
        var id = toId(memberId);
        var cards = [];
        container.querySelectorAll('.family-tree-card').forEach(function (card) {
            if (toId(card.getAttribute('data-member-id')) === id) cards.push(card);
        });
        container.querySelectorAll('.family-tree-card[id="member-' + id + '"], .family-tree-card[id^="member-' + id + '-"]').forEach(function (card) {
            if (cards.indexOf(card) < 0) cards.push(card);
        });
        return cards;
    }

    function setCardAvatar(avatar, hasPhoto, memberId) {
        if (!avatar) return;
        var node = nodeById[toId(memberId)];
        avatar.innerHTML = '';
        if (hasPhoto) {
            var img = document.createElement('img');
            img.src = memberPhotoUrl(memberId);
            img.alt = '';
            img.className = 'family-tree-card-photo';
            avatar.appendChild(img);
        } else {
            avatar.textContent = node ? avatarInitials(node) : '?';
        }
    }

    function updateMemberPhoto(memberId, hasPhoto, sourceCard) {
        var id = toId(memberId);
        if (nodeById[id]) nodeById[id].hasPhoto = !!hasPhoto;

        var cards = findMemberCards(id);
        if (sourceCard && cards.indexOf(sourceCard) < 0) cards.push(sourceCard);

        cards.forEach(function (card) {
            setCardAvatar(card.querySelector('.family-tree-card-avatar'), hasPhoto, id);
        });
    }

    function updateMemberEditPhotoPreview(form, memberId, hasPhoto) {
        if (!form) return;
        var wrap = form.querySelector('.member-photo-preview-wrap');
        var preview = form.querySelector('.member-edit-photo-preview');
        var removeBtn = form.querySelector('.member-photo-remove-btn');
        if (!wrap || !preview) return;
        if (hasPhoto) {
            preview.src = memberPhotoUrl(memberId);
            wrap.classList.remove('d-none');
            if (removeBtn) removeBtn.classList.remove('d-none');
        } else {
            wrap.classList.add('d-none');
            if (removeBtn) removeBtn.classList.add('d-none');
        }
    }

    window.FamilyTreePhotos = {
        updateMemberPhoto: updateMemberPhoto
    };

    function computeDepthById() {
        var depthById = {};
        var maxIter = rawNodes.length + 1;
        while (maxIter--) {
            var changed = false;
            rawNodes.forEach(function (n) {
                var id = toId(n.id);
                var node = nodeById[id];
                if (!node) return;
                if (node.parentIds.length === 0) {
                    if (depthById[id] === undefined) { depthById[id] = 0; changed = true; }
                    return;
                }
                var parentDepths = node.parentIds.map(function (pid) { return depthById[pid]; }).filter(function (d) { return d !== undefined; });
                if (parentDepths.length === node.parentIds.length) {
                    var d = 1 + Math.max.apply(null, parentDepths);
                    if (depthById[id] !== d) { depthById[id] = d; changed = true; }
                }
            });
            if (!changed) break;
        }
        rawNodes.forEach(function (n) {
            var id = toId(n.id);
            if (depthById[id] === undefined) depthById[id] = 0;
        });
        return depthById;
    }

    var rendered = {};

    function createLeafJumpBranch(node, scrollToMemberId) {
        var branch = document.createElement('div');
        branch.className = 'ft-branch ft-branch-leaf';
        var card = createCard(node, 'leaf');
        card.appendChild(createSeeChildrenButton('member-' + scrollToMemberId));
        branch.appendChild(card);
        return branch;
    }

    function renderBranch(memberId) {
        var node = nodeById[memberId];
        if (!node) return null;
        if (rendered[memberId]) {
            var domPartnerId = null;
            if (node.partnerIds.length) {
                node.partnerIds.forEach(function (pid) {
                    if (nodeById[pid] && dominates(nodeById[pid], node)) domPartnerId = pid;
                });
            }
            if (domPartnerId) return createLeafJumpBranch(node, domPartnerId);
            return null;
        }
        rendered[memberId] = true;

        var families = getPartnerFamilies(memberId);

        if (families.length === 0) {
            var branch = document.createElement('div');
            branch.className = 'ft-branch';
            branch.appendChild(createCard(node));
            return branch;
        }

        var branch = document.createElement('div');
        branch.className = 'ft-branch';

        if (families.length === 1) {
            var fam = families[0];
            var unit = document.createElement('div');
            unit.className = 'ft-unit';

            var parents = document.createElement('div');
            parents.className = 'ft-parents';
            parents.appendChild(createCard(node));

            if (fam.partnerId && nodeById[fam.partnerId]) {
                var coupleLink = document.createElement('div');
                coupleLink.className = 'ft-couple-link';
                parents.appendChild(coupleLink);
                parents.appendChild(createCard(nodeById[fam.partnerId]));
                rendered[fam.partnerId] = true;
            }
            unit.appendChild(parents);

            if (fam.childIds.length > 0) {
                var children = document.createElement('div');
                children.className = 'ft-children';
                fam.childIds.forEach(function (cid) {
                    var childBranch = renderBranch(cid);
                    if (childBranch) children.appendChild(childBranch);
                });
                if (children.childNodes.length > 0) unit.appendChild(children);
            }
            branch.appendChild(unit);
        } else {
            branch.appendChild(createCard(node));

            var stack = document.createElement('div');
            stack.className = 'ft-member-families';

            families.forEach(function (fam) {
                var unit = document.createElement('div');
                unit.className = 'ft-unit';

                var parents = document.createElement('div');
                parents.className = 'ft-parents';
                if (fam.partnerId && nodeById[fam.partnerId]) {
                    parents.appendChild(createCard(nodeById[fam.partnerId]));
                    rendered[fam.partnerId] = true;
                }
                unit.appendChild(parents);

                if (fam.childIds.length > 0) {
                    var children = document.createElement('div');
                    children.className = 'ft-children';
                    fam.childIds.forEach(function (cid) {
                        var childBranch = renderBranch(cid);
                        if (childBranch) children.appendChild(childBranch);
                    });
                    if (children.childNodes.length > 0) unit.appendChild(children);
                }
                stack.appendChild(unit);
            });
            branch.appendChild(stack);
        }
        return branch;
    }

    depthById = computeDepthById();
    var allIds = rawNodes.map(function (n) { return toId(n.id); });
    allIds.sort(function (a, b) {
        var da = depthById[a] || 0;
        var db = depthById[b] || 0;
        if (db !== da) return db - da;
        var nodeA = nodeById[a], nodeB = nodeById[b];
        if (da === 0) {
            var primaryA = clusterHasPrimaryDescendant([a]);
            var primaryB = clusterHasPrimaryDescendant([b]);
            if (primaryA && !primaryB) return -1;
            if (!primaryA && primaryB) return 1;
        } else {
            if (nodeA && isPrimary(nodeA) && !(nodeB && isPrimary(nodeB))) return -1;
            if (!(nodeA && isPrimary(nodeA)) && nodeB && isPrimary(nodeB)) return 1;
        }
        return a - b;
    });

    var branchCache = {};
    function buildBranchBottomUp(memberId) {
        if (memberId in branchCache) return branchCache[memberId];
        var node = nodeById[memberId];
        if (!node) return null;
        var families = getPartnerFamilies(memberId);
        if (families.length === 0) {
            var b = document.createElement('div');
            b.className = 'ft-branch';
            b.appendChild(createCard(node));
            branchCache[memberId] = b;
            return b;
        }
        var primaryPartnerId = null;
        if (families.length) {
            families.forEach(function (f) {
                if (!primaryPartnerId && f.partnerId && nodeById[f.partnerId] && dominates(nodeById[f.partnerId], node))
                    primaryPartnerId = f.partnerId;
            });
        }
        if (primaryPartnerId) {
            var ownFamilies = families.filter(function (f) {
                return f.partnerId !== primaryPartnerId;
            });
            var hasOwnChildren = ownFamilies.some(function (f) {
                return (f.childIds || []).length > 0;
            });

            if (!hasOwnChildren) {
                var depth = depthById[memberId] || 0;
                if (depth === 0) {
                    branchCache[memberId] = null;
                    return null;
                }
                var leafBranch = createLeafJumpBranch(node, primaryPartnerId);
                branchCache[memberId] = leafBranch;
                return leafBranch;
            }

            var branch = document.createElement('div');
            branch.className = 'ft-branch';
            var card = createCard(node);
            card.appendChild(createSeeChildrenButton('member-' + primaryPartnerId));

            var unit = document.createElement('div');
            unit.className = 'ft-unit';
            var parents = document.createElement('div');
            parents.className = 'ft-parents';
            parents.appendChild(card);

            if (ownFamilies.length === 1) {
                var fam = ownFamilies[0];
                if (fam.partnerId && nodeById[fam.partnerId]) {
                    var cl = document.createElement('div');
                    cl.className = 'ft-couple-link';
                    parents.appendChild(cl);
                    var partnerHasOwnBranch = (fam.partnerId in branchCache) && branchCache[fam.partnerId] !== null;
                    parents.appendChild(createCard(nodeById[fam.partnerId], partnerHasOwnBranch ? 'ref' : undefined));
                }
                unit.appendChild(parents);
                if ((fam.childIds || []).length > 0) {
                    var children = document.createElement('div');
                    children.className = 'ft-children';
                    (fam.childIds || []).forEach(function (cid) {
                        var cb = buildBranchBottomUp(cid);
                        if (cb) children.appendChild(cb);
                    });
                    if (children.childNodes.length > 0) unit.appendChild(children);
                }
            } else {
                unit.appendChild(parents);
                var ownAllChildIds = [];
                ownFamilies.forEach(function (fam) {
                    if (fam.partnerId && nodeById[fam.partnerId]) {
                        var cl = document.createElement('div');
                        cl.className = 'ft-couple-link';
                        parents.appendChild(cl);
                        var partnerHasOwnBranch = (fam.partnerId in branchCache) && branchCache[fam.partnerId] !== null;
                        parents.appendChild(createCard(nodeById[fam.partnerId], partnerHasOwnBranch ? 'ref' : undefined));
                    }
                    (fam.childIds || []).forEach(function (cid) { ownAllChildIds.push(cid); });
                });
                if (ownAllChildIds.length > 0) {
                    var children = document.createElement('div');
                    children.className = 'ft-children';
                    ownAllChildIds.forEach(function (cid) {
                        var cb = buildBranchBottomUp(cid);
                        if (cb) children.appendChild(cb);
                    });
                    if (children.childNodes.length > 0) unit.appendChild(children);
                }
            }
            branch.appendChild(unit);
            branchCache[memberId] = branch;
            return branch;
        }
        var branch = document.createElement('div');
        branch.className = 'ft-branch';
        var unit = document.createElement('div');
        unit.className = 'ft-unit';
        var parents = document.createElement('div');
        parents.className = 'ft-parents';
        parents.appendChild(createCard(node));

        var multiPartnerNode = families.length > 1;
        if (multiPartnerNode) {
            var singleParentChildIds = [];
            function hasBothParents(cid, partnerId) {
                var c = nodeById[cid];
                if (!c || !c.parentIds) return false;
                return c.parentIds.indexOf(memberId) >= 0 && (partnerId == null || c.parentIds.indexOf(partnerId) >= 0);
            }
            function onlyThisParent(cid) {
                var c = nodeById[cid];
                return c && c.parentIds && c.parentIds.length === 1 && c.parentIds[0] === memberId;
            }
            families.forEach(function (fam) {
                (fam.childIds || []).forEach(function (cid) {
                    if (onlyThisParent(cid)) singleParentChildIds.push(cid);
                });
            });
            families = families.map(function (fam) {
                if (fam.partnerId != null) {
                    var filtered = (fam.childIds || []).filter(function (cid) {
                        return hasBothParents(cid, fam.partnerId);
                    });
                    return { partnerId: fam.partnerId, childIds: filtered };
                }
                return { partnerId: fam.partnerId, childIds: singleParentChildIds };
            });
            if (singleParentChildIds.length > 0 && !families.some(function (f) { return f.partnerId == null; }))
                families.push({ partnerId: null, childIds: singleParentChildIds });
            unit.appendChild(parents);
            var partnerUnits = document.createElement('div');
            partnerUnits.className = 'ft-partner-units';
            var sortedFamilies = families.slice().sort(function (a, b) {
                var aHasPartner = a.partnerId != null ? 1 : 0;
                var bHasPartner = b.partnerId != null ? 1 : 0;
                return bHasPartner - aHasPartner;
            });
            sortedFamilies.forEach(function (fam) {
                var partnerUnit = document.createElement('div');
                partnerUnit.className = 'ft-partner-unit' + (fam.partnerId == null ? ' ft-partner-unit-single' : '');
                if (fam.partnerId && nodeById[fam.partnerId]) {
                    var coupleLink = document.createElement('div');
                    coupleLink.className = 'ft-couple-link';
                    partnerUnit.appendChild(coupleLink);
                    var partnerHasOwnBranch = (fam.partnerId in branchCache) && branchCache[fam.partnerId] !== null;
                    partnerUnit.appendChild(createCard(nodeById[fam.partnerId], partnerHasOwnBranch ? 'ref' : undefined));
                }
                if ((fam.childIds || []).length > 0) {
                    var allChildIds = fam.childIds;
                    var children = document.createElement('div');
                    children.className = 'ft-children';
                    allChildIds.forEach(function (cid) {
                        var childBranch = buildBranchBottomUp(cid);
                        if (childBranch) children.appendChild(childBranch);
                    });
                    if (children.childNodes.length > 0) partnerUnit.appendChild(children);
                }
                if (partnerUnit.childNodes.length > 0) partnerUnits.appendChild(partnerUnit);
            });
            if (partnerUnits.childNodes.length > 0) unit.appendChild(partnerUnits);
        } else {
            families.forEach(function (fam) {
                if (fam.partnerId && nodeById[fam.partnerId]) {
                    var coupleLink = document.createElement('div');
                    coupleLink.className = 'ft-couple-link';
                    parents.appendChild(coupleLink);
                    parents.appendChild(createCard(nodeById[fam.partnerId]));
                }
            });
            unit.appendChild(parents);
            var allChildIds = [];
            families.forEach(function (fam) {
                (fam.childIds || []).forEach(function (cid) { allChildIds.push(cid); });
            });
            if (allChildIds.length > 0) {
                var children = document.createElement('div');
                children.className = 'ft-children';
                allChildIds.forEach(function (cid) {
                    var childBranch = buildBranchBottomUp(cid);
                    if (childBranch) children.appendChild(childBranch);
                });
                if (children.childNodes.length > 0) unit.appendChild(children);
            }
        }
        branch.appendChild(unit);
        branchCache[memberId] = branch;
        return branch;
    }

    allIds.forEach(buildBranchBottomUp);

    var rootContainer = document.createElement('div');
    rootContainer.className = 'ft-roots';
    var rootIds = allIds.filter(function (id) { return (depthById[id] || 0) === 0; });
    rootIds.sort(function (a, b) {
        var primaryA = clusterHasPrimaryDescendant([a]);
        var primaryB = clusterHasPrimaryDescendant([b]);
        if (primaryA && !primaryB) return -1;
        if (!primaryA && primaryB) return 1;
        var priA = nodeById[a] && isPrimary(nodeById[a]);
        var priB = nodeById[b] && isPrimary(nodeById[b]);
        if (priA && !priB) return -1;
        if (!priA && priB) return 1;
        return a - b;
    });
    rootIds.forEach(function (rid) {
        var b = branchCache[rid];
        if (b) rootContainer.appendChild(b);
    });

    var treeInner = document.createElement('div');
    treeInner.className = 'family-tree-inner';
    treeInner.appendChild(rootContainer);

    var stage = document.createElement('div');
    stage.className = 'family-tree-stage';
    var world = document.createElement('div');
    world.className = 'family-tree-world';
    world.appendChild(treeInner);
    stage.appendChild(world);
    container.appendChild(stage);

    function initViewportPanZoom(stageEl, worldEl) {
        var scale = 1;
        var panX = 0;
        var panY = 0;
        var minScale = 0.35;
        var maxScale = 2.5;
        var DRAG_THRESHOLD = 8;
        var activePointers = Object.create(null);
        var pointerCount = 0;
        var panPointerId = null;
        var panPending = false;
        var panActive = false;
        var panStartX = 0;
        var panStartY = 0;
        var lastX = 0;
        var lastY = 0;
        var pinchActive = false;
        var pinchStartDist = 0;
        var pinchStartScale = 1;
        var suppressClick = false;
        var suppressClickTimer = null;

        function applyTransform() {
            worldEl.style.transform = 'translate(' + panX + 'px, ' + panY + 'px) scale(' + scale + ')';
            worldEl.style.transformOrigin = '0 0';
        }

        function clearSuppressClickSoon() {
            if (suppressClickTimer) clearTimeout(suppressClickTimer);
            suppressClickTimer = setTimeout(function () {
                suppressClick = false;
                suppressClickTimer = null;
            }, 400);
        }

        function clearSuppressClickNow() {
            suppressClick = false;
            if (suppressClickTimer) {
                clearTimeout(suppressClickTimer);
                suppressClickTimer = null;
            }
        }

        function isInteractiveTarget(target) {
            return !!(target.closest('.member-action-trigger') ||
                target.closest('.ft-expand-down'));
        }

        function shouldDblClickReset(target) {
            return !target.closest('.family-tree-card') &&
                !isInteractiveTarget(target);
        }

        function zoomAt(mx, my, newScale) {
            newScale = Math.min(maxScale, Math.max(minScale, newScale));
            var ratio = newScale / scale;
            panX = mx - (mx - panX) * ratio;
            panY = my - (my - panY) * ratio;
            scale = newScale;
            applyTransform();
        }

        function pointerList() {
            var list = [];
            for (var id in activePointers) {
                if (Object.prototype.hasOwnProperty.call(activePointers, id)) {
                    list.push(activePointers[id]);
                }
            }
            return list;
        }

        function distanceBetween(a, b) {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return Math.sqrt(dx * dx + dy * dy);
        }

        function midpointBetween(a, b) {
            return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
        }

        function setPanningClass(on) {
            if (on) stageEl.classList.add('ft-panning');
            else stageEl.classList.remove('ft-panning');
        }

        function beginPinch() {
            var pts = pointerList();
            if (pts.length < 2) return;
            panPending = false;
            panActive = false;
            panPointerId = null;
            setPanningClass(false);
            pinchActive = true;
            pinchStartDist = distanceBetween(pts[0], pts[1]);
            pinchStartScale = scale;
            if (pinchStartDist < 1) pinchStartDist = 1;
        }

        function updatePinch() {
            var pts = pointerList();
            if (pts.length < 2 || !pinchActive) return;
            var dist = distanceBetween(pts[0], pts[1]);
            var mid = midpointBetween(pts[0], pts[1]);
            var rect = stageEl.getBoundingClientRect();
            var mx = mid.x - rect.left;
            var my = mid.y - rect.top;
            zoomAt(mx, my, pinchStartScale * (dist / pinchStartDist));
            suppressClick = true;
        }

        function endPinchIfNeeded() {
            if (!pinchActive) return;
            if (pointerCount >= 2) {
                beginPinch();
                return;
            }
            pinchActive = false;
            if (pointerCount === 1) {
                var pts = pointerList();
                panPointerId = pts[0].id;
                lastX = pts[0].x;
                lastY = pts[0].y;
                panPending = false;
                panActive = true;
                setPanningClass(true);
            }
        }

        stageEl.addEventListener('wheel', function (e) {
            e.preventDefault();
            var rect = stageEl.getBoundingClientRect();
            var mx = e.clientX - rect.left;
            var my = e.clientY - rect.top;
            var delta = e.deltaY > 0 ? 0.9 : 1.1;
            zoomAt(mx, my, scale * delta);
        }, { passive: false });

        function canStartPan(target, pointerType) {
            if (isInteractiveTarget(target)) return false;
            // Touch/pen: allow pan on cards (they fill most of a phone screen).
            // Mouse: keep desktop card clicks for menus; pan only on empty stage.
            if (pointerType === 'touch' || pointerType === 'pen') return true;
            return !target.closest('.family-tree-card');
        }

        stageEl.addEventListener('pointerdown', function (e) {
            if (e.pointerType === 'mouse' && e.button !== 0) return;
            if (pointerCount === 0 && !canStartPan(e.target, e.pointerType)) return;

            activePointers[e.pointerId] = { id: e.pointerId, x: e.clientX, y: e.clientY };
            pointerCount++;
            try { stageEl.setPointerCapture(e.pointerId); } catch { /* ignore */ }

            if (pointerCount === 2) {
                beginPinch();
                e.preventDefault();
                return;
            }

            if (pointerCount === 1 && !pinchActive) {
                panPointerId = e.pointerId;
                panPending = true;
                panActive = false;
                panStartX = e.clientX;
                panStartY = e.clientY;
                lastX = e.clientX;
                lastY = e.clientY;
            }
        });

        stageEl.addEventListener('pointermove', function (e) {
            if (!activePointers[e.pointerId]) return;
            activePointers[e.pointerId].x = e.clientX;
            activePointers[e.pointerId].y = e.clientY;

            if (pinchActive && pointerCount >= 2) {
                updatePinch();
                e.preventDefault();
                return;
            }

            if (panPointerId !== e.pointerId) return;

            if (panPending && !panActive) {
                var dx = e.clientX - panStartX;
                var dy = e.clientY - panStartY;
                if ((dx * dx + dy * dy) < DRAG_THRESHOLD * DRAG_THRESHOLD) return;
                panPending = false;
                panActive = true;
                suppressClick = true;
                setPanningClass(true);
                lastX = e.clientX;
                lastY = e.clientY;
            }

            if (!panActive) return;
            panX += e.clientX - lastX;
            panY += e.clientY - lastY;
            lastX = e.clientX;
            lastY = e.clientY;
            applyTransform();
            e.preventDefault();
        });

        function releasePointer(e) {
            if (!activePointers[e.pointerId]) return;
            delete activePointers[e.pointerId];
            pointerCount = Math.max(0, pointerCount - 1);

            if (panPointerId === e.pointerId) {
                panPointerId = null;
                panPending = false;
                panActive = false;
                setPanningClass(false);
            }

            if (pointerCount < 2) endPinchIfNeeded();
            if (pointerCount === 0) {
                panPointerId = null;
                panPending = false;
                panActive = false;
                pinchActive = false;
                setPanningClass(false);
                if (suppressClick) clearSuppressClickSoon();
            }
        }

        stageEl.addEventListener('pointerup', releasePointer);
        stageEl.addEventListener('pointercancel', releasePointer);

        stageEl.addEventListener('click', function (e) {
            if (!suppressClick) return;
            clearSuppressClickNow();
            e.preventDefault();
            e.stopPropagation();
        }, true);

        stageEl.addEventListener('dblclick', function (e) {
            if (!shouldDblClickReset(e.target)) return;
            scale = 1;
            panX = 0;
            panY = 0;
            applyTransform();
        });

        function focusMember(memberId) {
            var el = document.getElementById('member-' + memberId);
            if (!el) return false;
            var stageRect = stageEl.getBoundingClientRect();
            var cardRect = el.getBoundingClientRect();
            var cx = (cardRect.left + cardRect.width / 2 - stageRect.left - panX) / scale;
            var cy = (cardRect.top + cardRect.height / 2 - stageRect.top - panY) / scale;
            panX = stageRect.width / 2 - cx * scale;
            panY = stageRect.height / 2 - cy * scale;
            applyTransform();
            return true;
        }

        return { focusMember: focusMember };
    }

    window.FamilyTreeViewport = initViewportPanZoom(stage, world);

    // Insert spacers for missing half-rank slots so same-rank members align.
    // Vertical: ranks = rows ΓåÆ spacers add height. Horizontal: ranks = columns ΓåÆ spacers add width.
    var isHorizontal = container.classList.contains('ft-orientation-horizontal');
    var rankDim = isHorizontal ? 'width' : 'height';
    var rankPos = isHorizontal ? 'left' : 'top';
    var rankMargin = isHorizontal ? 'marginLeft' : 'marginTop';

    (function insertHalfRankSpacers() {
        var halfRankLevels = {};
        Object.keys(nodeById).forEach(function (id) {
            var r = nodeById[id].visualRank;
            if (r % 1 !== 0) halfRankLevels[Math.floor(r)] = true;
        });
        if (Object.keys(halfRankLevels).length === 0) return;

        // Phase 1: insert zero-size spacers in branches that skip a half-rank.
        container.querySelectorAll('.ft-children').forEach(function (childrenEl) {
            var parent = childrenEl.parentElement;
            if (!parent) return;

            var parentCard = null;
            if (parent.classList.contains('ft-partner-unit') &&
                !parent.classList.contains('ft-partner-unit-single')) {
                var unitEl = parent.closest('.ft-unit');
                if (unitEl) parentCard = unitEl.querySelector('.ft-parents > .family-tree-card');
            } else if (parent.classList.contains('ft-partner-unit-single')) {
                var unitEl = parent.closest('.ft-unit');
                if (unitEl) parentCard = unitEl.querySelector('.ft-parents > .family-tree-card');
            } else if (parent.classList.contains('ft-unit')) {
                parentCard = parent.querySelector('.ft-parents > .family-tree-card');
            }
            if (!parentCard) return;

            var parentRank = parseFloat(parentCard.getAttribute('data-visual-rank') || '0');
            if (parentRank % 1 !== 0) return;
            if (!halfRankLevels[parentRank]) return;

            var spacer = document.createElement('div');
            spacer.className = 'ft-rank-spacer';
            spacer.style[rankDim] = '0px';
            parent.insertBefore(spacer, childrenEl);
        });

        // Phase 2: after layout settles, align root branches first, then adjust spacers.
        setTimeout(function () {
            // Phase 2a: align satellite root branches to the main (anchor) branch.
            function alignRoots() {
                var rootBranches = container.querySelectorAll('.ft-roots > .ft-branch');
                var anchorBranch = null;
                var maxCardCount = 0;
                rootBranches.forEach(function (branch) {
                    var count = branch.querySelectorAll('.family-tree-card').length;
                    if (count > maxCardCount) { maxCardCount = count; anchorBranch = branch; }
                });
                rootBranches.forEach(function (branch) {
                    if (branch === anchorBranch) return;
                    var firstCard = branch.querySelector('.family-tree-card[data-visual-rank]');
                    if (!firstCard) return;
                    var firstRank = parseFloat(firstCard.getAttribute('data-visual-rank'));
                    if (firstRank === 0) return;

                    var targetCard = null;
                    anchorBranch.querySelectorAll('.family-tree-card[data-visual-rank]').forEach(function (c) {
                        if (parseFloat(c.getAttribute('data-visual-rank')) === firstRank) targetCard = c;
                    });
                    if (!targetCard) return;

                    var diff = targetCard.getBoundingClientRect()[rankPos] - firstCard.getBoundingClientRect()[rankPos];
                    if (Math.abs(diff) > 1) {
                        var currentMargin = parseFloat(branch.style[rankMargin] || '0');
                        branch.style[rankMargin] = (currentMargin + diff) + 'px';
                    }
                });
                // Force reflow so measurements account for root branch margins.
                void container.offsetHeight;
            }

            alignRoots();

            // Phase 2b: measure card positions and adjust spacers for same-rank alignment.
            Object.keys(halfRankLevels).forEach(function (baseStr) {
                var childRank = parseInt(baseStr, 10) + 1;

                var entries = [];
                container.querySelectorAll('.family-tree-card[data-visual-rank]').forEach(function (c) {
                    if (parseFloat(c.getAttribute('data-visual-rank')) !== childRank) return;
                    var branch = c.closest('.ft-branch');
                    if (!branch) return;
                    var childrenEl = branch.parentElement;
                    if (!childrenEl || !childrenEl.classList.contains('ft-children')) return;
                    var spacer = childrenEl.previousElementSibling;
                    entries.push({ pos: c.getBoundingClientRect()[rankPos], spacer: spacer });
                });
                if (entries.length < 2) return;

                var maxPos = -Infinity;
                entries.forEach(function (e) { if (e.pos > maxPos) maxPos = e.pos; });

                var seen = new Map();
                entries.forEach(function (e) {
                    if (!e.spacer || !e.spacer.classList.contains('ft-rank-spacer')) return;
                    var diff = maxPos - e.pos;
                    if (diff < 1) return;
                    if (!seen.has(e.spacer) || seen.get(e.spacer) < diff) seen.set(e.spacer, diff);
                });
                seen.forEach(function (diff, spacer) {
                    spacer.style[rankDim] = (parseFloat(spacer.style[rankDim]) + diff) + 'px';
                });
            });

            // Phase 2c: Re-align satellite root branches since Phase 2b spacers may have shifted target anchors
            alignRoots();

        }, 50);
    })();

    // --- Details panel, hover preview, and action menu ---

    var popup = document.getElementById('member-action-popup');
    var detailsPopup = document.getElementById('member-details-popup');
    var hoverCard = null;
    var hoverHideTimer = null;
    var hoverMemberId = null;
    var hoverSourceCard = null;

    function getAntiForgeryToken(el) {
        var input = el && el.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function clampElementInView(el) {
        if (!el) return;
        var pad = 8;
        var vw = window.innerWidth;
        var vh = window.innerHeight;
        var r = el.getBoundingClientRect();
        var left = r.left;
        var top = r.top;
        if (r.right > vw - pad) left = vw - el.offsetWidth - pad;
        if (left < pad) left = pad;
        if (r.bottom > vh - pad) top = vh - el.offsetHeight - pad;
        if (top < pad) top = pad;
        el.style.left = left + 'px';
        el.style.top = top + 'px';
    }

    function positionElementInView(el, triggerRect) {
        if (!el) return;
        el.style.position = 'fixed';
        el.style.left = triggerRect.left + 'px';
        el.style.top = (triggerRect.bottom + 4) + 'px';
        requestAnimationFrame(function () {
            clampElementInView(el);
        });
    }

    function closeActionPopup() {
        if (!popup) return;
        popup.style.display = 'none';
        popup.innerHTML = '<div class="text-muted small p-2">Loading\u2026</div>';
    }

    function closeDetailsPopup() {
        if (!detailsPopup) return;
        detailsPopup.style.display = 'none';
        detailsPopup.hidden = true;
        detailsPopup.innerHTML = '';
        detailsPopup._sourceCard = null;
    }

    function isActionPopupOpen() {
        return !!(popup && popup.style.display !== 'none');
    }

    function isDetailsPopupOpen() {
        return !!(detailsPopup && detailsPopup.style.display !== 'none' && !detailsPopup.hidden);
    }

    function isPhotoOnlyView() {
        return (container.className || '').indexOf('ft-view-photo-') !== -1;
    }

    function canUseHoverPreview() {
        return window.matchMedia &&
            window.matchMedia('(hover: hover) and (pointer: fine)').matches;
    }

    function isFinePointerDesktop() {
        return canUseHoverPreview();
    }

    function getSiblingIds(node) {
        if (!node || !node.parentIds || !node.parentIds.length) return [];
        var parentSet = {};
        node.parentIds.forEach(function (pid) { parentSet[toId(pid)] = true; });
        var seen = {};
        var out = [];
        Object.keys(nodeById).forEach(function (key) {
            var other = nodeById[key];
            if (!other || other.id === node.id) return;
            if (!other.parentIds || !other.parentIds.length) return;
            var shares = other.parentIds.some(function (pid) { return parentSet[toId(pid)]; });
            if (shares && !seen[other.id]) {
                seen[other.id] = true;
                out.push(other.id);
            }
        });
        return out;
    }

    function openMemberPhoto(cardEl) {
        if (!canEdit || !window.MemberPhoto || !cardEl) return false;
        var memberId = cardEl.getAttribute('data-member-id');
        var node = nodeById[toId(memberId)];
        var avatar = cardEl.querySelector('.family-tree-card-avatar');
        window.MemberPhoto.open({
            memberId: memberId,
            label: node ? node.label : cardEl.getAttribute('aria-label') || '',
            name: node ? node.name : '',
            nickName: node ? node.nickName : '',
            hasPhoto: node ? !!node.hasPhoto : !!(avatar && avatar.querySelector('.family-tree-card-photo')),
            sourceCard: cardEl
        });
        return true;
    }

    function appendRelativeSection(parent, title, ids) {
        if (!ids || !ids.length) return;
        var section = document.createElement('div');
        section.className = 'ft-details-section';
        var heading = document.createElement('div');
        heading.className = 'ft-details-section-title';
        heading.textContent = title;
        section.appendChild(heading);
        ids.forEach(function (rid) {
            var rel = nodeById[toId(rid)];
            if (!rel) return;
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'ft-details-relative btn btn-sm btn-link p-0';
            btn.setAttribute('data-member-id', String(rel.id));
            btn.textContent = rel.label;
            section.appendChild(btn);
        });
        if (section.querySelector('.ft-details-relative')) parent.appendChild(section);
    }

    function appendLifeDates(parent, node, className) {
        if (!node.dob && !node.dod) return;
        var life = document.createElement('div');
        life.className = className;
        if (node.dob) {
            var born = document.createElement('div');
            born.textContent = 'Born ' + String(node.dob);
            life.appendChild(born);
        }
        if (node.dod) {
            var died = document.createElement('div');
            died.textContent = 'Died ' + String(node.dod);
            life.appendChild(died);
        }
        parent.appendChild(life);
    }

    function buildDetailsContent(node, cardEl) {
        var panel = document.createElement('div');
        panel.className = 'ft-details-panel';

        var header = document.createElement('div');
        header.className = 'ft-details-header';

        var avatar = document.createElement('div');
        avatar.className = 'ft-details-avatar';
        if (node.photoUrl) {
            var img = document.createElement('img');
            img.src = node.photoUrl;
            img.alt = '';
            avatar.appendChild(img);
        } else if (node.hasPhoto) {
            var photo = document.createElement('img');
            photo.src = '/photos/members/' + node.id;
            photo.alt = '';
            avatar.appendChild(photo);
        } else {
            avatar.textContent = avatarInitials(node);
        }
        header.appendChild(avatar);

        var titleWrap = document.createElement('div');
        titleWrap.className = 'ft-details-title-wrap';
        var nameEl = document.createElement('div');
        nameEl.className = 'ft-details-name';
        nameEl.textContent = node.label;
        titleWrap.appendChild(nameEl);
        if (node.isMe) {
            var badge = document.createElement('span');
            badge.className = 'ft-details-you-badge';
            badge.textContent = 'You';
            titleWrap.appendChild(badge);
        }
        header.appendChild(titleWrap);
        panel.appendChild(header);

        appendLifeDates(panel, node, 'ft-details-life');

        appendRelativeSection(panel, 'Parents', node.parentIds);
        appendRelativeSection(panel, 'Siblings', getSiblingIds(node));
        appendRelativeSection(panel, 'Partners', node.partnerIds);
        appendRelativeSection(panel, 'Children', node.childIds);

        if (canEdit) {
            var actions = document.createElement('div');
            actions.className = 'ft-details-actions';

            // Mobile/touch: expose Change picture explicitly (no hover, photo tap opens details).
            if (!isFinePointerDesktop() && window.MemberPhoto) {
                var photoBtn = document.createElement('button');
                photoBtn.type = 'button';
                photoBtn.className = 'btn btn-sm btn-outline-secondary ft-details-photo-btn';
                photoBtn.textContent = 'Change picture';
                photoBtn.addEventListener('click', function (e) {
                    e.preventDefault();
                    e.stopPropagation();
                    closeDetailsPopup();
                    openMemberPhoto(cardEl);
                });
                actions.appendChild(photoBtn);
            }

            var manageBtn = document.createElement('button');
            manageBtn.type = 'button';
            manageBtn.className = 'btn btn-sm btn-outline-primary ft-details-manage-btn';
            manageBtn.textContent = 'Manage';
            manageBtn.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                closeDetailsPopup();
                openPopupForCard(cardEl);
            });
            actions.appendChild(manageBtn);
            panel.appendChild(actions);
        }

        return panel;
    }

    function openDetails(cardEl) {
        if (!detailsPopup || !cardEl) return;
        var memberId = cardEl.getAttribute('data-member-id');
        if (!memberId) return;
        var node = nodeById[toId(memberId)];
        if (!node) return;

        hideHoverCard();
        closeActionPopup();

        detailsPopup._sourceCard = cardEl;
        detailsPopup.innerHTML = '';
        detailsPopup.appendChild(buildDetailsContent(node, cardEl));
        detailsPopup.hidden = false;
        detailsPopup.style.display = '';
        positionElementInView(detailsPopup, cardEl.getBoundingClientRect());
    }

    function ensureHoverCard() {
        if (hoverCard) return hoverCard;
        hoverCard = document.createElement('div');
        hoverCard.id = 'member-hover-card';
        hoverCard.className = 'ft-member-hover-card';
        hoverCard.setAttribute('aria-hidden', 'true');
        hoverCard.style.display = 'none';
        document.body.appendChild(hoverCard);

        hoverCard.addEventListener('pointerenter', function () {
            if (hoverHideTimer) {
                clearTimeout(hoverHideTimer);
                hoverHideTimer = null;
            }
        });
        hoverCard.addEventListener('pointerleave', function () {
            scheduleHideHover();
        });
        hoverCard.addEventListener('click', function (e) {
            var manage = e.target.closest('.ft-hover-manage-btn');
            if (!manage || !hoverSourceCard) return;
            e.preventDefault();
            e.stopPropagation();
            var cardEl = hoverSourceCard;
            hideHoverCard();
            openPopupForCard(cardEl);
        });
        return hoverCard;
    }

    function hideHoverCard() {
        if (hoverHideTimer) {
            clearTimeout(hoverHideTimer);
            hoverHideTimer = null;
        }
        hoverMemberId = null;
        hoverSourceCard = null;
        if (hoverCard) {
            hoverCard.style.display = 'none';
            hoverCard.classList.remove('ft-member-hover-card-interactive');
            hoverCard.innerHTML = '';
        }
    }

    function buildHoverContent(node, interactiveManage) {
        var wrap = document.createElement('div');
        var nameEl = document.createElement('div');
        nameEl.className = 'ft-hover-name';
        nameEl.textContent = node.label;
        wrap.appendChild(nameEl);

        appendLifeDates(wrap, node, 'ft-hover-meta');

        function addRelLine(label, ids) {
            if (!ids || !ids.length) return;
            var names = [];
            ids.forEach(function (rid) {
                var rel = nodeById[toId(rid)];
                if (rel) names.push(rel.label);
            });
            if (!names.length) return;
            var line = document.createElement('div');
            line.className = 'ft-hover-rel';
            line.textContent = label + ': ' + names.join(', ');
            wrap.appendChild(line);
        }
        addRelLine('Parents', node.parentIds);
        addRelLine('Siblings', getSiblingIds(node));
        addRelLine('Partners', node.partnerIds);
        addRelLine('Children', node.childIds);

        if (interactiveManage) {
            var actions = document.createElement('div');
            actions.className = 'ft-hover-actions';
            var manageBtn = document.createElement('button');
            manageBtn.type = 'button';
            manageBtn.className = 'btn btn-sm btn-outline-primary ft-hover-manage-btn';
            manageBtn.textContent = 'Manage';
            actions.appendChild(manageBtn);
            wrap.appendChild(actions);
        }
        return wrap;
    }

    function showHoverForCard(cardEl) {
        if (!canUseHoverPreview() || isDemo) return;
        if (isDetailsPopupOpen() || isActionPopupOpen()) return;
        var stage = container.querySelector('.family-tree-stage');
        if (stage && stage.classList.contains('ft-panning')) return;

        var memberId = cardEl.getAttribute('data-member-id');
        var node = nodeById[toId(memberId)];
        if (!node) return;

        if (hoverHideTimer) {
            clearTimeout(hoverHideTimer);
            hoverHideTimer = null;
        }

        var interactive = isPhotoOnlyView() && canEdit;
        var card = ensureHoverCard();
        hoverSourceCard = cardEl;
        if (hoverMemberId !== memberId || card.classList.contains('ft-member-hover-card-interactive') !== interactive) {
            hoverMemberId = memberId;
            card.innerHTML = '';
            card.appendChild(buildHoverContent(node, interactive));
            card.classList.toggle('ft-member-hover-card-interactive', interactive);
        }
        card.style.display = '';
        positionElementInView(card, cardEl.getBoundingClientRect());
    }

    function scheduleHideHover() {
        if (hoverHideTimer) clearTimeout(hoverHideTimer);
        hoverHideTimer = setTimeout(hideHoverCard, 180);
    }

    if (detailsPopup) {
        detailsPopup.addEventListener('click', function (e) {
            var relBtn = e.target.closest('.ft-details-relative');
            if (!relBtn) return;
            e.preventDefault();
            e.stopPropagation();
            var rid = relBtn.getAttribute('data-member-id');
            closeDetailsPopup();
            focusMemberCard('member-' + rid);
        });
    }

    document.addEventListener('mousedown', function (e) {
        if (isDetailsPopupOpen() &&
            detailsPopup && !detailsPopup.contains(e.target) &&
            !e.target.closest('.family-tree-card')) {
            closeDetailsPopup();
        }
        if (isActionPopupOpen() &&
            popup && !popup.contains(e.target) &&
            !e.target.closest('.member-action-trigger') &&
            !e.target.closest('.ft-details-manage-btn') &&
            !e.target.closest('.ft-hover-manage-btn')) {
            closeActionPopup();
        }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        if (isActionPopupOpen()) closeActionPopup();
        else if (isDetailsPopupOpen()) closeDetailsPopup();
        hideHoverCard();
    });

    function openPopupForCard(cardEl) {
        if (!canEdit || !popup) return;
        var memberId = cardEl.getAttribute('data-member-id');
        if (!memberId) return;
        hideHoverCard();
        closeDetailsPopup();
        popup._sourceCard = cardEl;
        var rect = cardEl.getBoundingClientRect();
        popup.innerHTML = '<div class="text-muted small p-2">Loading\u2026</div>';
        popup.style.display = '';
        popup.style.position = 'fixed';
        popup.style.top = (rect.bottom + 4) + 'px';
        popup.style.left = rect.left + 'px';
        fetch('/FamilyMember/ActionMenuContent?memberId=' + encodeURIComponent(memberId), {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(function (r) { return r.text(); })
            .then(function (html) {
                popup.innerHTML = html;
                var menuEl = popup.querySelector('.cascading-menu');
                if (menuEl) initCascadingMenu(menuEl);
                positionElementInView(popup, rect);
            })
            .catch(function () {
                popup.innerHTML = '<div class="text-danger small p-2">Failed to load.</div>';
                positionElementInView(popup, rect);
            });
    }

    if (!isDemo) {
        container.addEventListener('click', function (e) {
            var trigger = e.target.closest('.member-action-trigger');
            if (trigger) {
                e.preventDefault();
                e.stopPropagation();
                openPopupForCard(trigger.closest('.family-tree-card'));
                return;
            }

            var card = e.target.closest('.family-tree-card');
            if (!card || e.target.closest('.ft-expand-down')) return;

            var avatar = e.target.closest('.member-photo-trigger, .member-details-trigger');
            // Desktop editors: avatar/photo click changes picture; body opens details.
            // Touch / view-only: any card tap opens details.
            if (avatar && canEdit && isFinePointerDesktop()) {
                e.preventDefault();
                e.stopPropagation();
                hideHoverCard();
                openMemberPhoto(card);
                return;
            }

            e.preventDefault();
            openDetails(card);
        });

        container.addEventListener('pointerover', function (e) {
            if (e.pointerType && e.pointerType !== 'mouse') return;
            var card = e.target.closest('.family-tree-card');
            if (!card || !container.contains(card)) return;
            showHoverForCard(card);
        });

        container.addEventListener('pointerout', function (e) {
            if (e.pointerType && e.pointerType !== 'mouse') return;
            var card = e.target.closest('.family-tree-card');
            if (!card) return;
            var related = e.relatedTarget;
            if (related && card.contains(related)) return;
            if (related && hoverCard && hoverCard.contains(related)) return;
            scheduleHideHover();
        });
    }

    if (!popup) return;

    function initCascadingMenu(menu) {
        var primaryItems = menu.querySelectorAll('.cascading-item');
        var panels = menu.querySelectorAll('.cascading-panel');
        var subContainer = menu.querySelector('.cascading-sub');
        function showPanel(name) {
            primaryItems.forEach(function (it) {
                it.classList.toggle('active', it.getAttribute('data-panel') === name);
            });
            var anyVisible = false;
            panels.forEach(function (p) {
                var match = p.getAttribute('data-panel') === name;
                p.style.display = match ? 'block' : 'none';
                if (match) anyVisible = true;
            });
            if (subContainer) subContainer.classList.toggle('has-active-panel', anyVisible);
            requestAnimationFrame(function () { clampElementInView(popup); });
        }
        primaryItems.forEach(function (item) {
            item.addEventListener('click', function () { showPanel(item.getAttribute('data-panel')); });
        });
        panels.forEach(function (p) { p.style.display = 'none'; });
        if (subContainer) subContainer.classList.remove('has-active-panel');
        initAddPanel(menu);
        initEditPanel(menu);
        initRemovePanel(menu);
    }

    function initAddPanel(menu) {
        var typeBtns = menu.querySelectorAll('.menu-type-btn');
        var choiceBtns = menu.querySelectorAll('.menu-add-choice-btn');
        var panelExisting = menu.querySelector('.menu-panel-existing');
        var panelNewForm = menu.querySelector('.menu-panel-new-form');
        var searchInput = menu.querySelector('.member-action-search');
        var candidatesContainers = menu.querySelectorAll('.menu-candidates');
        var addForm = menu.querySelector('.cascading-add-form');
        var relTypeInput = addForm ? addForm.querySelector('.add-rel-type') : null;
        var isChildInput = addForm ? addForm.querySelector('.add-is-child') : null;
        var birthOrderRow = menu.querySelector('.menu-birthorder-row');
        function typeValue(type) {
            if (type === 'parent') return 0;
            if (type === 'partner') return 2;
            return 0;
        }
        function getSelectedType() {
            var active = menu.querySelector('.menu-type-btn.active');
            if (!active) return { type: 'parent', isChild: 'true' };
            return { type: active.getAttribute('data-type'), isChild: active.getAttribute('data-ischild') };
        }
        function syncFormHiddenFields() {
            var sel = getSelectedType();
            if (relTypeInput) relTypeInput.value = String(typeValue(sel.type));
            if (isChildInput) isChildInput.value = sel.isChild === 'true' ? 'true' : 'false';
            if (birthOrderRow) birthOrderRow.style.display = 'none';
        }
        function showCandidates() {
            var sel = getSelectedType();
            var q = (searchInput ? searchInput.value : '').trim().toLowerCase();
            candidatesContainers.forEach(function (cont) {
                var match = cont.getAttribute('data-type') === sel.type && cont.getAttribute('data-ischild') === sel.isChild;
                cont.style.display = match ? '' : 'none';
                cont.querySelectorAll('.candidate-btn').forEach(function (btn) {
                    var name = (btn.getAttribute('data-name') || '').toLowerCase();
                    btn.style.display = match && (!q || name.indexOf(q) !== -1) ? '' : 'none';
                });
            });
        }
        function showChoice() {
            var choice = menu.querySelector('.menu-add-choice-btn.active');
            var isExisting = choice && choice.getAttribute('data-choice') === 'existing';
            if (panelExisting) panelExisting.style.display = isExisting ? '' : 'none';
            if (panelNewForm) panelNewForm.style.display = isExisting ? 'none' : '';
            if (isExisting) showCandidates();
            syncFormHiddenFields();
        }
        typeBtns.forEach(function (btn) {
            btn.addEventListener('click', function () {
                typeBtns.forEach(function (b) { b.classList.remove('active'); });
                btn.classList.add('active');
                syncFormHiddenFields();
                showCandidates();
                requestAnimationFrame(function () { clampElementInView(popup); });
            });
        });
        choiceBtns.forEach(function (btn) {
            btn.addEventListener('click', function () {
                choiceBtns.forEach(function (b) { b.classList.remove('active'); });
                btn.classList.add('active');
                showChoice();
                requestAnimationFrame(function () { clampElementInView(popup); });
            });
        });
        if (searchInput) searchInput.addEventListener('input', showCandidates);
        menu.querySelectorAll('.candidate-btn').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var memberId = menu.getAttribute('data-member-id');
                var treeId = menu.getAttribute('data-tree-id');
                var token = getAntiForgeryToken(menu);
                var body = new URLSearchParams();
                body.append('ContextMemberId', memberId);
                body.append('FamilyTreeId', treeId);
                body.append('RelationshipType', btn.getAttribute('data-rel-type'));
                body.append('IsChild', btn.getAttribute('data-is-child'));
                body.append('ExistingMemberId', btn.getAttribute('data-existing-id'));
                body.append('__RequestVerificationToken', token);
                fetch('/FamilyMember/LinkExisting', { method: 'POST', body: body })
                    .then(function () { window.location.reload(); });
            });
        });
        if (addForm) {
            addForm.addEventListener('submit', function (e) {
                e.preventDefault();
                fetch('/FamilyMember/AddRelation', { method: 'POST', body: new URLSearchParams(new FormData(addForm)) })
                    .then(function () { window.location.reload(); });
            });
        }
        syncFormHiddenFields();
        showChoice();
    }

    function initEditPanel(menu) {
        var form = menu.querySelector('.cascading-edit-form');
        if (!form) return;

        var photoInput = form.querySelector('.member-photo-input');
        var removeBtn = form.querySelector('.member-photo-remove-btn');
        var memberId = form.getAttribute('data-member-id');

        if (photoInput) {
            photoInput.addEventListener('change', function () {
                if (!photoInput.files || photoInput.files.length === 0) return;
                var upload = window.PhotoUpload && window.PhotoUpload.uploadMemberPhoto;
                if (!upload) return;

                upload(memberId, photoInput.files[0], { sourceCard: popup._sourceCard })
                    .then(function () {
                        updateMemberEditPhotoPreview(form, memberId, true);
                        photoInput.value = '';
                    })
                    .catch(function (err) { alert(err.message || 'Failed to upload photo.'); });
            });
        }

        if (removeBtn) {
            removeBtn.addEventListener('click', function () {
                var remove = window.PhotoUpload && window.PhotoUpload.removeMemberPhoto;
                if (!remove) return;

                remove(memberId, { sourceCard: popup._sourceCard })
                    .then(function () { updateMemberEditPhotoPreview(form, memberId, false); })
                    .catch(function (err) { alert(err.message || 'Failed to remove photo.'); });
            });
        }

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            fetch('/FamilyMember/EditMember', { method: 'POST', body: new URLSearchParams(new FormData(form)) })
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    if (data.success) window.location.reload();
                    else alert(data.error || 'Failed to save.');
                });
        });
    }

    function initRemovePanel(menu) {
        menu.querySelectorAll('.cascading-remove-btn').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var token = getAntiForgeryToken(menu);
                var body = new URLSearchParams();
                body.append('relationshipId', btn.getAttribute('data-rel-id'));
                body.append('__RequestVerificationToken', token);
                fetch('/FamilyMember/RemoveRelation', { method: 'POST', body: body })
                    .then(function (r) { return r.json(); })
                    .then(function (data) {
                        if (data.success) window.location.reload();
                        else alert(data.error || 'Failed to remove.');
                    });
            });
        });
    }
})();
