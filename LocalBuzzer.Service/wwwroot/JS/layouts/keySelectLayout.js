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

    commitLabel() {
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

        const incomplete = this.ordered && this.selectedKeys.size !== this.maxSelections;

        this.commitBtn.disabled = this.locked || this.selectedKeys.size === 0 || incomplete;
    }

    async handleCommitClick() {
        if (this.locked) return;
        if (!this.lastContext) return;
        if (this.selectedKeys.size === 0) return;
        if (this.ordered && this.selectedKeys.size !== this.maxSelections) return;

        const payload = {
            playerId: this.lastContext.playerId,
            selectedKeys: [...this.selectedKeys],
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
            window.show_toast("info", this.prompts.length > 0
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