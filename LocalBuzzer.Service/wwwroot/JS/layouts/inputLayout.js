import { toast } from "../helpers.js";

const COMMIT_LABEL = "Eingabe bestätigen";
const RESUBMIT_LABEL = "Erneut abgeben";

export class InputLayout {
    constructor(actions) {
        this.actions = actions;
        this.root = null;
        this.label = null;
        this.input = null;
        this.submittedLine = null;
        this.commitBtn = null;
        this.locked = true;
        this.lastContext = null;
        this.lastSignature = "";
        this.submittedValue = null;
        this.handleCommitClick = this.handleCommitClick.bind(this);
    }

    render(host, context) {
        const root = document.createElement("div");
        root.className = "layout layout-input";

        const label = document.createElement("label");
        label.className = "input-label";
        label.htmlFor = "input_field";

        const input = document.createElement("input");
        input.className = "input-field";
        input.id = "input_field";
        input.autocomplete = "off";

        const submittedLine = document.createElement("div");
        submittedLine.className = "input-submitted";
        submittedLine.setAttribute("aria-live", "polite");
        submittedLine.hidden = true;

        const commitBtn = document.createElement("button");
        commitBtn.type = "button";
        commitBtn.className = "commit-btn";

        root.append(label, input, submittedLine, commitBtn);
        host.appendChild(root);

        this.root = root;
        this.label = label;
        this.input = input;
        this.submittedLine = submittedLine;
        this.commitBtn = commitBtn;

        commitBtn.addEventListener("click", this.handleCommitClick);

        this.lastContext = context;
        this.configure(context);
        this.setLocked(context.currentLayoutLocked || context.allLocked);
    }

    createSignature(context) {
        const info = context?.layoutInfo ?? {};
        return JSON.stringify({
            inputType: info.inputType ?? "text",
            placeholder: info.placeholder ?? "Eingabe",
            questionId: info.questionId ?? ""
        });
    }

    // Feld, Beschriftung und Abgabezeile auf die aktuelle Frage stellen (neue Frage = leeres Feld).
    configure(context) {
        const info = context.layoutInfo ?? {};
        const inputType = info.inputType ?? "text";
        const placeholder = info.placeholder ?? "Eingabe";

        this.lastSignature = this.createSignature(context);

        this.label.textContent = placeholder;
        this.input.setAttribute("placeholder", placeholder);

        // Zahl als Textfeld mit Dezimaltastatur: type=number liefert bei "12,5" auf iOS einen leeren Wert.
        if (inputType === "date") {
            this.input.type = "date";
            this.input.removeAttribute("inputmode");
        } else {
            this.input.type = "text";
            if (inputType === "number") {
                this.input.setAttribute("inputmode", "decimal");
            } else {
                this.input.removeAttribute("inputmode");
            }
        }

        this.input.value = "";
        this.submittedValue = null;
        this.commitBtn.textContent = COMMIT_LABEL;
        this.refreshSubmittedLine();
    }

    update(context) {
        this.lastContext = context;

        if (this.createSignature(context) !== this.lastSignature) {
            this.configure(context);
        }

        this.setLocked(context.currentLayoutLocked || context.allLocked);
    }

    setLocked(locked) {
        this.locked = !!locked;
        if (this.input) this.input.disabled = this.locked;
        if (this.commitBtn) this.commitBtn.disabled = this.locked;
        this.refreshSubmittedLine();
    }

    refreshSubmittedLine() {
        if (!this.submittedLine) return;

        if (this.submittedValue === null) {
            this.submittedLine.hidden = true;
            this.submittedLine.textContent = "";
            return;
        }

        this.submittedLine.hidden = false;
        this.submittedLine.textContent = this.locked
            ? `Abgegeben: ${this.submittedValue}`
            : `Abgegeben: ${this.submittedValue} – bis zum Rundenende änderbar`;
    }

    async handleCommitClick() {
        if (this.locked || !this.lastContext) return;

        const value = this.input.value.trim();
        if (!value) {
            toast("warning", "Bitte zuerst einen Wert eingeben");
            this.input.focus();
            return;
        }

        this.commitBtn.disabled = true;

        try {
            await this.actions.submitInput({
                playerId: this.lastContext.playerId,
                value
            });
        } catch {
            // Fehler hat index.js schon gemeldet; hier nur den Knopf wieder freigeben.
            this.commitBtn.disabled = this.locked;
            return;
        }

        this.submittedValue = value;
        this.commitBtn.textContent = RESUBMIT_LABEL;
        this.commitBtn.disabled = this.locked;
        this.refreshSubmittedLine();
        toast("info", `Abgegeben: ${value}`);
    }

    dispose() {
        if (this.commitBtn) {
            this.commitBtn.removeEventListener("click", this.handleCommitClick);
        }
    }
}
