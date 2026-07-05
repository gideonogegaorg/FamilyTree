(function () {
    const modalEl = document.getElementById('profilePhotoModal');
    if (!modalEl) return;

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const form = document.getElementById('profile-photo-form');
    const fileInput = document.getElementById('profile-photo-file');
    const preview = document.getElementById('profile-photo-preview');
    const previewPlaceholder = document.getElementById('profile-photo-preview-placeholder');
    const errorEl = document.getElementById('profile-photo-error');
    const submitBtn = document.getElementById('profile-photo-submit');

    function getDefaultPhotoUrl() {
        return modalEl.dataset.photoUrl || '';
    }

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

    function resetPreview() {
        if (!preview || !previewPlaceholder) return;
        var defaultPhotoUrl = getDefaultPhotoUrl();
        if (defaultPhotoUrl) {
            preview.src = defaultPhotoUrl;
            preview.classList.remove('d-none');
            previewPlaceholder.classList.add('d-none');
        } else {
            preview.removeAttribute('src');
            preview.classList.add('d-none');
            previewPlaceholder.classList.remove('d-none');
        }
    }

    function updateNavPhotos(url) {
        document.querySelectorAll('[data-profile-photo-placeholder]').forEach(function (el) {
            el.classList.add('d-none');
        });

        document.querySelectorAll('[data-profile-photo]').forEach(function (img) {
            img.src = url;
            img.classList.remove('d-none');
        });

        document.querySelectorAll('#userMenuDropdown, .profile-photo-open').forEach(function (container) {
            if (container.querySelector('[data-profile-photo]')) return;
            var placeholder = container.querySelector('[data-profile-photo-placeholder]');
            if (!placeholder) return;
            var img = document.createElement('img');
            img.setAttribute('data-profile-photo', '');
            img.className = 'rounded-circle';
            img.alt = container.id === 'userMenuDropdown' ? 'Profile' : '';
            var size = container.id === 'userMenuDropdown' ? 36 : 48;
            img.width = size;
            img.height = size;
            img.style.objectFit = 'cover';
            if (container.id !== 'userMenuDropdown') {
                img.style.display = 'block';
            }
            img.src = url;
            container.insertBefore(img, placeholder);
        });

        modalEl.dataset.photoUrl = url;
    }

    document.querySelectorAll('.profile-photo-open').forEach(function (trigger) {
        trigger.addEventListener('click', function (event) {
            event.preventDefault();
            clearError();
            if (fileInput) fileInput.value = '';
            resetPreview();
            modal.show();
        });
    });

    if (fileInput) {
        fileInput.addEventListener('change', function () {
            clearError();
            const file = fileInput.files && fileInput.files[0];
            if (!file || !preview || !previewPlaceholder) return;
            preview.src = URL.createObjectURL(file);
            preview.classList.remove('d-none');
            previewPlaceholder.classList.add('d-none');
        });
    }

    if (form) {
        form.addEventListener('submit', async function (event) {
            event.preventDefault();
            clearError();

            if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
                showError('Please select an image file.');
                return;
            }

            const formData = new FormData(form);
            formData.set('photo', fileInput.files[0]);
            // Keep a single antiforgery token (form tag helper may coexist with other page tokens).
            var tokenField = form.querySelector('input[name="__RequestVerificationToken"]');
            if (tokenField) {
                formData.delete('__RequestVerificationToken');
                formData.set('__RequestVerificationToken', tokenField.value);
            }

            submitBtn.disabled = true;
            try {
                const response = await fetch(form.action, {
                    method: 'POST',
                    body: formData,
                    headers: {
                        'Accept': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    credentials: 'same-origin'
                });

                const data = await response.json().catch(function () { return null; });
                if (!response.ok || !data || !data.success) {
                    if (response.status === 400 && !data) {
                        showError('Upload was rejected. Refresh the page and try again.');
                    } else {
                        showError((data && data.error) || 'Upload failed. Please try again.');
                    }
                    return;
                }

                const photoUrl = data.photoUrl + (data.photoUrl.indexOf('?') >= 0 ? '&' : '?') + 't=' + Date.now();
                modalEl.dataset.photoUrl = data.photoUrl;
                updateNavPhotos(photoUrl);
                if (preview) {
                    preview.src = photoUrl;
                    preview.classList.remove('d-none');
                }
                if (previewPlaceholder) previewPlaceholder.classList.add('d-none');
                modal.hide();
            } catch {
                showError('Upload failed. Please try again.');
            } finally {
                submitBtn.disabled = false;
            }
        });
    }
})();
