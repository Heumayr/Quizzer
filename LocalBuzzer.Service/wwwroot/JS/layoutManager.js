export class LayoutManager {
    constructor(state) {
        this.state = state;
        this.layouts = new Map();
        this.activeLayout = null;
        this.activeLayoutId = null;
    }

    register(layoutId, layout) {
        this.layouts.set(layoutId, layout);
    }

    switchTo(layoutId, context) {
        this.clear(context.host);
        this.activeLayoutId = layoutId;

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

    clear(host) {
        if (this.activeLayout?.dispose) {
            this.activeLayout.dispose();
        }

        this.activeLayout = null;
        this.activeLayoutId = null;
        host.innerHTML = "";
    }

    update(context) {
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
