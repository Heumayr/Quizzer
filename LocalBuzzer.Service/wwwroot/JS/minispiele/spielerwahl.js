// Spielerwahl (M35 C2): je Mitspieler eine Taste mit seinem Namen. Die Id darf ans Telefon (E4) -
// der Server prueft trotzdem, dass sie zu einem Mitspieler gehoert und nicht zum Waehlenden.
// Duell und Teilen oder Nehmen bauen sie in ihr Feld ein.

export class Spielerwahl {
    constructor() {
        this.locked = true;
        this.gesendet = false;
        this.tasten = [];
        this.frage = null;
    }

    // Baut Frage und Tasten in host; ein Tipp sendet { chosen: id } - genau einmal.
    zeige(host, frage, optionen, senden) {
        this.gesendet = false;
        this.tasten = [];

        const text = document.createElement("p");
        text.className = "spielerwahl-frage";
        text.textContent = (optionen ?? []).length > 0 ? frage : "Niemand zum Wählen da – der Spielleiter hilft.";
        host.appendChild(text);
        this.frage = text;

        for (const spieler of optionen ?? []) {
            const taste = document.createElement("button");
            taste.type = "button";
            taste.className = "spielerwahl-taste";
            taste.textContent = spieler.name;
            taste.addEventListener("click", () => this.waehle(spieler.id, taste, senden));
            host.appendChild(taste);
            this.tasten.push(taste);
        }
    }

    async waehle(id, taste, senden) {
        if (this.locked || this.gesendet) return;

        // Erst sperren, dann senden - ein zweiter Tipp darf nichts mehr schicken. Nie nur Farbe: der Text sagt die Wahl.
        this.gesendet = true;
        this.tasten.forEach(t => { t.disabled = true; });
        taste.className = "spielerwahl-taste spielerwahl-aktiv";
        this.frage.textContent = `Gewählt: ${taste.textContent} – gleich geht es los.`;

        try {
            await senden({ chosen: id });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    setLocked(locked) {
        this.locked = !!locked;
    }
}
