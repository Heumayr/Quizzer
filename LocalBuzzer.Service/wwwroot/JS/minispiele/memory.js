// Minispiel Memory (M35 C5): jeder sein Feld, alle dieselbe Belegung - aber die kennt nur der Server.
// Jedes Umdrehen fragt ihn; zurueck kommen nur die offenen Karten und die eigenen Paare. Zwei offene
// ohne Paar bleiben liegen, bis die naechste Karte umgedreht wird.

const TAKT_MS = 250;

export function restText(ms) {
    return `Noch ${Math.max(0, Math.ceil(ms / 1000))} s`;
}

// 42300 -> "42,3 s" - dieselbe Schreibweise wie am Beamer (MemoryEvaluator.Sekunden).
export function sekundenText(ms) {
    return (ms / 1000).toFixed(1).replace(".", ",") + " s";
}

export function paareText(artInfo) {
    return `${artInfo?.ownPairs ?? 0} von ${artInfo?.totalPairs ?? 0} Paaren`;
}

// Der Satz am Ende, aus Sicht dieses Telefons.
export function endeText(artInfo) {
    const eigen = artInfo?.ownFinishedMs != null
        ? `Alle Paare in ${sekundenText(artInfo.ownFinishedMs)}.`
        : `${paareText(artInfo)}.`;
    const namen = artInfo?.winnerNames ?? [];

    if (artInfo?.isWinner) return `🏆 Gewonnen! ${eigen}`;
    if (namen.length === 0) return `${eigen} Niemand hat ein Paar gefunden.`;
    return `${eigen} Gewonnen hat ${namen.join(", ")}.`;
}

export class MemoryModul {
    constructor({ timer, now } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.now = now ?? (() => performance.now());
        this.senden = null;
        this.locked = true;
        this.karten = [];
        this.offen = new Map();
        this.gefunden = new Map();
        this.paare = 0;
        this.ende = 0;
        this.uhr = null;
    }

    mount(feld) {
        const inhalt = document.createElement("div");
        inhalt.className = "memory-inhalt";

        const stand = document.createElement("p");
        stand.className = "memory-stand";

        const hinweis = document.createElement("p");
        hinweis.className = "memory-hinweis";
        hinweis.setAttribute("aria-live", "polite");

        const raster = document.createElement("div");
        raster.className = "memory-raster";

        inhalt.append(stand, hinweis, raster);
        feld.appendChild(inhalt);
        Object.assign(this, { inhalt, stand, hinweis, raster });
        this.leeren();
    }

    // Die Karten selbst, alle verdeckt - welches Motiv wo liegt, sagt erst der Server je Karte.
    baue(anzahl) {
        if (this.karten.length === anzahl) return;

        this.raster.innerHTML = "";
        this.karten = [];
        for (let i = 0; i < anzahl; i++) {
            const knopf = document.createElement("button");
            knopf.type = "button";
            knopf.addEventListener("click", () => this.drehe(i));
            this.raster.appendChild(knopf);
            this.karten.push(knopf);
        }
    }

    starte(artInfo, senden) {
        this.senden = senden;
        this.paare = artInfo?.ownPairs ?? 0;
        this.uebernimm(artInfo);
        this.hinweis.textContent = "Finde die Paare – tippe eine Karte.";
        this.ticke();
    }

    // Die Antwort des Servers auf ein Umdrehen.
    aktualisiere(artInfo) {
        if (!this.senden) return;

        const vorher = this.paare;
        this.uebernimm(artInfo);

        // Nie allein ueber Farbe: Zeichen und Wort.
        if (this.paare > vorher) this.hinweis.textContent = "✓ Ein Paar!";
        else if (this.offen.size === 2) this.hinweis.textContent = "✗ Kein Paar – tippe die nächste Karte.";
        else this.hinweis.textContent = "Und die zweite?";
    }

    uebernimm(artInfo) {
        this.baue(artInfo?.cardCount ?? 0);
        this.offen = new Map((artInfo?.openCards ?? []).map(karte => [karte.index, karte]));
        this.gefunden = new Map((artInfo?.foundCards ?? []).map(karte => [karte.index, karte]));
        this.paare = artInfo?.ownPairs ?? 0;
        this.gesamt = paareText(artInfo);
        if (artInfo?.remainingMs != null) this.ende = this.now() + artInfo.remainingMs;

        this.karten.forEach((knopf, i) => this.zeigeKarte(knopf, i));
        this.zeigeStand();
    }

    // Nie allein ueber Farbe: verdeckt "?", offen das Motiv, gefunden das Motiv mit "gefunden" fuer Vorleser.
    zeigeKarte(knopf, i) {
        const gefunden = this.gefunden.has(i);
        const karte = this.gefunden.get(i) ?? this.offen.get(i);

        if (!karte) {
            knopf.className = "memory-karte memory-verdeckt";
            knopf.textContent = "?";
            knopf.setAttribute("aria-label", `Karte ${i + 1}, verdeckt`);
            return;
        }

        knopf.className = gefunden ? "memory-karte memory-gefunden" : "memory-karte memory-offen";
        knopf.textContent = karte.symbol;
        knopf.setAttribute("aria-label", gefunden ? `${karte.name} – gefunden` : karte.name);
    }

    drehe(i) {
        if (!this.senden || this.locked || this.rest() <= 0) return;

        // Eine gefundene oder die eine schon offene Karte fragt den Server nicht - er verwuerfe sie ohnehin.
        if (this.gefunden.has(i) || (this.offen.size === 1 && this.offen.has(i))) return;

        Promise.resolve(this.senden({ card: i })).catch(() => {
            // Gemeldet hat es index.js.
        });
    }

    rest() {
        return Math.max(0, this.ende - this.now());
    }

    ticke() {
        this.zeigeStand();
        if (this.rest() <= 0) {
            this.uhr = null;
            this.hinweis.textContent = "Die Zeit ist um.";
            return;
        }
        this.uhr = this.timer.setTimeout(() => this.ticke(), TAKT_MS);
    }

    zeigeStand() {
        this.stand.textContent = this.senden ? `${this.gesamt ?? ""} · ${restText(this.rest())}` : (this.gesamt ?? "");
    }

    // Fertig, die anderen spielen noch.
    zeigeErgebnis(artInfo) {
        this.halte();
        this.uebernimm(artInfo);
        this.hinweis.textContent = artInfo?.ownFinishedMs != null
            ? `✓ Alle Paare in ${sekundenText(artInfo.ownFinishedMs)} – warte auf die anderen.`
            : "Warte auf die anderen.";
    }

    zeigeEnde(artInfo) {
        this.halte();
        this.uebernimm(artInfo);
        this.hinweis.textContent = endeText(artInfo);
    }

    halte() {
        this.senden = null;
        if (this.uhr != null) this.timer.clearTimeout(this.uhr);
        this.uhr = null;
    }

    leeren() {
        this.halte();
        this.offen = new Map();
        this.gefunden = new Map();
        this.gesamt = "";
        this.baue(0);
        this.stand.textContent = "";
        this.hinweis.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    dispose() {
        if (this.raster) this.leeren();
    }
}
