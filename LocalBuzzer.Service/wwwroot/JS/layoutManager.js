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
        if (this.activeLayout?.dispose) {
            this.activeLayout.dispose();
        }

        this.activeLayout = null;
        this.activeLayoutId = layoutId;
        context.host.innerHTML = "";

        const layout = this.layouts.get(layoutId);
        if (!layout) {
            context.host.innerHTML = `<div class="layout-empty">Kein Layout aktiv.</div>`;
            return;
        }

        this.activeLayout = layout;
        layout.render(context.host, context);
        layout.setLocked?.(context.currentLayoutLocked || context.allLocked);
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