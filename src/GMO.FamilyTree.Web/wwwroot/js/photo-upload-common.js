(function () {
    'use strict';

    function getAntiforgeryToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function initials(label) {
        if (!label) return '?';
        var parts = String(label).trim().split(/\s+/).filter(Boolean);
        if (parts.length >= 2)
            return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
        return parts[0] ? parts[0].slice(0, 2).toUpperCase() : '?';
    }

    /** Prefer first nickname word; else name; else label without trailing "(nick)". */
    function avatarInitials(options) {
        options = options || {};
        var nick = options.nickName ? String(options.nickName).trim() : '';
        if (nick) {
            var firstNick = nick.split(/\s+/).filter(Boolean)[0];
            return initials(firstNick || nick);
        }
        var name = options.name ? String(options.name).trim() : '';
        if (name) return initials(name);
        var label = options.label ? String(options.label).trim() : '';
        var withoutParen = label.replace(/\s*\([^)]*\)\s*$/, '').trim();
        return initials(withoutParen || label);
    }

    function postMultipart(url, formData) {
        return fetch(url, {
            method: 'POST',
            body: formData,
            headers: {
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            credentials: 'same-origin'
        }).then(function (response) {
            return response.json()
                .catch(function () { return null; })
                .then(function (data) { return { ok: response.ok, status: response.status, data: data }; });
        });
    }

    function uploadMemberPhoto(memberId, file, options) {
        options = options || {};
        var formData = new FormData();
        formData.append('memberId', memberId);
        formData.append('photo', file);
        formData.append('__RequestVerificationToken', getAntiforgeryToken());

        return postMultipart('/FamilyMember/UploadMemberPhoto', formData)
            .then(function (result) {
                if (result.ok && result.data && result.data.success) {
                    if (window.FamilyTreePhotos && window.FamilyTreePhotos.updateMemberPhoto)
                        window.FamilyTreePhotos.updateMemberPhoto(memberId, true, options.sourceCard);
                    return result.data;
                }
                var error = (result.data && result.data.error)
                    || (result.status === 400 ? 'Upload was rejected. Refresh the page and try again.' : 'Upload failed.');
                throw new Error(error);
            });
    }

    function removeMemberPhoto(memberId, options) {
        options = options || {};
        var body = new URLSearchParams();
        body.append('memberId', memberId);
        body.append('__RequestVerificationToken', getAntiforgeryToken());

        return fetch('/FamilyMember/RemoveMemberPhoto', { method: 'POST', body: body })
            .then(function (response) {
                return response.json()
                    .catch(function () { return null; })
                    .then(function (data) {
                        if (response.ok && data && data.success) {
                            if (window.FamilyTreePhotos && window.FamilyTreePhotos.updateMemberPhoto)
                                window.FamilyTreePhotos.updateMemberPhoto(memberId, false, options.sourceCard);
                            return data;
                        }
                        throw new Error((data && data.error) || 'Failed to remove photo.');
                    });
            });
    }

    window.PhotoUpload = {
        getAntiforgeryToken: getAntiforgeryToken,
        initials: initials,
        avatarInitials: avatarInitials,
        uploadMemberPhoto: uploadMemberPhoto,
        removeMemberPhoto: removeMemberPhoto
    };
})();
