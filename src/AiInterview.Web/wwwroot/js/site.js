// Chọn bảng màu + thu gọn sidebar. Trạng thái lưu ở localStorage (tiện ích riêng của từng người dùng).
(function () {
    function safeSet(key, value) {
        try { localStorage.setItem(key, value); } catch (e) { /* trình duyệt chặn lưu trữ: bỏ qua */ }
    }

    document.querySelectorAll('[data-palette-choice]').forEach(function (button) {
        button.addEventListener('click', function () {
            var palette = button.getAttribute('data-palette-choice');
            document.documentElement.dataset.palette = palette;
            safeSet('palette', palette);
        });
    });

    var toggle = document.getElementById('sidebarToggle');
    if (toggle) {
        toggle.addEventListener('click', function () {
            var collapsed = document.documentElement.classList.toggle('sidebar-collapsed');
            safeSet('sidebar', collapsed ? 'collapsed' : 'expanded');
        });
    }
})();
