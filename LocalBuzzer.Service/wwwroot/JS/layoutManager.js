// M35 C3b: Auktion, Experte, Allianz - diese Frage beantworten nur einige. Der Server sperrt, hier steht es.
export const EXCLUDED_TEXT = "Diese Frage beantworten andere";

export class LayoutManager {
    constructor(state) {
        this.state = state;
        this.layouts = new Map();
        this.activeLayout = null;
        this.activeLayoutId = null;
        this.excluded = false;
        this.excludedReason = null;
    }

    register(layoutId, layout) {
        this.layouts.set(layoutId, layout);
    }

    switchTo(layoutId, context) {
        this.clear(context.host);
        this.activeLayoutId = layoutId;
        this.excluded = !!context.excluded;
        this.excludedReason = context.excludedReason ?? null;

        // Gesperrt fuer diese Frage: statt des Layouts der Satz, wer antwortet - nie nur ein grauer Knopf.
        // Mit eigenem Satz vom Server (M35 C7b: wer per Joker gesperrt hat) steht dieser.
        if (this.excluded) {
            this.showExcluded(context.host, this.excludedReason);
            return;
        }

        const layout = this.layouts.get(layoutId);
        if (!layout) {
            context.host.innerHTML = `<div class="layout-empty">Warten auf die nächste Frage …</div>`;
            return;
        }

        this.activeLayout = layout;
        layout.render(context.host, context);
        layout.setLocked?.(context.currentLayoutLocked || context.allLocked);
    }

    // Ersetzt das aktive Layout durch eine Meldung mit einem grossen Knopf (Verbindung verloren, abgeloest).
    showNotice(host, { text, buttonLabel, onButton }) {
        this.clear(host);

        const root = document.createElement("div");
        root.className = "layout layout-notice";

        const message = document.createElement("p");
        message.className = "notice-text";
        message.textContent = text;

        const btn = document.createElement("button");
        btn.type = "button";
        btn.className = "notice-btn";
        btn.textContent = buttonLabel;
        btn.addEventListener("click", onButton);

        root.append(message, btn);
        host.appendChild(root);
    }

    showExcluded(host, reason = null) {
        const root = document.createElement("div");
        root.className = "layout layout-ausgeschlossen";

        const message = document.createElement("p");
        message.className = "ausgeschlossen-text";
        message.textContent = reason || `${EXCLUDED_TEXT} – du schaust zu.`;

        root.appendChild(message);
        host.appendChild(root);
    }

    clear(host) {
        if (this.activeLayout?.dispose) {
            this.activeLayout.dispose();
        }

        this.activeLayout = null;
        this.activeLayoutId = null;
        host.innerHTML = "";
    }

    update(context) {
        // Die Sperre fuer diese Frage kam, ging oder bekam einen anderen Satz: neu aufbauen, nicht nur sperren.
        if (!!context.excluded !== this.excluded || (this.excluded && (context.excludedReason ?? null) !== this.excludedReason)) {
            this.switchTo(context.currentLayout ?? this.activeLayoutId, context);
            return;
        }

        if (!this.activeLayout) return;
        this.activeLayout.update?.(context);
        this.activeLayout.setLocked?.(context.currentLayoutLocked || context.allLocked);
    }

    lockCurrent() {
        this.activeLayout?.setLocked?.(true);
    }

    unlockCurrent() {
        this.activeLayout?.setLocked?.(false);
    }

    lockAll() {
        this.state.allLocked = true;
        this.activeLayout?.setLocked?.(true);
    }

    unlockAll() {
        this.state.allLocked = false;
        this.activeLayout?.setLocked?.(this.state.currentLayoutLocked);
    }
}
