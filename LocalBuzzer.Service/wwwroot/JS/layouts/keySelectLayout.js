export class KeySelectLayout {
    constructor(actions) {
        this.actions = actions;

        this.root = null;
        this.keysWrap = null;
        this.commitBtn = null;

        this.selectedKeys = new Set();
        this.locked = true;
    }

    render(host, context) {
        const root = document.createElement("div");
        root.className = "layout layout-keyselect";
        root.innerHTML = `
            <div class="keys-wrap"></div>
            <button class="commit-btn">Auswahl bestätigen</button>
        `;

        host.appendChild(root);

        this.root = root;
        this.keysWrap = root.querySelector(".keys-wrap");
        this.commitBtn = root.querySelector(".commit-btn");

        this.commitBtn.addEventListener("click", async () => {
            if (this.locked) return;

            await this.actions.submitSelection({
                playerId: context.playerId,
                selectedKeys: [...this.selectedKeys],
                committedResult: true
            });
        });

        this.rebuild(context);
    }

    update(context) {
        this.rebuild(context);
    }

    rebuild(context) {
        if (!this.keysWrap) return;

        const info = context.layoutInfo ?? {};
        const dic = info.keysAndDesignations ?? {};
        const maxSelections = info.maxAllowedSelections ?? 1;

        this.keysWrap.innerHTML = "";
        this.selectedKeys.clear();

        for (const [key, designation] of Object.entries(dic)) {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "key-btn";
            btn.textContent = info.showDesignations === false ? key : (designation || key);

            btn.addEventListener("click", () => {
                if (this.locked) return;

                if (this.selectedKeys.has(key)) {
                    this.selectedKeys.delete(key);
                    btn.classList.remove("selected");
                } else {
                    if (this.selectedKeys.size >= maxSelections) {
                        return;
                    }

                    this.selectedKeys.add(key);
                    btn.classList.add("selected");
                }

                this.updateCommitState();
            });

            this.keysWrap.appendChild(btn);
        }

        this.updateCommitState();
        this.setLocked(context.currentLayoutLocked || context.allLocked);
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
        this.commitBtn.disabled = this.locked || this.selectedKeys.size === 0;
    }

    dispose() {
    }
}