export class KeySelectLayout {
    constructor(actions) {
        this.actions = actions;

        this.root = null;
        this.keysWrap = null;
        this.commitBtn = null;

        this.selectedKeys = new Set();
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
            <div class="keys-wrap"></div>
            <button type="button" class="commit-btn">Auswahl bestätigen</button>
        `;

        host.appendChild(root);

        this.root = root;
        this.keysWrap = root.querySelector(".keys-wrap");
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

        // Der Ruecksetzstand gehoert dazu: ohne ihn bleibt die Kennung beim Zuruecksetzen
        // gleich, das Layout wird nicht neu gebaut, und wer schon abgegeben hatte, blieb fuer
        // immer gesperrt - der Bestaetigen-Knopf trug weiter "Abgegeben".
        const resetCount = context?.resetCount ?? 0;

        return JSON.stringify({
            dic,
            showDesignations,
            maxSelections,
            questionId,
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
        this.commitBtn.textContent = "Auswahl bestätigen";

        for (const [key, designation] of entries) {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "key-btn";
            btn.dataset.key = key;
            btn.textContent = showDesignations
                ? `${key}: ${designation}`
                : key;

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

                this.updateCommitState();
            });

            this.keysWrap.appendChild(btn);
        }

        this.updateCommitState();
        this.setLocked(context.currentLayoutLocked || context.allLocked);
    }

    // Gewaehlt wird nicht allein ueber die Farbe angezeigt: der Haken kommt aus der CSS,
    // aria-pressed sagt es der Vorlesehilfe.
    markSelected(btn, selected) {
        btn.classList.toggle("selected", selected);
        btn.setAttribute("aria-pressed", String(selected));
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

        this.commitBtn.disabled = this.locked || this.selectedKeys.size === 0;
    }

    async handleCommitClick() {
        if (this.locked) return;
        if (!this.lastContext) return;
        if (this.selectedKeys.size === 0) return;

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
            window.show_toast("info", "Auswahl übermittelt");
        }
    }

    dispose() {
        if (this.commitBtn) {
            this.commitBtn.removeEventListener("click", this.handleCommitClick);
        }
    }
}