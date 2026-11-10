// Desktop sidebar collapse. State is kept per browser in localStorage.
(function () {
    var toggle = document.getElementById('sidebarToggle');
    if (!toggle) return;
    toggle.addEventListener('click', function () {
        var collapsed = document.documentElement.classList.toggle('sidebar-collapsed');
        try { localStorage.setItem('sidebar', collapsed ? 'collapsed' : 'expanded'); } catch (e) { /* storage blocked: ignore */ }
    });
})();
