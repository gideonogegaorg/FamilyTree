(function () {
    'use strict';

    var page = document.querySelector('.ft-tree-page');
    if (!page) return;

    var pickerBtn = page.querySelector('.ft-tree-picker-btn');
    var pickerMenu = page.querySelector('.ft-tree-picker-menu');
    var viewPickerBtn = page.querySelector('.ft-view-picker-btn');
    var viewPickerMenu = page.querySelector('.ft-view-picker-menu');

    function closeTreePicker() {
        if (pickerMenu) pickerMenu.hidden = true;
        if (pickerBtn) pickerBtn.setAttribute('aria-expanded', 'false');
    }

    function closeViewPicker() {
        if (viewPickerMenu) viewPickerMenu.hidden = true;
        if (viewPickerBtn) viewPickerBtn.setAttribute('aria-expanded', 'false');
    }

    if (pickerBtn && pickerMenu) {
        pickerBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            closeViewPicker();
            var isOpen = !pickerMenu.hidden;
            pickerMenu.hidden = isOpen;
            pickerBtn.setAttribute('aria-expanded', isOpen ? 'false' : 'true');
        });
        pickerMenu.addEventListener('click', function (e) {
            e.stopPropagation();
        });
    }

    if (viewPickerBtn && viewPickerMenu) {
        viewPickerBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            closeTreePicker();
            var isOpen = !viewPickerMenu.hidden;
            viewPickerMenu.hidden = isOpen;
            viewPickerBtn.setAttribute('aria-expanded', isOpen ? 'false' : 'true');
        });
        viewPickerMenu.addEventListener('click', function (e) {
            e.stopPropagation();
        });
    }

    document.addEventListener('click', function () {
        closeTreePicker();
        closeViewPicker();
    });

    page.querySelectorAll('.ft-tree-picker-delete-form').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            var nameBtn = form.closest('.ft-tree-picker-row') &&
                form.closest('.ft-tree-picker-row').querySelector('.ft-tree-picker-item');
            var treeName = nameBtn ? nameBtn.textContent.trim() : 'this tree';
            if (!window.confirm('Delete "' + treeName + '"? This cannot be undone.' +
                (document.querySelectorAll('.ft-tree-picker-item').length <= 1
                    ? ' A new empty Default tree will be created.'
                    : ''))) {
                e.preventDefault();
            }
        });
    });

    var jumpBtn = page.querySelector('.ft-jump-to-me');
    if (jumpBtn) {
        jumpBtn.addEventListener('click', function () {
            var id = jumpBtn.getAttribute('data-focus-member-id');
            if (!id) return;
            if (window.FamilyTreeViewport && window.FamilyTreeViewport.focusMember(id)) return;
            var el = document.getElementById('member-' + id);
            if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'center' });
        });
    }
})();
