// Sonderregel Risiko (M35 C3) am Telefon: doppelt spielen, ja oder nein - genau einmal.
// Nie allein ueber Farbe: der Satz sagt die Wahl.

export function wahlText(doppelt) {
    if (doppelt == null) return "Keine Wahl – du spielst wie immer.";
    return doppelt ? "Du spielst doppelt – Plus und Minus zählen zweimal." : "Du spielst einfach – wie immer.";
}

export class RisikoModul {
    constructor() {
        this.locked = true;
        this.gesendet = false;
        this.tasten = [];
        this.kopf = null;
        this.inhalt = null;
        this.text = null;
    }

    mount(feld) {
        const kopf = document.createElement("p");
        kopf.className = "risiko-kopf";

        const inhalt = document.createElement("div");
        inhalt.className = "risiko-inhalt";

        const text = document.createElement("p");
        text.className = "risiko-text";
        text.setAttribute("aria-live", "polite");

        feld.append(kopf, inhalt, text);
        Object.assign(this, { kopf, inhalt, text });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.gesendet = false;
        this.inhalt.innerHTML = "";
        this.kopf.textContent = "Doppelt spielen?";
        this.tasten = [this.taste("⚠ Ja, doppelt", true, senden), this.taste("Nein, einfach", false, senden)];
        this.text.textContent = "Wer ja wählt, bekommt Plus und Minus doppelt.";
    }

    taste(beschriftung, doppelt, senden) {
        const taste = document.createElement("button");
        taste.type = "button";
        taste.className = "risiko-taste";
        taste.textContent = beschriftung;
        taste.addEventListener("click", () => this.entscheide(taste, doppelt, senden));
        this.inhalt.appendChild(taste);
        return taste;
    }

    async entscheide(taste, doppelt, senden) {
        if (this.locked || this.gesendet) return;

        this.gesendet = true;
        this.tasten.forEach(t => { t.disabled = true; });
        taste.className = "risiko-taste risiko-aktiv";
        this.text.textContent = wahlText(doppelt) + " Warte auf die anderen.";

        try {
            await senden({ doubled: doppelt });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    zeigeErgebnis(artInfo) {
        this.inhalt.innerHTML = "";
        this.tasten = [];
        this.kopf.textContent = "Doppelt spielen?";
        this.text.textContent = wahlText(artInfo?.ownDoubled);
    }

    zeigeEnde(artInfo) {
        this.zeigeErgebnis(artInfo);
    }

    leeren() {
        this.tasten = [];
        if (this.inhalt) this.inhalt.innerHTML = "";
        if (this.kopf) this.kopf.textContent = "";
        if (this.text) this.text.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    dispose() {
        this.leeren();
    }
}
