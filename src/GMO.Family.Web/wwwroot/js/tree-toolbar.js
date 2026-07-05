(function () {
    'use strict';

    var page = document.querySelector('.ft-tree-page');
    if (!page) return;

    var pickerBtn = page.querySelector('.ft-tree-picker-btn');
    var pickerMenu = page.querySelector('.ft-tree-picker-menu');
    if (pickerBtn && pickerMenu) {
        pickerBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            var isOpen = !pickerMenu.hidden;
            pickerMenu.hidden = isOpen;
            pickerBtn.setAttribute('aria-expanded', isOpen ? 'false' : 'true');
        });
        document.addEventListener('click', function () {
            pickerMenu.hidden = true;
            pickerBtn.setAttribute('aria-expanded', 'false');
        });
        pickerMenu.addEventListener('click', function (e) {
            e.stopPropagation();
        });
    }

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
