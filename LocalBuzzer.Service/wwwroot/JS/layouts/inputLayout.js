export class InputLayout {
    constructor(actions) {
        this.actions = actions;
        this.root = null;
        this.input = null;
        this.commitBtn = null;
        this.locked = true;
    }

    render(host, context) {
        const info = context.layoutInfo ?? {};
        const inputType = info.inputType ?? "text";
        const placeholder = info.placeholder ?? "Eingabe";

        const root = document.createElement("div");
        root.className = "layout layout-input";
        root.innerHTML = `
            <input class="input-field" type="${inputType}" placeholder="${placeholder}">
            <button class="commit-btn">Eingabe bestätigen</button>
        `;

        host.appendChild(root);

        this.root = root;
        this.input = root.querySelector(".input-field");
        this.commitBtn = root.querySelector(".commit-btn");

        this.commitBtn.addEventListener("click", async () => {
            if (this.locked) return;

            await this.actions.submitInput({
                playerId: context.playerId,
                value: this.input.value
            });
        });

        this.setLocked(context.currentLayoutLocked || context.allLocked);
    }

    update(context) {
        this.setLocked(context.currentLayoutLocked || context.allLocked);
    }

    setLocked(locked) {
        this.locked = !!locked;
        if (this.input) this.input.disabled = this.locked;
        if (this.commitBtn) this.commitBtn.disabled = this.locked;
    }

    dispose() {
    }
}