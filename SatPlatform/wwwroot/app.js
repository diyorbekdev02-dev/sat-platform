// SAT Platform — small browser helpers used via JS interop.
window.sat = (() => {
    const theme = {
        get: () => { try { return localStorage.getItem("theme") || ""; } catch { return ""; } },
        set: (t) => { try { if (t) localStorage.setItem("theme", t); else localStorage.removeItem("theme"); } catch { } if (t) document.documentElement.setAttribute("data-theme", t); else document.documentElement.removeAttribute("data-theme"); }
    };

    // ---- KaTeX rendering of $...$ / $$...$$ in a container
    function renderMath(el) {
        const root = el ? (typeof el === "string" ? document.getElementById(el) : el) : document.body;
        if (!root || !window.renderMathInElement) return;
        try {
            renderMathInElement(root, {
                delimiters: [{ left: "$$", right: "$$", display: true }, { left: "\\[", right: "\\]", display: true }, { left: "$", right: "$", display: false }, { left: "\\(", right: "\\)", display: false }],
                throwOnError: false, ignoredTags: ["script", "noscript", "style", "textarea", "pre", "code"], ignoredClasses: ["ql-editor", "no-math"]
            });
        } catch (e) { console.warn("katex", e); }
    }

    // ---- server-synchronised countdown
    const timers = {};
    function startTimer(id, secondsLeft, dotnetRef) {
        stopTimer(id);
        const end = Date.now() + secondsLeft * 1000;
        const el = document.getElementById(id);
        let lastWhole = -1;
        const tick = () => {
            const left = Math.max(0, Math.round((end - Date.now()) / 1000));
            if (el && left !== lastWhole) {
                lastWhole = left;
                const h = Math.floor(left / 3600), m = Math.floor(left % 3600 / 60), s = left % 60;
                el.textContent = (h > 0 ? h + ":" : "") + String(m).padStart(2, "0") + ":" + String(s).padStart(2, "0");
                el.classList.toggle("critical", left <= 300 && left > 0);
            }
            if (left <= 0) { stopTimer(id); try { dotnetRef && dotnetRef.invokeMethodAsync("OnTimerExpired"); } catch { } }
        };
        tick();
        timers[id] = setInterval(tick, 500);
    }
    function stopTimer(id) { if (timers[id]) { clearInterval(timers[id]); delete timers[id]; } }

    // ---- local backup of answers (IndexedDB-like persistence via localStorage; survives reloads and connection loss)
    const backup = {
        key: (attempt) => "sat.attempt." + attempt,
        save: (attempt, qid, answer) => {
            try { const k = backup.key(attempt); const d = JSON.parse(localStorage.getItem(k) || "{}"); d[qid] = { a: answer, t: Date.now() }; localStorage.setItem(k, JSON.stringify(d)); } catch { }
        },
        pending: (attempt) => { try { return JSON.parse(localStorage.getItem(backup.key(attempt)) || "{}"); } catch { return {}; } },
        clear: (attempt, qid) => {
            try { const k = backup.key(attempt); const d = JSON.parse(localStorage.getItem(k) || "{}"); if (qid === undefined) localStorage.removeItem(k); else { delete d[qid]; localStorage.setItem(k, JSON.stringify(d)); } } catch { }
        }
    };

    // ---- device check
    function deviceCheck() {
        const r = { online: navigator.onLine, width: window.innerWidth, height: window.innerHeight, localStorage: false, indexedDb: !!window.indexedDB, browser: navigator.userAgent, cookies: navigator.cookieEnabled };
        try { localStorage.setItem("sat.test", "1"); localStorage.removeItem("sat.test"); r.localStorage = true; } catch { }
        return r;
    }

    // ---- leave guard during a test
    let guard = false;
    window.addEventListener("beforeunload", (e) => { if (guard) { e.preventDefault(); e.returnValue = ""; } });
    function setLeaveGuard(on) { guard = !!on; }

    // ---- keyboard shortcuts in the test
    let keyRef = null;
    document.addEventListener("keydown", (e) => {
        if (!keyRef) return;
        const tag = (e.target && e.target.tagName || "").toLowerCase();
        if (tag === "input" || tag === "textarea" || (e.target && e.target.isContentEditable)) return;
        const map = { ArrowRight: "next", ArrowLeft: "back", f: "flag", F: "flag", g: "grid", G: "grid", a: "A", b: "B", c: "C", d: "D", A: "A", B: "B", C: "C", D: "D", "1": "A", "2": "B", "3": "C", "4": "D" };
        const cmd = map[e.key];
        if (cmd) { e.preventDefault(); try { keyRef.invokeMethodAsync("OnKey", cmd); } catch { } }
    });
    function setKeyHandler(ref) { keyRef = ref; }

    // ---- line reader
    let reader = null;
    function toggleLineReader() {
        if (reader) { reader.remove(); reader = null; document.removeEventListener("mousemove", moveReader); return false; }
        reader = document.createElement("div"); reader.className = "line-reader"; document.body.appendChild(reader);
        document.addEventListener("mousemove", moveReader); return true;
    }
    function moveReader(e) { if (reader) reader.style.top = (e.clientY - 24) + "px"; }

    // ---- highlight (annotation) of selected text in the passage
    function highlightSelection() {
        const sel = window.getSelection();
        if (!sel || sel.rangeCount === 0 || sel.isCollapsed) return;
        const range = sel.getRangeAt(0);
        try { const mark = document.createElement("mark"); mark.className = "annot"; range.surroundContents(mark); sel.removeAllRanges(); } catch { }
    }
    function clearHighlights() { document.querySelectorAll("mark.annot").forEach(m => { const p = m.parentNode; while (m.firstChild) p.insertBefore(m.firstChild, m); p.removeChild(m); }); }

    // ---- Quill rich editor
    const editors = {};
    function initEditor(id, html, dotnetRef, placeholder) {
        const el = document.getElementById(id);
        if (!el || !window.Quill) return;
        if (editors[id]) { editors[id].root.innerHTML = html || ""; return; }
        const q = new Quill(el, {
            theme: "snow", placeholder: placeholder || "",
            modules: { toolbar: { container: el.closest(".compact") ? [["bold", "italic", "underline"], [{ script: "sub" }, { script: "super" }], ["image", "formula"]] : [["bold", "italic", "underline"], [{ list: "ordered" }, { list: "bullet" }], [{ script: "sub" }, { script: "super" }], ["link", "image", "formula"], ["clean"]],
                handlers: { image: () => { const input = document.createElement("input"); input.type = "file"; input.accept = "image/*"; input.onchange = async () => { const f = input.files[0]; if (!f) return; const url = await uploadImage(f); if (url) { const r = q.getSelection(true); q.insertEmbed(r ? r.index : 0, "image", url, "user"); } }; input.click(); } } } }
        });
        q.root.innerHTML = html || "";
        q.on("text-change", () => { try { dotnetRef.invokeMethodAsync("OnEditorChanged", q.root.innerHTML); } catch { } });
        editors[id] = q;
    }
    function destroyEditor(id) { delete editors[id]; }
    async function uploadImage(file) {
        const fd = new FormData(); fd.append("file", file);
        try { const r = await fetch("/api/upload-image", { method: "POST", body: fd }); if (!r.ok) { alert("Rasm yuklanmadi: " + (await r.text())); return null; } return (await r.json()).url; } catch (e) { alert("Rasm yuklanmadi"); return null; }
    }
    function insertTable(id, rows, cols) {
        const q = editors[id]; if (!q) return;
        let html = "<table>"; for (let r = 0; r < rows; r++) { html += "<tr>"; for (let c = 0; c < cols; c++) html += r === 0 ? "<th>&nbsp;</th>" : "<td>&nbsp;</td>"; html += "</tr>"; } html += "</table>";
        const sel = q.getSelection(true); q.clipboard.dangerouslyPasteHTML(sel ? sel.index : q.getLength(), html);
    }

    function download(name, content, type) {
        const blob = new Blob([content], { type: type || "application/octet-stream" }); const url = URL.createObjectURL(blob);
        const a = document.createElement("a"); a.href = url; a.download = name; document.body.appendChild(a); a.click(); a.remove(); setTimeout(() => URL.revokeObjectURL(url), 2000);
    }
    function copy(text) { try { navigator.clipboard.writeText(text); return true; } catch { return false; } }
    function focus(id) { const el = document.getElementById(id); if (el) el.focus(); }
    function scrollTop(id) { const el = id ? document.getElementById(id) : window; if (el) el.scrollTo({ top: 0 }); }
    function confirm(msg) { return window.confirm(msg); }
    function print() { window.print(); }
    // heartbeat driven by the browser: stops as soon as the tab is gone, so another window can take over the test
    let hb = null;
    function startHeartbeat(ref) { stopHeartbeat(); hb = setInterval(() => { try { ref.invokeMethodAsync("OnHeartbeat"); } catch { } }, 5000); }
    function stopHeartbeat() { if (hb) { clearInterval(hb); hb = null; } }
    function ready() { document.documentElement.setAttribute("data-ready", "1"); }

    return { ready, startHeartbeat, stopHeartbeat, theme, renderMath, startTimer, stopTimer, backup, deviceCheck, setLeaveGuard, setKeyHandler, toggleLineReader, highlightSelection, clearHighlights, initEditor, destroyEditor, insertTable, download, copy, focus, scrollTop, confirm, print };
})();

// Uzbek phone mask for every <input data-phone>: always "+998 90 123 45 67", digits only, at most 9 after +998.
// Capture phase on document, so Blazor (@bind oninput/onchange) already reads the formatted value.
document.addEventListener("input", function (e) {
    const el = e.target;
    if (!(el instanceof HTMLInputElement) || !el.hasAttribute("data-phone")) return;
    const v = el.value;
    let d;
    if (v.startsWith("+998")) d = v.slice(4).replace(/\D/g, "");
    else if ("+998 ".startsWith(v)) d = "";                      // the prefix itself is being erased
    else {                                                       // pasted "90 123 45 67", "0901234567" or "998901234567"
        d = v.replace(/\D/g, "");
        if (d.length > 9 && d.startsWith("998")) d = d.slice(3);
        else if (d.length > 9 && d.startsWith("0")) d = d.slice(1);
    }
    d = d.slice(0, 9);
    const parts = [d.slice(0, 2), d.slice(2, 5), d.slice(5, 7), d.slice(7, 9)].filter(Boolean);
    const out = "+998 " + parts.join(" ");
    if (el.value !== out) el.value = out;
}, true);
