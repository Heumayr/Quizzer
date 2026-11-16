import { Spielerwahl } from "./spielerwahl.js";

// Minispiel Gluecksrad (M35 C4): der Spielleiter dreht, das Rad steht am Beamer. Das Telefon kommt nur
// zum Zug, wenn das Feld ein Ziel braucht (Klauen, Verschenken) - dann waehlt der Dreher einen Mitspieler.
// Das Feld kennt ein Telefon vor der Aufloesung nur, wenn es gerade waehlt.

export class GluecksradModul {
    constructor() {
        this.wahl = new Spielerwahl();
        this.kopf = null;
        this.inhalt = null;
        this.text = null;
    }

    mount(feld) {
        const kopf = document.createElement("p");
        kopf.className = "gluecksrad-kopf";

        const inhalt = document.createElement("div");
        inhalt.className = "gluecksrad-inhalt";

        const text = document.createElement("p");
        text.className = "gluecksrad-text";
        text.setAttribute("aria-live", "polite");

        feld.append(kopf, inhalt, text);
        Object.assign(this, { kopf, inhalt, text });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.inhalt.innerHTML = "";
        this.kopf.textContent = artInfo?.segmentName ? `Das Rad zeigt: ${artInfo.segmentName}` : "Glücksrad";

        if (artInfo?.choosesTarget) {
            this.text.textContent = "";
            this.wahl.zeige(this.inhalt, artInfo.choicePrompt || "Wähle einen Mitspieler:", artInfo.options, senden);
            return;
        }

        this.text.textContent = "Schau auf den Beamer!";
    }

    zeigeErgebnis(artInfo) {
        this.inhalt.innerHTML = "";
        this.kopf.textContent = artInfo?.segmentName ? `Das Rad zeigt: ${artInfo.segmentName}` : "Glücksrad";
        this.text.textContent = "Gewählt – gleich steht es am Beamer.";
    }

    zeigeEnde(artInfo) {
        this.inhalt.innerHTML = "";
        this.kopf.textContent = artInfo?.segmentName ? `Das Rad zeigte: ${artInfo.segmentName}` : "Glücksrad";
        this.text.textContent = artInfo?.resultText || "Schaut auf den Beamer.";
    }

    leeren() {
        if (this.inhalt) this.inhalt.innerHTML = "";
        if (this.kopf) this.kopf.textContent = "Glücksrad";
        if (this.text) this.text.textContent = "Gleich dreht sich das Rad";
    }

    setLocked(locked) {
        this.wahl.setLocked(locked);
    }

    dispose() {
        this.leeren();
    }
}
