export class KeySelectLayout {
    constructor(actions) {
        this.actions = actions;

        this.root = null;
        this.keysWrap = null;
        this.commitBtn = null;

        this.selectedKeys = new Set();
        this.ordered = false;
        this.maxSelections = 1;
        this.prompts = [];
        this.promptLine = null;
        this.trueFalse = false;
        this.urteile = new Map();
        this.tfCount = 0;
        this.locked = true;
        this.submitted = false;

        this.lastSignature = "";
        this.lastContext = null;
        this.lastQuestionId = "";

        this.handleCommitClick = this.handleCommitClick.bind(this);
    }

    render(host, context) {
        const root = document.createElement("div");
        root.className = "layout layout-keyselect";
        root.innerHTML = `
            <div class="prompt-line" hidden></div>
            <div class="keys-wrap"></div>
            <button type="button" class="commit-btn">Auswahl bestätigen</button>
        `;

        host.appendChild(root);

        this.root = root;
        this.keysWrap = root.querySelector(".keys-wrap");
        this.promptLine = root.querySelector(".prompt-line");
        this.commitBtn = root.querySelector(".commit-btn");
        this.lastContext = context;

        this.commitBtn.addEventListener("click", this.handleCommitClick);

        this.rebuild(context);
    }

    update(context) {
        this.lastContext = context;

        const signature = this.createSignature(context);

        if (signature !== this.lastSignature) {
            this.rebuild(context);
            return;
        }

        this.setLocked(context.currentLayoutLocked || context.allLocked);
    }

    createSignature(context) {
        const info = context?.layoutInfo ?? {};
        const dic = info.keysAndDesignations ?? {};
        const showDesignations = info.showDesignations !== false;
        const maxSelections = info.maxAllowedSelections ?? 1;
        const questionId = info.questionId ?? "";
        const ordered = info.ordered === true;
        const prompts = info.prompts ?? [];
        const trueFalse = info.trueFalse === true;

        // Der Ruecksetzstand gehoert dazu: ohne ihn bleibt die Kennung beim Zuruecksetzen
        // gleich, das Layout wird nicht neu gebaut, und wer schon abgegeben hatte, blieb fuer
        // immer gesperrt - der Bestaetigen-Knopf trug weiter "Abgegeben".
        const resetCount = context?.resetCount ?? 0;

        return JSON.stringify({
            dic,
            showDesignations,
            maxSelections,
            questionId,
            ordered,
            prompts,
            trueFalse,
            resetCount
        });
    }

    rebuild(context) {
        if (!this.keysWrap) return;

        const info = context.layoutInfo ?? {};
        const dic = info.keysAndDesignations ?? {};
        const showDesignations = info.showDesignations !== false;
        const maxSelections = info.maxAllowedSelections ?? 1;
        const questionId = info.questionId ?? "";

        // Reihenfolgefrage (M12): die Klickfolge ist die Antwort, und abgegeben wird erst,
        // wenn alle Tasten gewaehlt sind. Ein Set behaelt die Einfuegereihenfolge - genau die
        // geht an den Server.
        this.ordered = info.ordered === true;
        this.maxSelections = maxSelections;
        this.prompts = Array.isArray(info.prompts) ? info.prompts : [];
        this.trueFalse = info.trueFalse === true;
        this.urteile = new Map();

        const canReselect =
            !!questionId &&
            !!this.lastQuestionId &&
            questionId === this.lastQuestionId;

        const previouslySelected = canReselect
            ? new Set(this.selectedKeys)
            : new Set();

        const entries = Object.entries(dic)
            .sort(([aKey], [bKey]) => aKey.localeCompare(bKey, undefined, { numeric: true, sensitivity: "base" }));

        const gridSize = Math.ceil(Math.sqrt(entries.length || 1));

        this.lastSignature = this.createSignature(context);
        this.lastQuestionId = questionId;

        this.keysWrap.style.setProperty("--grid-size", gridSize);
        this.keysWrap.innerHTML = "";
        this.selectedKeys = new Set();

        // Neue Runde: die Abgabe der letzten gilt nicht mehr.
        this.submitted = false;
        this.commitBtn.textContent = this.commitLabel();

        if (this.trueFalse) {
            this.keysWrap.style.setProperty("--grid-size", 1);
            this.buildTrueFalse(entries, showDesignations);
            this.updateCommitState();
            this.setLocked(context.currentLayoutLocked || context.allLocked);
            return;
        }

        for (const [key, designation] of entries) {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "key-btn";
            btn.dataset.key = key;
            btn.dataset.label = showDesignations
                ? `${key}: ${designation}`
                : key;
            btn.textContent = btn.dataset.label;

            btn.setAttribute("aria-pressed", "false");

            if (previouslySelected.has(key)) {
                this.markSelected(btn, true);
                this.selectedKeys.add(key);
            }

            btn.addEventListener("click", () => {
                if (this.locked) return;

                if (this.selectedKeys.has(key)) {
                    this.selectedKeys.delete(key);
                    this.markSelected(btn, false);
                } else {
                    if (this.selectedKeys.size >= maxSelections) {
                        if (typeof window.show_toast === "function") {
                            window.show_toast(
                                "warning",
                                `Maximal ${maxSelections} Auswahl${maxSelections > 1 ? "en" : ""} erlaubt`
                            );
                        }
                        return;
                    }

                    this.selectedKeys.add(key);
                    this.markSelected(btn, true);
                }

                this.updatePositions();
                this.updateCommitState();
            });

            this.keysWrap.appendChild(btn);
        }

        this.updatePositions();
        this.updateCommitState();
        this.setLocked(context.currentLayoutLocked || context.allLocked);
    }

    // Gewaehlt wird nicht allein ueber die Farbe angezeigt: der Haken kommt aus der CSS,
    // aria-pressed sagt es der Vorlesehilfe.
    markSelected(btn, selected) {
        btn.classList.toggle("selected", selected);
        btn.setAttribute("aria-pressed", String(selected));
    }

    // Wahr oder falsch (M12): je Aussage eine Zeile mit zwei Knoepfen. Abgegeben werden die
    // Tasten der als wahr beurteilten Aussagen - erst wenn jede beurteilt ist, damit eine leere
    // Abgabe "alles falsch" heisst und nicht "nichts getippt".
    buildTrueFalse(entries, showDesignations) {
        this.tfCount = entries.length;

        for (const [key, designation] of entries) {
            const row = document.createElement("div");
            row.className = "tf-row";

            const label = document.createElement("div");
            label.className = "tf-label";
            label.textContent = showDesignations ? `${key}: ${designation}` : key;
            row.appendChild(label);

            const buttons = [];

            for (const [wert, text] of [[true, "wahr"], [false, "falsch"]]) {
                const btn = document.createElement("button");
                btn.type = "button";
                btn.className = wert ? "key-btn tf-btn tf-true" : "key-btn tf-btn tf-false";
                btn.dataset.key = key;
                btn.dataset.label = text;
                btn.textContent = text;
                btn.setAttribute("aria-pressed", "false");
                btn.setAttribute("aria-label", `${key} ${text}`);

                btn.addEventListener("click", () => {
                    if (this.locked) return;

                    this.urteile.set(key, wert);

                    for (const b of buttons)
                        this.markSelected(b, b === btn);

                    this.updateCommitState();
                });

                buttons.push(btn);
                row.appendChild(btn);
            }

            this.keysWrap.appendChild(row);
        }
    }

    commitLabel() {
        if (this.trueFalse) return "Urteile abgeben";
        if (this.prompts.length > 0) return "Zuordnung bestätigen";
        return this.ordered ? "Reihenfolge bestätigen" : "Auswahl bestätigen";
    }

    // Zuordnung: zu welchem Begriff gerade gewaehlt wird. Ohne diese Zeile muesste der Spieler
    // zum Beamer schauen, um zu wissen, wofuer sein naechster Tipp zaehlt.
    updatePrompt() {
        if (!this.promptLine) return;

        if (this.prompts.length === 0) {
            this.promptLine.hidden = true;
            this.promptLine.textContent = "";
            return;
        }

        const next = this.selectedKeys.size;

        this.promptLine.hidden = false;
        this.promptLine.textContent = next < this.prompts.length
            ? `Zu ${next + 1} · ${this.prompts[next]} passt …`
            : "Alles zugeordnet - bitte bestätigen.";
    }

    // Bei der Reihenfolge steht der Platz vorn auf der Taste ("2. B: Text") - ein Haken
    // allein sagte nicht, an welcher Stelle das Element liegt.
    updatePositions() {
        if (!this.root) return;

        this.updatePrompt();

        const order = [...this.selectedKeys];

        this.root.querySelectorAll(".key-btn").forEach(btn => {
            const label = btn.dataset.label ?? btn.dataset.key;
            const pos = order.indexOf(btn.dataset.key);

            if (this.ordered && pos >= 0) {
                btn.textContent = `${pos + 1}. ${label}`;
                btn.dataset.position = String(pos + 1);
            } else {
                btn.textContent = label;
                delete btn.dataset.position;
            }
        });
    }

    setLocked(locked) {
        this.locked = !!locked;

        if (!this.root) return;

        this.root.querySelectorAll(".key-btn").forEach(btn => {
            btn.disabled = this.locked;
        });

        this.updateCommitState();
    }

    updateCommitState() {
        if (!this.commitBtn) return;

        if (this.submitted) {
            this.commitBtn.disabled = true;
            return;
        }

        if (this.trueFalse) {
            this.commitBtn.disabled = this.locked || this.urteile.size !== this.tfCount;
            return;
        }

        const incomplete = this.ordered && this.selectedKeys.size !== this.maxSelections;

        this.commitBtn.disabled = this.locked || this.selectedKeys.size === 0 || incomplete;
    }

    async handleCommitClick() {
        if (this.locked) return;
        if (!this.lastContext) return;
        if (this.trueFalse) {
            if (this.urteile.size !== this.tfCount) return;
        } else {
            if (this.selectedKeys.size === 0) return;
            if (this.ordered && this.selectedKeys.size !== this.maxSelections) return;
        }

        const keys = this.trueFalse
            ? [...this.urteile.entries()].filter(([, wahr]) => wahr).map(([key]) => key).sort()
            : [...this.selectedKeys];

        const payload = {
            playerId: this.lastContext.playerId,
            selectedKeys: keys,
            committedResult: true
        };

        this.setLocked(true);

        try {
            await this.actions.submitSelection(payload);
        } catch (e) {
            this.setLocked(false);

            if (typeof window.show_toast === "function") {
                window.show_toast("error", e?.message ?? "Speichern fehlgeschlagen");
            }

            return;
        }

        // Erst jetzt melden. Bis 2026-09-05 stand "Auswahl gespeichert" schon da, bevor der
        // Server geantwortet hatte - auch dann, wenn er die Abgabe still verwarf.
        this.submitted = true;
        this.commitBtn.textContent = "Abgegeben ✓";
        this.updateCommitState();

        if (typeof window.show_toast === "function") {
            window.show_toast("info", this.trueFalse
                ? "Urteile übermittelt"
                : this.prompts.length > 0
                ? "Zuordnung übermittelt"
                : this.ordered ? "Reihenfolge übermittelt" : "Auswahl übermittelt");
        }
    }

    dispose() {
        if (this.commitBtn) {
            this.commitBtn.removeEventListener("click", this.handleCommitClick);
        }
    }
}