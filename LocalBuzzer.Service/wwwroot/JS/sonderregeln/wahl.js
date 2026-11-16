// Sonderregeln mit Spielerwahl (M35 C3b) am Telefon: Wette, Experte, Allianz. Der Server schickt die Frage
// und die Tasten; gesendet wird genau einmal { chosen: id } - der Server prueft die Wahl trotzdem.

import { Spielerwahl } from "../minispiele/spielerwahl.js";

// Der Name der eigenen Wahl aus den Tasten - nie nur Farbe, immer ein Satz.
export function wahlText(artInfo) {
    const id = artInfo?.ownChoice;
    if (id == null) return "Keine Wahl abgegeben.";

    const name = (artInfo?.options ?? []).find(o => o.id === id)?.name ?? "?";
    return `Deine Wahl: ${name}`;
}

export class WahlModul {
    constructor() {
        this.locked = true;
        this.wahl = new Spielerwahl();
        this.inhalt = null;
        this.text = null;
    }

    mount(feld) {
        const inhalt = document.createElement("div");
        inhalt.className = "wahl-inhalt";

        const text = document.createElement("p");
        text.className = "wahl-text";
        text.setAttribute("aria-live", "polite");

        feld.append(inhalt, text);
        Object.assign(this, { inhalt, text });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.inhalt.innerHTML = "";
        this.text.textContent = "";
        this.wahl.zeige(this.inhalt, artInfo?.prompt || "Wähle einen Mitspieler:", artInfo?.options ?? [], senden);
        this.wahl.setLocked(this.locked);
    }

    zeigeErgebnis(artInfo) {
        this.inhalt.innerHTML = "";
        this.text.textContent = wahlText(artInfo);
    }

    zeigeEnde(artInfo) {
        this.zeigeErgebnis(artInfo);
    }

    leeren() {
        if (this.inhalt) this.inhalt.innerHTML = "";
        if (this.text) this.text.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
        this.wahl.setLocked(this.locked);
    }

    dispose() {
        this.leeren();
    }
}
