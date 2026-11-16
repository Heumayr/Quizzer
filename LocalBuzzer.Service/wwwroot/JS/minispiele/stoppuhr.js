// Minispiel Stoppuhr (M35 C1): der Spieler startet selbst, die Uhr ist visibleMs lang zu sehen und
// verschwindet dann; gestoppt wird mit einem Tipp bei der Zielzeit. Gemessen wird HIER, ab dem
// eigenen Start. Wer nicht stoppt, gibt ueber der Grenze des Servers selbst auf.

export const VERDECKT_TEXT = "?,?? s";

export function sekunden(ms, stellen = 2) {
    return `${(ms / 1000).toFixed(stellen).replace(".", ",")} s`;
}

export class StoppuhrModul {
    constructor({ timer, now } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.now = now ?? (() => performance.now());
        this.flaeche = null;
        this.symbol = null;
        this.anzeige = null;
        this.text = null;
        this.zustand = "aus";
        this.senden = null;
        this.locked = true;
        this.seit = 0;
        this.ziel = 10;
        this.sichtbarMs = 3000;
        this.grenze = 20000;
        this.uhren = [];
        this.onPointerDown = this.onPointerDown.bind(this);
    }

    mount(feld) {
        const flaeche = document.createElement("button");
        flaeche.type = "button";
        flaeche.className = "stoppuhr-taste";

        const symbol = document.createElement("span");
        symbol.className = "stoppuhr-symbol";
        symbol.setAttribute("aria-hidden", "true");

        const anzeige = document.createElement("span");
        anzeige.className = "stoppuhr-anzeige";

        const text = document.createElement("span");
        text.className = "stoppuhr-text";
        text.setAttribute("aria-live", "polite");

        flaeche.append(symbol, anzeige, text);
        feld.appendChild(flaeche);

        flaeche.addEventListener("pointerdown", this.onPointerDown);

        Object.assign(this, { flaeche, symbol, anzeige, text });
        this.zeige("neutral", "", "", "Gleich geht es los");
    }

    // Die Runde ist offen - gestartet wird erst mit dem eigenen Tipp.
    starte(artInfo, senden) {
        this.stoppeUhren();
        this.senden = senden;
        this.ziel = artInfo?.targetSeconds ?? 10;
        this.sichtbarMs = artInfo?.visibleMs ?? 3000;
        this.grenze = artInfo?.maxMs ?? this.ziel * 2000;
        this.zustand = "bereit";
        this.zeige("stoppuhr-bereit", "▶", sekunden(0), `Tippe zum Starten – und stoppe nach genau ${this.ziel} Sekunden.`);
    }

    onPointerDown(e) {
        if (e?.button > 0) return;
        this.tippe();
    }

    tippe() {
        if (this.locked || !this.senden) return;

        if (this.zustand === "bereit") {
            this.starteUhr();
        } else if (this.zustand === "sichtbar" || this.zustand === "verdeckt") {
            const ms = this.gemessen();
            this.abgeben(ms, `Gestoppt bei ${sekunden(ms)}`);
        }
    }

    starteUhr() {
        this.seit = this.now();
        this.zustand = "sichtbar";
        this.zeigeLauf();
        this.uhr(() => this.verdecke(), this.sichtbarMs);

        // Etwas ueber der Grenze des Servers: dann zaehlt die Zeit nicht, und die Runde haengt nicht.
        this.uhr(() => {
            if (this.zustand === "sichtbar" || this.zustand === "verdeckt") {
                this.abgeben(this.gemessen(), "Zu spät – keine Wertung");
            }
        }, this.grenze + 200);
    }

    // Solange sie zu sehen ist, laeuft die Anzeige mit.
    zeigeLauf() {
        if (this.zustand !== "sichtbar") return;

        this.zeige("stoppuhr-sichtbar", "⏱", sekunden(this.gemessen(), 1), `Ziel: ${this.ziel} Sekunden – tippe zum Stoppen.`);
        this.uhr(() => this.zeigeLauf(), 100);
    }

    verdecke() {
        if (this.zustand !== "sichtbar") return;

        this.zustand = "verdeckt";
        this.zeige("stoppuhr-verdeckt", "🙈", VERDECKT_TEXT, `Die Uhr ist weg – tippe bei ${this.ziel} Sekunden!`);
    }

    gemessen() {
        return Math.max(0, Math.round(this.now() - this.seit));
    }

    async abgeben(ms, satz) {
        // Erst sperren, dann senden - ein zweiter Tipp darf nichts mehr schicken.
        this.zustand = "abgegeben";
        this.stoppeUhren();
        this.zeige("stoppuhr-ergebnis", "✔", sekunden(ms), satz);

        try {
            await this.senden?.({ milliseconds: ms });
        } catch {
            // Gemeldet hat es index.js; die Zeit laesst sich nicht wiederholen.
        }
    }

    // Eigene Abgabe der Runde, etwa nach dem Wiederverbinden oder nach Rundenende.
    zeigeErgebnis(artInfo) {
        this.stoppeUhren();
        this.zustand = "abgegeben";
        this.zeige("stoppuhr-ergebnis", "✔", this.eigeneZeit(artInfo), this.eigenerSatz(artInfo));
    }

    zeigeEnde(artInfo) {
        this.stoppeUhren();
        this.zustand = "aus";

        const namen = artInfo?.winnerNames ?? [];
        const abstand = artInfo?.winnerDistanceMs != null ? ` (${this.daneben(artInfo.winnerDistanceMs)})` : "";

        if (artInfo?.isWinner) {
            this.zeige("stoppuhr-ergebnis", "🏆", this.eigeneZeit(artInfo), `Gewonnen! ${this.eigenerSatz(artInfo)}`);
        } else if (namen.length > 0) {
            this.zeige("stoppuhr-ergebnis", "■", this.eigeneZeit(artInfo), `${this.eigenerSatz(artInfo)} Gewonnen: ${namen.join(", ")}${abstand}`);
        } else {
            this.zeige("stoppuhr-ergebnis", "■", this.eigeneZeit(artInfo), `${this.eigenerSatz(artInfo)} Keine gültige Zeit.`);
        }
    }

    eigeneZeit(artInfo) {
        return artInfo?.ownMilliseconds != null ? sekunden(artInfo.ownMilliseconds) : "–";
    }

    eigenerSatz(artInfo) {
        if (artInfo?.ownMilliseconds == null) return "Keine Abgabe.";
        if (artInfo.ownDistanceMs == null) return "Zu spät – keine Wertung.";
        return `Deine Zeit: ${sekunden(artInfo.ownMilliseconds)}, ${this.daneben(artInfo.ownDistanceMs)}.`;
    }

    daneben(ms) {
        return ms === 0 ? "genau getroffen" : `${sekunden(ms)} daneben`;
    }

    leeren() {
        this.stoppeUhren();
        this.zustand = "aus";
        this.zeige("neutral", "", "", "Gleich geht es los");
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    zeige(klasse, symbol, anzeige, text) {
        if (!this.flaeche) return;

        // Ausgeschrieben statt zusammengesetzt - PhonePageContractUnitTests findet so jede Klasse.
        switch (klasse) {
            case "stoppuhr-bereit": this.flaeche.className = "stoppuhr-taste stoppuhr-bereit"; break;
            case "stoppuhr-sichtbar": this.flaeche.className = "stoppuhr-taste stoppuhr-sichtbar"; break;
            case "stoppuhr-verdeckt": this.flaeche.className = "stoppuhr-taste stoppuhr-verdeckt"; break;
            case "stoppuhr-ergebnis": this.flaeche.className = "stoppuhr-taste stoppuhr-ergebnis"; break;
            default: this.flaeche.className = "stoppuhr-taste";
        }

        this.flaeche.disabled = !["bereit", "sichtbar", "verdeckt"].includes(this.zustand);
        this.symbol.textContent = symbol;
        this.anzeige.textContent = anzeige;
        this.text.textContent = text;
    }

    uhr(fn, ms) {
        this.uhren.push(this.timer.setTimeout(fn, ms));
    }

    stoppeUhren() {
        this.uhren.forEach(h => this.timer.clearTimeout(h));
        this.uhren = [];
    }

    dispose() {
        this.stoppeUhren();
        this.flaeche?.removeEventListener("pointerdown", this.onPointerDown);
    }
}
