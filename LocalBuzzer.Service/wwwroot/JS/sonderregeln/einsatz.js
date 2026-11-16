// Sonderregel Einsatz (M35 C3) am Telefon: eine ganze Zahl von 0 bis zur Obergrenze, genau einmal.
// Die Obergrenze prueft der Server noch einmal - das Telefon kommt aus dem WLAN.

export function pruefeEinsatz(text, max) {
    const roh = String(text ?? "").trim();
    if (!/^\d+$/.test(roh)) return { ok: false, grund: "Bitte eine ganze Zahl ab 0 eingeben." };

    const wert = Number(roh);
    if (wert > max) return { ok: false, grund: `Höchstens ${max} Punkte.` };

    return { ok: true, wert };
}

export class EinsatzModul {
    constructor() {
        this.locked = true;
        this.gesendet = false;
        this.max = 0;
        this.senden = null;
        this.kopf = null;
        this.eingabe = null;
        this.taste = null;
        this.text = null;
    }

    mount(feld) {
        const kopf = document.createElement("p");
        kopf.className = "einsatz-kopf";

        const eingabe = document.createElement("input");
        eingabe.className = "einsatz-eingabe";
        eingabe.setAttribute("inputmode", "numeric");
        eingabe.setAttribute("aria-label", this.eingabeName());

        const taste = document.createElement("button");
        taste.type = "button";
        taste.className = "einsatz-taste";
        taste.textContent = this.tastenText();
        taste.addEventListener("click", () => this.setze());

        const text = document.createElement("p");
        text.className = "einsatz-text";
        text.setAttribute("aria-live", "polite");

        feld.append(kopf, eingabe, taste, text);
        Object.assign(this, { kopf, eingabe, taste, text });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.gesendet = false;
        this.senden = senden;
        this.max = artInfo?.max ?? 0;
        this.kopf.textContent = this.kopfText(this.max);
        this.eingabe.value = "";
        this.eingabe.disabled = false;
        this.taste.disabled = false;
        this.text.textContent = "";
    }

    async setze() {
        if (this.locked || this.gesendet || !this.senden) return;

        const geprueft = pruefeEinsatz(this.eingabe.value, this.max);
        if (!geprueft.ok) {
            this.text.textContent = geprueft.grund;
            return;
        }

        // Erst sperren, dann senden - ein zweiter Druck geht nicht mehr raus.
        this.gesendet = true;
        this.eingabe.disabled = true;
        this.taste.disabled = true;
        this.text.textContent = `${this.ergebnisText(geprueft.wert)} – warte auf die anderen.`;

        try {
            await this.senden({ amount: geprueft.wert });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    zeigeErgebnis(artInfo) {
        this.eingabe.disabled = true;
        this.taste.disabled = true;
        this.text.textContent = this.ergebnisText(artInfo?.ownAmount);
    }

    // Die Texte - die Auktion (M35 C3b) erbt das Modul und ersetzt nur sie.
    tastenText() { return "Setzen"; }

    eingabeName() { return "Einsatz in Punkten"; }

    kopfText(max) { return `Setze 0 bis ${max} Punkte.`; }

    ergebnisText(wert) {
        return wert != null ? `Dein Einsatz: ${wert}` : "Kein Einsatz – du spielst um die Feldpunkte.";
    }

    zeigeEnde(artInfo) {
        this.zeigeErgebnis(artInfo);
    }

    leeren() {
        if (this.kopf) this.kopf.textContent = "";
        if (this.eingabe) { this.eingabe.value = ""; this.eingabe.disabled = true; }
        if (this.taste) this.taste.disabled = true;
        if (this.text) this.text.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    dispose() {
        this.leeren();
    }
}
