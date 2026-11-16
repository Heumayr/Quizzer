// Minispiel Raeuber und Waechter (M35 C2): jeder waehlt geheim - einen Mitspieler bestehlen oder das
// Schild heben. Das Telefon kennt nur die eigene Wahl; was daraus wurde, schickt der Server erst
// mit der Aufloesung. Die Id des Opfers darf ans Telefon (E4), geprueft wird sie am Server.

export class RaeuberModul {
    constructor() {
        this.locked = true;
        this.gesendet = false;
        this.tasten = [];
        this.text = null;
        this.inhalt = null;
    }

    mount(feld) {
        const text = document.createElement("p");
        text.className = "raeuber-text";
        text.setAttribute("aria-live", "polite");

        const inhalt = document.createElement("div");
        inhalt.className = "raeuber-inhalt";

        feld.append(text, inhalt);
        Object.assign(this, { text, inhalt });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.inhalt.innerHTML = "";
        this.gesendet = false;
        this.text.textContent = "Wähle geheim: wen bestiehlst du – oder hebst du das Schild?";

        const schild = this.taste("🛡 Schild heben", "das Schild", () => senden({ shield: true, victim: null }));
        schild.className = "raeuber-schild";

        const opfer = (artInfo?.options ?? []).map(spieler => this.taste(`💰 ${spieler.name} bestehlen`,
            spieler.name, () => senden({ shield: false, victim: spieler.id })));

        this.tasten = [schild, ...opfer];
    }

    taste(beschriftung, wahl, senden) {
        const taste = document.createElement("button");
        taste.type = "button";
        taste.className = "raeuber-opfer";
        taste.textContent = beschriftung;
        taste.addEventListener("click", () => this.waehle(taste, wahl, senden));
        this.inhalt.appendChild(taste);
        return taste;
    }

    async waehle(taste, wahl, senden) {
        if (this.locked || this.gesendet) return;

        // Erst sperren, dann senden. Nie nur Farbe: der Satz sagt, was gewaehlt ist.
        this.gesendet = true;
        this.tasten.forEach(t => { t.disabled = true; });
        taste.classList.add("raeuber-aktiv");
        this.text.textContent = `Gewählt: ${wahl} – warte auf die anderen.`;

        try {
            await senden();
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    // Abgegeben oder Runde vorbei: nur die eigene Wahl.
    zeigeErgebnis(artInfo) {
        this.inhalt.innerHTML = "";

        if (artInfo?.ownShield) {
            this.text.textContent = "Du hast das Schild gehoben – warte auf die Auflösung.";
        } else if (artInfo?.ownVictimName) {
            this.text.textContent = `Du willst ${artInfo.ownVictimName} bestehlen – warte auf die Auflösung.`;
        } else {
            this.text.textContent = "Keine Wahl abgegeben.";
        }
    }

    // Die Aufloesung: je Satz eine Zeile, was aus der eigenen Wahl wurde.
    zeigeEnde(artInfo) {
        this.inhalt.innerHTML = "";
        this.text.textContent = "Aufgelöst:";

        for (const satz of artInfo?.ownResults ?? []) {
            const zeile = document.createElement("p");
            zeile.className = "raeuber-ergebnis";
            zeile.textContent = satz;
            this.inhalt.appendChild(zeile);
        }
    }

    leeren() {
        this.tasten = [];
        if (this.inhalt) this.inhalt.innerHTML = "";
        if (this.text) this.text.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    dispose() {
        this.leeren();
    }
}
