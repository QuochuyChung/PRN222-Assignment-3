// Shared helpers for the AI Interview UI.

window.aiInterview = {
    /** Escapes text before inserting it into HTML. */
    escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    },

    /** Formats a byte count as KB / MB. */
    formatSize(bytes) {
        const kb = bytes / 1024;
        return kb >= 1024 ? (kb / 1024).toFixed(2) + " MB" : kb.toFixed(1) + " KB";
    }
};
