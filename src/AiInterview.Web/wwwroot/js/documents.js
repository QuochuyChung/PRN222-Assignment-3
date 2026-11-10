// Documents page: client-side file checks, live progress over SignalR, polling fallback when SignalR is unavailable.
// All server text is written with textContent, never innerHTML.
(function () {
    'use strict';

    var allowed = ['pdf', 'docx', 'pptx'];
    var form = document.getElementById('uploadForm');
    var input = document.getElementById('fileInput');
    var zone = document.getElementById('dropzone');
    var label = document.getElementById('dzLabel');
    var error = document.getElementById('fileError');
    var maxMb = Number(form.dataset.maxMb || 50);

    function check(file) {
        if (!file) return 'Choose a file first.';
        var ext = file.name.split('.').pop().toLowerCase();
        if (allowed.indexOf(ext) < 0) return 'Only PDF, DOCX and PPTX files are supported.';
        if (file.size === 0) return 'The file is empty.';
        if (file.size > maxMb * 1024 * 1024) return 'The file is larger than ' + maxMb + ' MB.';
        return null;
    }

    function showFile() {
        var file = input.files[0];
        var problem = file ? check(file) : null;
        error.hidden = !problem;
        error.textContent = problem || '';
        label.textContent = file ? file.name : 'Drop a file here or browse';
        zone.classList.toggle('has-file', !!file && !problem);
    }

    input.addEventListener('change', showFile);
    ['dragenter', 'dragover'].forEach(function (e) {
        zone.addEventListener(e, function (ev) { ev.preventDefault(); zone.classList.add('is-over'); });
    });
    ['dragleave', 'drop'].forEach(function (e) {
        zone.addEventListener(e, function (ev) { ev.preventDefault(); zone.classList.remove('is-over'); });
    });
    zone.addEventListener('drop', function (ev) {
        if (ev.dataTransfer.files.length) { input.files = ev.dataTransfer.files; showFile(); }
    });
    form.addEventListener('submit', function (ev) {
        var problem = check(input.files[0]);
        if (problem) { ev.preventDefault(); error.hidden = false; error.textContent = problem; return; }
        var btn = document.getElementById('uploadBtn');
        btn.disabled = true;
        btn.querySelector('.label').textContent = 'Uploading…';
    });

    document.querySelectorAll('form[data-confirm]').forEach(function (f) {
        f.addEventListener('submit', function (ev) { if (!confirm(f.dataset.confirm)) ev.preventDefault(); });
    });

    document.querySelectorAll('time[datetime]').forEach(function (t) {
        var d = new Date(t.getAttribute('datetime'));
        if (!isNaN(d)) t.textContent = d.toLocaleString([], { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
    });

    // ---- live status ----
    function row(id) { return document.querySelector('tr[data-id="' + CSS.escape(id) + '"]'); }

    // requestedAt is set for polled data: skip rows that a push event updated after the poll was sent (stale response).
    function apply(p, requestedAt) {
        var tr = row(p.documentId || p.id);
        if (!tr) return;
        if (requestedAt && Number(tr.dataset.touched || 0) > requestedAt) return;
        tr.dataset.touched = Date.now();
        var status = typeof p.status === 'number' ? ['Uploaded', 'Processing', 'Success', 'Failed'][p.status] : p.status;
        tr.dataset.status = status;

        var badge = tr.querySelector('[data-field="status"]');
        badge.className = 'status status-' + status.toLowerCase();
        badge.textContent = status;

        var prog = tr.querySelector('[data-field="progress"]');
        prog.hidden = status !== 'Processing';
        if (p.percent != null) prog.querySelector('.bar span').style.width = p.percent + '%';
        if (p.step) prog.querySelector('.step').textContent = p.step;

        var err = tr.querySelector('[data-field="error"]');
        err.hidden = !p.errorMessage;
        err.textContent = p.errorMessage || '';

        if (status !== 'Processing') tr.querySelector('[data-field="counts"]').textContent = (p.pageCount || 0) + ' · ' + (p.chunkCount || 0);
        tr.querySelector('[data-action="reindex"]').disabled = status === 'Processing';
        tally();
    }

    function tally() {
        var rows = document.querySelectorAll('tr[data-id]');
        var counts = { all: rows.length, Success: 0, Processing: 0, Failed: 0 };
        rows.forEach(function (r) { if (counts[r.dataset.status] != null) counts[r.dataset.status]++; });
        Object.keys(counts).forEach(function (k) {
            var el = document.querySelector('[data-count="' + k + '"]');
            if (el) el.textContent = counts[k];
        });
    }

    var note = document.getElementById('liveNote');
    function setNote(state, text) { note.dataset.state = state; note.lastElementChild.textContent = text; }

    function sync() {
        var requestedAt = Date.now();
        return fetch('?handler=Status', { headers: { Accept: 'application/json' } })
            .then(function (r) { return r.ok ? r.json() : []; })
            .then(function (list) { list.forEach(function (p) { apply(p, requestedAt); }); })
            .catch(function () { /* next tick will retry */ });
    }

    var pollTimer = null;
    function startPolling() {
        if (pollTimer) return;
        setNote('poll', 'Live connection unavailable, refreshing every 3 seconds.');
        pollTimer = setInterval(function () {
            if (document.querySelector('tr[data-status="Processing"]')) sync();
        }, 3000);
    }
    function stopPolling() { clearInterval(pollTimer); pollTimer = null; }

    if (!window.signalR) { startPolling(); return; }

    var connection = new signalR.HubConnectionBuilder().withUrl('/hubs/documents').withAutomaticReconnect().build();
    connection.on('progress', function (p) { apply(p); });
    connection.on('deleted', function (id) { var tr = row(id); if (tr) { tr.remove(); tally(); } });
    connection.onreconnecting(startPolling);
    connection.onreconnected(function () { stopPolling(); setNote('live', 'Live updates on.'); sync(); });
    connection.onclose(startPolling);

    // Safety net: even with a live connection, re-check slowly while something is processing, so a missed push
    // (for example the worker finished before this page connected) can never leave a row stuck on "Processing".
    var safetyTimer = null;
    function startSafetyPoll() {
        if (safetyTimer) return;
        safetyTimer = setInterval(function () {
            if (document.querySelector('tr[data-status="Processing"]')) sync();
        }, 10000);
    }

    connection.start()
        .then(function () { setNote('live', 'Live updates on.'); sync(); startSafetyPoll(); })
        .catch(startPolling);
})();
