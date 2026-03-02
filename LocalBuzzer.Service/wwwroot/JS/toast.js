// show_toast(type, msg)
// types: "debug" | "info" | "warning" | "error"
(function () {
    const TYPES = new Set(["debug", "info", "warning", "error"]);
    let host = null;

    function ensureHost() {
        host = document.getElementById("toast-host");
        if (!host) {
            host = document.createElement("div");
            host.id = "toast-host";
            document.body.appendChild(host);
        }
        return host;
    }

    function normalizeType(type) {
        const t = (type ?? "").toString().toLowerCase();
        return TYPES.has(t) ? t : "info";
    }

    window.show_toast = function show_toast(type, msg, options = {}) {
        const t = normalizeType(type);
        const text = (msg ?? "").toString();

        const {
            timeout = (t === "error" ? 6000 : 3000),
            closable = true
        } = options;

        const h = ensureHost();

        const el = document.createElement("div");
        el.className = `toast ${t}`;

        const msgEl = document.createElement("div");
        msgEl.className = "toast-msg";
        msgEl.textContent = text;

        el.appendChild(msgEl);

        let timer = null;
        const remove = () => {
            if (timer) clearTimeout(timer);
            el.classList.remove("show");
            // wait for transition then remove
            setTimeout(() => el.remove(), 200);
        };

        if (closable) {
            const btn = document.createElement("button");
            btn.className = "toast-close";
            btn.type = "button";
            btn.setAttribute("aria-label", "Close");
            btn.textContent = "×";
            btn.addEventListener("click", remove);
            el.appendChild(btn);
        }

        h.appendChild(el);

        // trigger transition
        requestAnimationFrame(() => el.classList.add("show"));

        if (timeout > 0) {
            timer = setTimeout(remove, timeout);
        }

        return { close: remove, element: el };
    };
})();