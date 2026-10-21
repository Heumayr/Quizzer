// Selbst anmelden am Telefon (M10): Name aus der Liste waehlen oder als Gast eintragen,
// dann wartet das Telefon, bis der Spielleiter am Laptop bestaetigt.
//
// Es geht nur ein Name ueber das WLAN, nie ein Kennwort - der Buzzer laeuft ueber http.

export const PENDING_KEY = "quizzerJoinRequest";

export const Texts = Object.freeze({
    title: "Wer bist du?",
    guestLabel: "Nicht in der Liste? Als Gast:",
    guestButton: "Als Gast mitspielen",
    guestPlaceholder: "Dein Name",
    waiting: "Der Spielleiter muss dich noch bestätigen …",
    declined: "Der Spielleiter hat die Anfrage abgelehnt.",
    expired: "Die Anfrage ist abgelaufen.",
    back: "Zurück zur Auswahl",
    tooMany: "Gerade warten zu viele Anfragen. Bitte kurz warten und neu versuchen.",
    noGame: "Es läuft gerade kein Spiel. Der Spielleiter muss zuerst eines öffnen.",
    failed: "Das hat nicht geklappt. Bitte neu versuchen."
});

function el(tag, className, text) {
    const e = document.createElement(tag);
    if (className) e.className = className;
    if (text !== undefined) e.textContent = text;
    return e;
}

function clear(host) {
    host.children?.splice?.(0);
    if (typeof host.replaceChildren === "function") host.replaceChildren();
}

export class JoinFlow {
    constructor({ host, setStatus, fetchImpl, navigate, storage, pollMs = 2000, wait }) {
        this.host = host;
        this.setStatus = setStatus ?? (() => { });
        this.fetch = fetchImpl ?? ((...a) => fetch(...a));
        this.navigate = navigate ?? ((url) => location.replace(url));
        this.storage = storage ?? window.sessionStorage;
        this.pollMs = pollMs;
        this.wait = wait ?? ((ms) => new Promise(r => setTimeout(r, ms)));
        this.stopped = false;
    }

    async start() {
        const pending = this.storage.getItem(PENDING_KEY);
        if (pending) {
            return this.waitFor(pending);
        }

        return this.showChoices();
    }

    stop() { this.stopped = true; }

    async showChoices(message) {
        clear(this.host);
        this.setStatus(message ?? Texts.title);

        let candidates = [];
        try {
            const response = await this.fetch("/join/candidates");
            candidates = response.ok ? await response.json() : [];
        } catch {
            candidates = [];
        }

        const root = el("div", "layout layout-join");
        root.appendChild(el("p", "join-title", Texts.title));

        if (message) root.appendChild(el("p", "join-message", message));

        if (candidates.length === 0) {
            root.appendChild(el("p", "join-message", Texts.noGame));
        }

        const list = el("div", "join-list");
        for (const c of candidates) {
            const btn = el("button", "join-candidate", c.name);
            btn.type = "button";
            btn.dataset.candidateId = c.candidateId;
            btn.addEventListener("click", () => this.request({ candidateId: c.candidateId, guestName: null }));
            list.appendChild(btn);
        }
        root.appendChild(list);

        root.appendChild(el("p", "join-guest-label", Texts.guestLabel));
        const input = el("input", "join-guest-name");
        input.type = "text";
        input.maxLength = 40;
        input.placeholder = Texts.guestPlaceholder;
        root.appendChild(input);

        const guest = el("button", "join-guest", Texts.guestButton);
        guest.type = "button";
        guest.addEventListener("click", () => {
            const name = String(input.value ?? "").trim();
            if (name.length > 0) this.request({ candidateId: null, guestName: name });
        });
        root.appendChild(guest);

        this.host.appendChild(root);
        return root;
    }

    async request(payload) {
        let response;
        try {
            response = await this.fetch("/join/request", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
        } catch {
            return this.showChoices(Texts.failed);
        }

        if (response.status === 429) return this.showChoices(Texts.tooMany);
        if (!response.ok) return this.showChoices(Texts.failed);

        const { requestId } = await response.json();
        this.storage.setItem(PENDING_KEY, requestId);
        return this.waitFor(requestId);
    }

    async waitFor(requestId) {
        clear(this.host);
        const root = el("div", "layout layout-join");
        root.appendChild(el("p", "join-waiting", Texts.waiting));
        this.host.appendChild(root);
        this.setStatus(Texts.waiting);

        while (!this.stopped) {
            let status = null;
            try {
                const response = await this.fetch(`/join/status/${encodeURIComponent(requestId)}`);
                if (response.status === 404) {
                    this.storage.removeItem(PENDING_KEY);
                    return this.showChoices(Texts.expired);
                }
                status = response.ok ? await response.json() : null;
            } catch {
                status = null;
            }

            if (status?.status === "accepted" && status.accessKey) {
                this.storage.removeItem(PENDING_KEY);
                this.navigate(`/?key=${encodeURIComponent(status.accessKey)}`);
                return "accepted";
            }

            if (status?.status === "declined" || status?.status === "expired") {
                this.storage.removeItem(PENDING_KEY);
                return this.showChoices(status.status === "declined" ? Texts.declined : Texts.expired);
            }

            await this.wait(this.pollMs);
        }

        return "stopped";
    }
}
