// Minispiel Tippsprint (M35 C1): nach dem Countdown so oft tippen wie moeglich, bis die Zeit um ist.
// Gezaehlt wird HIER; etwa alle 0,5 s geht der Zwischenstand an den Server (Rennen am Beamer), am
// Schluss der Endstand. Ueber der Grenze des Servers zaehlt das Telefon gar nicht erst - sonst
// verwuerfe er den ganzen Stand.

export function tippText(n) {
    return n === 1 ? "1 Tipp" : `${n} Tipps`;
}

export class TippsprintModul {
    constructor({ timer, now } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.now = now ?? (() => performance.now());
        this.flaeche = null;
        this.symbol = null;
        this.zahl = null;
        this.text = null;
        this.zustand = "aus";
        this.senden = null;
        this.locked = true;
        this.tipps = 0;
        this.gemeldet = 0;
        this.vorher = 0;
        this.seit = 0;
        this.dauerMs = 0;
        this.grenze = 20;
        this.takt = 500;
        this.uhren = [];
        this.onPointerDown = this.onPointerDown.bind(this);
    }

    mount(feld) {
        const flaeche = document.createElement("button");
        flaeche.type = "button";
        flaeche.className = "tippsprint-taste";

        const symbol = document.createElement("span");
        symbol.className = "tippsprint-symbol";
        symbol.setAttribute("aria-hidden", "true");

        const zahl = document.createElement("span");
        zahl.className = "tippsprint-zahl";

        const text = document.createElement("span");
        text.className = "tippsprint-text";
        text.setAttribute("aria-live", "polite");

        flaeche.append(symbol, zahl, text);
        feld.appendChild(flaeche);

        // pointerdown wie beim Buzzer: jeder Druck zaehlt, nicht erst das Loslassen.
        flaeche.addEventListener("pointerdown", this.onPointerDown);

        Object.assign(this, { flaeche, symbol, zahl, text });
        this.zeige("neutral", "", "", "Gleich geht es los");
    }

    // Der Sprint beginnt. Nach dem Wiederverbinden geht es beim letzten angenommenen Stand weiter.
    starte(artInfo, senden) {
        this.stoppeUhren();
        this.senden = senden;
        this.dauerMs = (artInfo?.durationSeconds ?? 10) * 1000;
        this.grenze = artInfo?.maxTapsPerSecond ?? 20;
        this.takt = artInfo?.reportIntervalMs ?? 500;
        this.tipps = artInfo?.ownTaps ?? 0;
        this.vorher = artInfo?.ownElapsedMs ?? 0;
        this.gemeldet = this.tipps;
        this.seit = this.now();
        this.zustand = "aktiv";

        this.zeigeStand();
        this.uhr(() => this.melde(), this.takt);
        this.uhr(() => this.ende(), Math.max(0, this.dauerMs - this.vorher));
    }

    vergangen() {
        return Math.min(this.dauerMs, this.vorher + Math.max(0, Math.round(this.now() - this.seit)));
    }

    onPointerDown(e) {
        if (e?.button > 0) return;
        this.tippe();
    }

    tippe() {
        if (this.locked || this.zustand !== "aktiv") return;
        if ((this.tipps + 1) * 1000 > this.grenze * this.vergangen()) return;

        this.tipps++;
        this.zeigeStand();
    }

    // Zwischenstand nur, wenn sich etwas getan hat - das Rennen braucht keine Wiederholungen.
    melde() {
        if (this.zustand !== "aktiv") return;

        if (this.tipps !== this.gemeldet) {
            this.gemeldet = this.tipps;
            this.sende({ taps: this.tipps, elapsedMs: this.vergangen(), isFinal: false });
        }

        this.zeigeStand();
        this.uhr(() => this.melde(), this.takt);
    }

    ende() {
        if (this.zustand !== "aktiv") return;

        // Erst sperren, dann senden - ein spaeter Tipp darf nichts mehr zaehlen.
        this.zustand = "abgegeben";
        this.stoppeUhren();
        this.zeige("tippsprint-ergebnis", "✔", String(this.tipps), `Zeit um! ${tippText(this.tipps)} – warte auf die anderen.`);
        this.sende({ taps: this.tipps, elapsedMs: this.dauerMs, isFinal: true });
    }

    async sende(payload) {
        try {
            await this.senden?.(payload);
        } catch {
            // Gemeldet hat es index.js; der naechste Stand geht trotzdem raus.
        }
    }

    zeigeStand() {
        const rest = Math.max(0, Math.ceil((this.dauerMs - this.vergangen()) / 1000));
        this.zeige("tippsprint-aktiv", "👆", String(this.tipps), `Tippen, tippen, tippen! Noch ${rest} s`);
    }

    // Eigener Stand der Runde, etwa nach dem Wiederverbinden oder nach Rundenende.
    zeigeErgebnis(artInfo) {
        this.stoppeUhren();
        this.zustand = "abgegeben";

        const eigene = artInfo?.ownTaps;
        this.zeige("tippsprint-ergebnis", "✔", eigene != null ? String(eigene) : "–",
            eigene != null ? `Deine Tipps: ${tippText(eigene)}` : "Keine Abgabe");
    }

    zeigeEnde(artInfo) {
        this.stoppeUhren();
        this.zustand = "aus";

        const eigene = artInfo?.ownTaps != null ? `Du: ${tippText(artInfo.ownTaps)}. ` : "";
        const namen = artInfo?.winnerNames ?? [];
        const bester = artInfo?.winnerTaps != null ? ` (${tippText(artInfo.winnerTaps)})` : "";

        if (artInfo?.isWinner) {
            this.zeige("tippsprint-ergebnis", "🏆", String(artInfo.ownTaps ?? ""), `Gewonnen! ${eigene}`.trim());
        } else if (namen.length > 0) {
            this.zeige("tippsprint-ergebnis", "■", "", `${eigene}Gewonnen: ${namen.join(", ")}${bester}`);
        } else {
            this.zeige("tippsprint-ergebnis", "■", "", `${eigene}Keine Tipps gewertet.`.trim());
        }
    }

    leeren() {
        this.stoppeUhren();
        this.zustand = "aus";
        this.zeige("neutral", "", "", "Gleich geht es los");
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    zeige(klasse, symbol, zahl, text) {
        if (!this.flaeche) return;

        // Ausgeschrieben statt zusammengesetzt - PhonePageContractUnitTests findet so jede Klasse.
        switch (klasse) {
            case "tippsprint-aktiv": this.flaeche.className = "tippsprint-taste tippsprint-aktiv"; break;
            case "tippsprint-ergebnis": this.flaeche.className = "tippsprint-taste tippsprint-ergebnis"; break;
            default: this.flaeche.className = "tippsprint-taste";
        }

        this.flaeche.disabled = this.zustand !== "aktiv";
        this.symbol.textContent = symbol;
        this.zahl.textContent = zahl;
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
