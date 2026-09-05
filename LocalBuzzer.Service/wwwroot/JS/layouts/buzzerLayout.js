export class BuzzerLayout {
    constructor(actions) {
        this.actions = actions;
        this.root = null;
        this.btn = null;
        this.locked = true;
        this.context = null;
        this.onPointerDown = this.onPointerDown.bind(this);
        this.onKeyDown = this.onKeyDown.bind(this);
    }

    render(host, context) {
        const root = document.createElement("div");
        root.className = "layout layout-buzzer";

        const btn = document.createElement("button");
        btn.type = "button";
        btn.className = "buzz-btn";
        btn.id = "buzz_btn";

        root.appendChild(btn);
        host.appendChild(root);

        this.root = root;
        this.btn = btn;
        // pointerdown statt click: der Buzzer reagiert beim Druecken, nicht erst beim Loslassen.
        btn.addEventListener("pointerdown", this.onPointerDown);
        btn.addEventListener("keydown", this.onKeyDown);

        this.update(context);
    }

    update(context) {
        this.context = context ?? this.context;
        this.refreshLabel();
    }

    setLocked(locked) {
        this.locked = !!locked;
        if (this.btn) {
            this.btn.disabled = this.locked;
        }
        this.refreshLabel();
    }

    refreshLabel() {
        if (!this.btn) return;
        this.btn.textContent = this.buildLabel();
    }

    buildLabel() {
        const winner = this.context?.winner ?? null;
        if (winner) {
            return winner === this.context?.playerName
                ? "Du bist dran!"
                : `${winner} war schneller`;
        }
        return this.locked ? "Gesperrt" : "BUZZ";
    }

    onPointerDown(e) {
        if (e.button > 0) return;
        this.press();
    }

    onKeyDown(e) {
        if (e.key !== "Enter" && e.key !== " ") return;
        e.preventDefault();
        if (e.repeat) return;
        this.press();
    }

    async press() {
        if (!this.btn || this.btn.disabled) return;

        // Lokale Sperre sofort, bevor der Aufruf rausgeht - ein zweiter Druck darf nichts mehr senden.
        this.btn.disabled = true;
        navigator.vibrate?.(40);

        try {
            await this.actions.buzz();
        } catch {
            // Fehler hat index.js schon gemeldet; nur die lokale Sperre auf den Serverstand zuruecksetzen.
            this.btn.disabled = this.locked;
        }
    }

    dispose() {
        if (this.btn) {
            this.btn.removeEventListener("pointerdown", this.onPointerDown);
            this.btn.removeEventListener("keydown", this.onKeyDown);
        }
    }
}
