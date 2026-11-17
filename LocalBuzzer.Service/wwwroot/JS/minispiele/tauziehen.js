// Minispiel Tauziehen (M37 T9): Team gegen Team - nach dem Countdown so oft tippen wie moeglich, jeder Tipp zieht das Seil
// zum eigenen Team. Gezaehlt wird HIER, entschieden an der Serveruhr: im Takt geht der Stand an den Server, und mit jeder
// Antwort kommt das Seil zurueck. Ueber der Grenze zaehlt das Telefon gar nicht erst. Die Taste traegt die Teamfarbe nur als
// Klasse team-farbe-N (styles/team.css), nie als Farbwert aus der Nachricht; Teamnamen kommen nur als Text hinein.

import { TEAM_FARBEN } from "../team.js";

export function tippText(n) {
    return n === 1 ? "1 Tipp" : `${n} Tipps`;
}

// „✚ Rot: Ø 12,5 Tipps“ - Symbol und Name tragen, die Farbe nie allein.
export function teamText(seite) {
    const name = `${seite?.symbol ?? ""} ${seite?.name ?? ""}`.trim();
    return seite?.pull ? `${name}: ${seite.pull}` : name;
}

// Wo der Knoten steht, in Prozent der Seilbreite: −1 ganz links, +1 ganz rechts.
export function knotenProzent(rope) {
    const lage = Math.max(-1, Math.min(1, Number(rope) || 0));
    return Math.round((lage + 1) * 50);
}

export class TauziehenModul {
    constructor({ timer, now } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.now = now ?? (() => performance.now());
        this.taste = null;
        this.symbol = null;
        this.zahl = null;
        this.text = null;
        this.seil = null;
        this.links = null;
        this.knoten = null;
        this.rechts = null;
        this.stand = null;
        this.zustand = "aus";
        this.senden = null;
        this.locked = true;
        this.tipps = 0;
        this.vorher = 0;
        this.seit = 0;
        this.dauerMs = 0;
        this.grenze = 20;
        this.takt = 500;
        this.uhren = [];
        this.farbe = null;
        this.onPointerDown = this.onPointerDown.bind(this);
    }

    mount(feld) {
        const taste = document.createElement("button");
        taste.type = "button";
        taste.className = "tauziehen-taste";

        const symbol = document.createElement("span");
        symbol.className = "tauziehen-symbol";
        symbol.setAttribute("aria-hidden", "true");

        const zahl = document.createElement("span");
        zahl.className = "tauziehen-zahl";

        const text = document.createElement("span");
        text.className = "tauziehen-text";
        text.setAttribute("aria-live", "polite");

        taste.append(symbol, zahl, text);

        const seil = document.createElement("div");
        seil.className = "tauziehen-seil";
        seil.hidden = true;

        const links = document.createElement("span");
        links.className = "tauziehen-links";

        const knoten = document.createElement("span");
        knoten.className = "tauziehen-knoten";
        knoten.setAttribute("aria-hidden", "true");
        knoten.textContent = "◆";

        const rechts = document.createElement("span");
        rechts.className = "tauziehen-rechts";

        seil.append(links, knoten, rechts);

        const stand = document.createElement("p");
        stand.className = "tauziehen-stand";
        stand.setAttribute("aria-live", "polite");

        feld.append(taste, seil, stand);

        // pointerdown wie beim Buzzer: jeder Druck zaehlt, nicht erst das Loslassen.
        taste.addEventListener("pointerdown", this.onPointerDown);

        Object.assign(this, { taste, symbol, zahl, text, seil, links, knoten, rechts, stand });
        this.zeige("neutral", "", "", "Gleich geht es los");
    }

    // Das Ziehen beginnt. Nach dem Wiederverbinden geht es beim letzten angenommenen Stand weiter, mit der Restzeit des Servers.
    starte(artInfo, senden) {
        this.stoppeUhren();
        this.senden = senden;
        this.dauerMs = (artInfo?.durationSeconds ?? 15) * 1000;
        this.grenze = artInfo?.maxTapsPerSecond ?? 20;
        this.takt = artInfo?.reportIntervalMs ?? 500;
        this.tipps = artInfo?.ownTaps ?? 0;
        this.vorher = Math.min(this.dauerMs, artInfo?.elapsedMs ?? 0);
        this.seit = this.now();
        this.zustand = "aktiv";

        this.faerbe(artInfo);
        this.zeigeSeil(artInfo);
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

    // Im Takt immer melden, auch ohne neuen Tipp: die Antwort bringt das Seil, das die anderen bewegen.
    melde() {
        if (this.zustand !== "aktiv") return;

        this.sende({ taps: this.tipps, isFinal: false });
        this.zeigeStand();
        this.uhr(() => this.melde(), this.takt);
    }

    ende() {
        if (this.zustand !== "aktiv") return;

        // Erst sperren, dann senden - ein spaeter Tipp darf nichts mehr zaehlen.
        this.zustand = "abgegeben";
        this.stoppeUhren();
        this.zeige("tauziehen-ergebnis", "✔", String(this.tipps), `Zeit um! ${tippText(this.tipps)} – gleich steht fest, wer gewinnt.`);
        this.sende({ taps: this.tipps, isFinal: true });
    }

    async sende(payload) {
        try {
            await this.senden?.(payload);
        } catch {
            // Gemeldet hat es index.js; der naechste Stand geht trotzdem raus.
        }
    }

    // Mitten in der Runde: der Server hat den eigenen Stand angenommen und schickt das Seil zurueck.
    aktualisiere(artInfo) {
        this.zeigeSeil(artInfo);
    }

    zeigeStand() {
        const rest = Math.max(0, Math.ceil((this.dauerMs - this.vergangen()) / 1000));
        this.zeige("tauziehen-aktiv", "💪", String(this.tipps), `Ziehen, ziehen, ziehen! Noch ${rest} s`);
    }

    // Eigener Stand, etwa nach dem Wiederverbinden oder nach Rundenende.
    zeigeErgebnis(artInfo) {
        this.stoppeUhren();
        this.zustand = "abgegeben";
        this.faerbe(artInfo);
        this.zeigeSeil(artInfo);

        const eigene = artInfo?.ownTaps;
        this.zeige("tauziehen-ergebnis", "✔", eigene != null ? String(eigene) : "–",
            eigene != null ? `Deine Tipps: ${tippText(eigene)}` : "Keine Abgabe");
    }

    zeigeEnde(artInfo) {
        this.stoppeUhren();
        this.zustand = "aus";
        this.faerbe(artInfo);
        this.zeigeSeil(artInfo);

        const eigene = artInfo?.ownTaps != null ? `Du: ${tippText(artInfo.ownTaps)}. ` : "";
        const sieger = (artInfo?.teams ?? []).filter(seite => seite.isWinner).map(seite => `${seite.symbol ?? ""} ${seite.name ?? ""}`.trim());

        if (artInfo?.isWinner) {
            this.zeige("tauziehen-ergebnis", "🏆", "", `Gewonnen! Euer Team zieht stärker. ${eigene}`.trim());
        } else if (sieger.length > 0) {
            this.zeige("tauziehen-ergebnis", "■", "", `${eigene}Gewonnen: ${sieger.join(", ")}`);
        } else {
            this.zeige("tauziehen-ergebnis", "■", "", `${eigene}Kein Team hat gezogen.`.trim());
        }
    }

    leeren() {
        this.stoppeUhren();
        this.zustand = "aus";
        this.zeigeSeil(null);
        this.zeige("neutral", "", "", "Gleich geht es los");
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    // Die Farbe des eigenen Teams - nur ein Platz der Palette, sonst neutral; gesetzt wird sie mit der Klasse in zeige().
    faerbe(artInfo) {
        const farbe = (artInfo?.teams ?? []).filter(seite => seite.isOwn).map(seite => seite.colorIndex)[0];
        this.farbe = Number.isInteger(farbe) && farbe >= 0 && farbe < TEAM_FARBEN ? farbe : null;
    }

    // Seil nur bei zwei Teams; der Stand aller Teams immer als Text, das eigene vorn.
    zeigeSeil(artInfo) {
        if (!this.seil) return;

        const teams = artInfo?.teams ?? [];
        const zwei = artInfo?.rope != null && teams.length === 2;

        this.seil.hidden = !zwei;
        if (zwei) {
            this.links.textContent = teamText(teams[0]);
            this.rechts.textContent = teamText(teams[1]);
            this.knoten.style.left = `${knotenProzent(artInfo.rope)}%`;
        }

        const eigenes = teams.filter(seite => seite.isOwn).map(seite => `Euer Team ${teamText(seite)}`);
        const andere = teams.filter(seite => !seite.isOwn).map(teamText);
        this.stand.textContent = [...eigenes, ...andere].join(" · ");
    }

    zeige(klasse, symbol, zahl, text) {
        if (!this.taste) return;

        // Ausgeschrieben statt zusammengesetzt - PhonePageContractUnitTests findet so jede Klasse. Die Teamfarbe bleibt stehen.
        switch (klasse) {
            case "tauziehen-aktiv": this.taste.className = "tauziehen-taste tauziehen-aktiv"; break;
            case "tauziehen-ergebnis": this.taste.className = "tauziehen-taste tauziehen-ergebnis"; break;
            default: this.taste.className = "tauziehen-taste";
        }
        if (this.farbe != null) this.taste.classList.add(`team-farbe-${this.farbe}`);

        this.taste.disabled = this.zustand !== "aktiv";
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
        this.taste?.removeEventListener("pointerdown", this.onPointerDown);
    }
}
