export class BuzzerLayout {
    constructor(actions) {
        this.actions = actions;
        this.root = null;
        this.btn = null;
        this.onBuzzClick = this.onBuzzClick.bind(this);
    }

    render(host) {
        const root = document.createElement("div");
        root.className = "layout layout-buzzer";
        root.innerHTML = `
            <button class="buzz-btn" id="buzz_btn">BUZZ</button>
        `;

        host.appendChild(root);

        this.root = root;
        this.btn = root.querySelector("#buzz_btn");
        this.btn.addEventListener("click", this.onBuzzClick);
    }

    update() {
    }

    setLocked(locked) {
        if (this.btn) {
            this.btn.disabled = !!locked;
        }
    }

    async onBuzzClick() {
        await this.actions.buzz();
    }

    dispose() {
        if (this.btn) {
            this.btn.removeEventListener("click", this.onBuzzClick);
        }
    }
}