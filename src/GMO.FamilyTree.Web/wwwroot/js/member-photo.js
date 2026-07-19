(function () {
    var modalEl = document.getElementById('memberPhotoModal');
    if (!modalEl || !window.PhotoUpload) return;

    var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    var memberIdInput = document.getElementById('member-photo-member-id');
    var fileInput = document.getElementById('member-photo-file');
    var preview = document.getElementById('member-photo-preview');
    var previewPlaceholder = document.getElementById('member-photo-preview-placeholder');
    var errorEl = document.getElementById('member-photo-error');
    var submitBtn = document.getElementById('member-photo-submit');
    var removeBtn = document.getElementById('member-photo-remove');
    var titleEl = document.getElementById('memberPhotoModalLabel');

    var state = {
        memberId: null,
        label: '',
        name: '',
        nickName: '',
        hasPhoto: false,
        sourceCard: null
    };

    function clearError() {
        if (!errorEl) return;
        errorEl.textContent = '';
        errorEl.classList.add('d-none');
    }

    function showError(message) {
        if (!errorEl) return;
        errorEl.textContent = message;
        errorEl.classList.remove('d-none');
    }

    function memberPhotoUrl(memberId) {
        return '/photos/members/' + memberId + '?t=' + Date.now();
    }

    function resetPreview() {
        if (!preview || !previewPlaceholder) return;
        if (state.hasPhoto && state.memberId) {
            preview.src = memberPhotoUrl(state.memberId);
            preview.classList.remove('d-none');
            previewPlaceholder.classList.add('d-none');
        } else {
            preview.removeAttribute('src');
            preview.classList.add('d-none');
            previewPlaceholder.textContent = PhotoUpload.avatarInitials({
                nickName: state.nickName,
                name: state.name,
                label: state.label
            });
            previewPlaceholder.classList.remove('d-none');
        }
    }

    function setRemoveVisible(visible) {
        if (!removeBtn) return;
        removeBtn.classList.toggle('d-none', !visible);
    }

    function open(options) {
        state.memberId = options.memberId;
        state.label = options.label || '';
        state.name = options.name || '';
        state.nickName = options.nickName || '';
        state.hasPhoto = !!options.hasPhoto;
        state.sourceCard = options.sourceCard || null;

        if (memberIdInput) memberIdInput.value = state.memberId;
        if (titleEl) titleEl.textContent = state.label ? 'Picture — ' + state.label : 'Picture';
        if (fileInput) fileInput.value = '';
        clearError();
        resetPreview();
        setRemoveVisible(state.hasPhoto);
        modal.show();
    }

    if (fileInput) {
        fileInput.addEventListener('change', function () {
            clearError();
            var file = fileInput.files && fileInput.files[0];
            if (!file || !preview || !previewPlaceholder) return;
            preview.src = URL.createObjectURL(file);
            preview.classList.remove('d-none');
            previewPlaceholder.classList.add('d-none');
        });
    }

    if (removeBtn) {
        removeBtn.addEventListener('click', function () {
            if (!state.memberId) return;
            clearError();
            removeBtn.disabled = true;
            PhotoUpload.removeMemberPhoto(state.memberId, { sourceCard: state.sourceCard })
                .then(function () { modal.hide(); })
                .catch(function (err) { showError(err.message); })
                .finally(function () { removeBtn.disabled = false; });
        });
    }

    var form = document.getElementById('member-photo-form');
    if (form) {
        form.addEventListener('submit', function (event) {
            event.preventDefault();
            clearError();

            if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
                showError('Please select an image file.');
                return;
            }

            submitBtn.disabled = true;
            PhotoUpload.uploadMemberPhoto(state.memberId, fileInput.files[0], { sourceCard: state.sourceCard })
                .then(function () {
                    state.hasPhoto = true;
                    modal.hide();
                })
                .catch(function (err) { showError(err.message); })
                .finally(function () { submitBtn.disabled = false; });
        });
    }

    window.MemberPhoto = { open: open };
})();
