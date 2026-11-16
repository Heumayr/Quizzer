import { ReactionOutcomes } from "../constants.js";

// Minispiel Reaktion (M35 B3): rot, nach der Wartezeit vom Server gruen, tippen.
// Die Zeit misst DIESES Telefon ab seinem eigenen Umschalten - Ankunftszeiten am Server
// taugen wegen des WLANs nicht. Nie nur Farbe: Symbol und Text wechseln mit.

export const JETZT_TEXT = "JETZT!";
export const WARTEN_TEXT = "Warten … noch nicht tippen";

// Etwas ueber der Grenze des Servers (5000 ms): dann gibt das Telefon selbst auf, und die Runde
// haengt nicht an einem, der nicht tippt.
export const AUFGEBEN_NACH_MS = 5200;

export function beschreibe(outcome, ms) {
    switch (outcome) {
        case ReactionOutcomes.Valid: return `${ms} ms`;
        case ReactionOutcomes.FalseStart: return "Frühstart – Runde verloren";
        case ReactionOutcomes.TooSlow: return "zu langsam – keine Wertung";
        default: return "keine Abgabe";
    }
}

export class ReaktionModul {
    constructor({ timer, now } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.now = now ?? (() => performance.now());
        this.flaeche = null;
        this.symbol = null;
        this.text = null;
        this.zustand = "aus";
        this.senden = null;
        this.locked = true;
        this.gruenSeit = 0;
        this.uhren = [];
        this.onPointerDown = this.onPointerDown.bind(this);
    }

    mount(feld) {
        const flaeche = document.createElement("button");
        flaeche.type = "button";
        flaeche.className = "reaktion-taste";

        const symbol = document.createElement("span");
        symbol.className = "reaktion-symbol";
        symbol.setAttribute("aria-hidden", "true");

        const text = document.createElement("span");
        text.className = "reaktion-text";
        text.setAttribute("aria-live", "assertive");

        flaeche.append(symbol, text);
        feld.appendChild(flaeche);

        // pointerdown wie beim Buzzer: es zaehlt der Druck, nicht das Loslassen.
        flaeche.addEventListener("pointerdown", this.onPointerDown);

        this.flaeche = flaeche;
        this.symbol = symbol;
        this.text = text;
        this.zeige("neutral", "", "Gleich geht es los");
    }

    // Die Runde beginnt: rot, und nach artInfo.waitMs lokal gruen.
    starte(artInfo, senden) {
        this.stoppeUhren();
        this.senden = senden;
        this.zustand = "rot";
        this.zeige("reaktion-warten", "✋", WARTEN_TEXT);
        this.uhr(() => this.gruen(), artInfo?.waitMs ?? 3000);
    }

    gruen() {
        if (this.zustand !== "rot") return;

        this.zustand = "gruen";
        this.zeige("reaktion-jetzt", "⚡", JETZT_TEXT);
        this.gruenSeit = this.now();
        globalThis.navigator?.vibrate?.(200);

        this.uhr(() => {
            if (this.zustand !== "gruen") return;
            this.abgeben({ falseStart: false, milliseconds: this.gemessen() }, "zu langsam – keine Wertung");
        }, AUFGEBEN_NACH_MS);
    }

    onPointerDown(e) {
        if (e?.button > 0) return;
        this.tippe();
    }

    tippe() {
        if (this.locked || !this.senden) return;

        if (this.zustand === "rot") {
            this.abgeben({ falseStart: true, milliseconds: null }, "Zu früh! Diese Runde ist verloren.");
        } else if (this.zustand === "gruen") {
            const ms = this.gemessen();
            this.abgeben({ falseStart: false, milliseconds: ms }, `${ms} ms`);
        }
    }

    gemessen() {
        return Math.max(0, Math.round(this.now() - this.gruenSeit));
    }

    async abgeben(payload, satz) {
        // Erst sperren, dann senden - ein zweiter Tipp darf nichts mehr schicken.
        this.zustand = "abgegeben";
        this.stoppeUhren();
        this.zeige("reaktion-ergebnis", "✔", satz);

        try {
            await this.senden?.(payload);
        } catch {
            // Gemeldet hat es index.js; die Zeit laesst sich nicht wiederholen.
        }
    }

    // Eigene Abgabe der Runde, etwa nach dem Wiederverbinden oder nach Rundenende.
    zeigeErgebnis(artInfo) {
        this.stoppeUhren();
        this.zustand = "abgegeben";

        const eigene = beschreibe(artInfo?.ownOutcome, artInfo?.ownMilliseconds);
        const beste = artInfo?.bestMilliseconds != null ? ` · Bestzeit: ${artInfo.bestMilliseconds} ms` : "";

        this.zeige("reaktion-ergebnis", "✔", `Deine Zeit: ${eigene}${beste}`);
    }

    zeigeEnde(artInfo) {
        this.stoppeUhren();
        this.zustand = "aus";

        const namen = artInfo?.winnerNames ?? [];
        const zeit = artInfo?.winnerMilliseconds != null ? ` (${artInfo.winnerMilliseconds} ms)` : "";
        const beste = artInfo?.bestMilliseconds != null ? `Deine Bestzeit: ${artInfo.bestMilliseconds} ms. ` : "";

        if (artInfo?.isWinner) {
            this.zeige("reaktion-ergebnis", "🏆", `Gewonnen! ${beste}`.trim());
        } else if (namen.length > 0) {
            this.zeige("reaktion-ergebnis", "■", `${beste}Gewonnen: ${namen.join(", ")}${zeit}`);
        } else {
            this.zeige("reaktion-ergebnis", "■", `${beste}Keine gültige Zeit.`.trim());
        }
    }

    leeren() {
        this.stoppeUhren();
        this.zustand = "aus";
        this.zeige("neutral", "", "Gleich geht es los");
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    zeige(klasse, symbol, text) {
        if (!this.flaeche) return;

        // Ausgeschrieben statt zusammengesetzt - PhonePageContractUnitTests findet so jede Klasse.
        switch (klasse) {
            case "reaktion-warten": this.flaeche.className = "reaktion-taste reaktion-warten"; break;
            case "reaktion-jetzt": this.flaeche.className = "reaktion-taste reaktion-jetzt"; break;
            case "reaktion-ergebnis": this.flaeche.className = "reaktion-taste reaktion-ergebnis"; break;
            default: this.flaeche.className = "reaktion-taste";
        }

        this.flaeche.disabled = this.zustand !== "rot" && this.zustand !== "gruen";
        this.symbol.textContent = symbol;
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
