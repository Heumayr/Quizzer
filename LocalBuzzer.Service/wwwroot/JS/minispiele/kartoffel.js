// Minispiel Heisse Kartoffel (M35 C6): wer sie haelt, sieht eine Aussage und zwei Knoepfe - wahr oder falsch. Nach einer
// richtigen Antwort waehlt er, an wen er sie weitergibt; alle anderen sehen nur, wer sie gerade haelt. Wann sie platzt,
// weiss allein der Server - das Telefon zeigt bewusst keine Uhr, und ob eine Antwort stimmte, sagt nur der naechste Zustand.

// Nicht wartend bleiben, wenn die Antwort des Servers ausbleibt - dann darf man erneut senden.
const WARTEZEIT_MS = 3000;

// Wer haelt, aus Sicht dieses Telefons.
export function halterText(artInfo) {
    if (artInfo?.isHolder) return "🥔 Du hast die heiße Kartoffel!";
    return artInfo?.holderName ? `🥔 ${artInfo.holderName} hat die heiße Kartoffel.` : "Gleich geht es los";
}

// Nach der Runde, aus Sicht dieses Telefons - in Worten, nie allein ueber Farbe.
export function endeText(artInfo) {
    if (!artInfo?.exploded) return "Vorbei – ohne Knall.";

    const wann = artInfo?.fuseSeconds != null ? ` nach ${artInfo.fuseSeconds} s` : "";
    if (artInfo?.isHolder) return `💥 BUMM${wann}! Bei dir ist sie geplatzt.`;

    const von = artInfo?.passerName ? ` – ${artInfo.passerName} hat sie zugeschoben` : "";
    return `💥 BUMM${wann}! Bei ${artInfo?.holderName ?? "?"} ist sie geplatzt${von}.`;
}

// Eine Zeile der Aufloesung - erst nach der Runde steht die Wahrheit da.
export function aufloesungText(eintrag) {
    return `${eintrag.isTrue ? "✓ wahr" : "✗ falsch"} – ${eintrag.text}`;
}

export class KartoffelModul {
    constructor({ timer } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.senden = null;
        this.letzte = null;
        this.locked = true;
        this.wartet = false;
        this.nummer = 0;
        this.zielTasten = [];
        this.warteUhr = null;
    }

    mount(feld) {
        const inhalt = document.createElement("div");
        inhalt.className = "kartoffel-inhalt";

        const halter = document.createElement("p");
        halter.className = "kartoffel-halter";
        halter.setAttribute("aria-live", "polite");

        const aussage = document.createElement("p");
        aussage.className = "kartoffel-aussage";
        aussage.setAttribute("aria-live", "polite");

        const hinweis = document.createElement("p");
        hinweis.className = "kartoffel-hinweis";
        hinweis.setAttribute("aria-live", "polite");

        const tasten = document.createElement("div");
        tasten.className = "kartoffel-tasten";

        // Nie allein ueber Farbe: Zeichen und Wort auf jedem Knopf.
        const wahr = document.createElement("button");
        wahr.type = "button";
        wahr.className = "kartoffel-taste kartoffel-wahr";
        wahr.textContent = "✓ Wahr";
        wahr.setAttribute("aria-label", "Die Aussage ist wahr");
        wahr.addEventListener("click", () => this.urteile(true));

        const falsch = document.createElement("button");
        falsch.type = "button";
        falsch.className = "kartoffel-taste kartoffel-falsch";
        falsch.textContent = "✗ Falsch";
        falsch.setAttribute("aria-label", "Die Aussage ist falsch");
        falsch.addEventListener("click", () => this.urteile(false));

        const ziele = document.createElement("div");
        ziele.className = "kartoffel-ziele";

        const liste = document.createElement("div");
        liste.className = "kartoffel-liste";

        tasten.append(wahr, falsch);
        inhalt.append(halter, aussage, hinweis, tasten, ziele, liste);
        feld.appendChild(inhalt);
        Object.assign(this, { inhalt, halter, aussage, hinweis, tasten, ziele, liste });
        this.leeren();
    }

    // Nach dem Countdown - mit dem neuesten Stand, falls die Kartoffel waehrenddessen gewandert ist.
    starte(artInfo, senden) {
        this.senden = senden;
        this.zeige(this.letzte ?? artInfo);
        this.letzte = null;
    }

    // Die Antwort des Servers auf eine Abgabe - oder die Kartoffel ist gewandert (alle bekommen ihren Stand).
    aktualisiere(artInfo) {
        if (!this.senden) {
            this.letzte = artInfo;
            return;
        }

        const vorher = this.nummer > 0;
        this.zeige(artInfo);

        // Gerade bekommen: kurz vibrieren, wo der Browser es kann (Android).
        if (!vorher && this.nummer > 0) globalThis.navigator?.vibrate?.(200);
    }

    zeige(artInfo) {
        this.warte(false);
        this.nummer = artInfo?.isHolder ? (artInfo?.statementNumber ?? 0) : 0;
        this.halter.textContent = halterText(artInfo);
        this.ziele.innerHTML = "";
        this.zielTasten = [];
        this.tasten.hidden = true;
        this.aussage.textContent = "";

        if (this.nummer <= 0) {
            this.hinweis.textContent = artInfo?.passerName ? `${artInfo.passerName} hat sie weitergegeben.` : "";
            return;
        }

        if (artInfo?.choosing) {
            this.zeigeZiele(artInfo?.options ?? []);
            return;
        }

        this.tasten.hidden = false;
        this.aussage.textContent = artInfo?.statementText ?? "";
        this.hinweis.textContent = artInfo?.lastWrong ? "✗ Daneben – die nächste: wahr oder falsch?" : "Wahr oder falsch?";
    }

    zeigeZiele(optionen) {
        this.hinweis.textContent = optionen.length > 0
            ? "✓ Richtig! An wen gibst du sie weiter?"
            : "✓ Richtig! Gerade ist niemand zum Weitergeben da – der Spielleiter hilft.";

        for (const spieler of optionen) {
            const taste = document.createElement("button");
            taste.type = "button";
            taste.className = "kartoffel-ziel";
            taste.textContent = `🥔 an ${spieler.name}`;
            taste.addEventListener("click", () => this.gibWeiter(spieler.id, spieler.name));
            this.ziele.appendChild(taste);
            this.zielTasten.push(taste);
        }
    }

    async urteile(wahr) {
        if (!this.bereit() || this.tasten.hidden) return;

        // Erst sperren, dann senden - ein zweiter Tipp darf dieselbe Aussage nicht noch einmal schicken.
        this.warte(true);
        try {
            await this.senden({ statementNumber: this.nummer, answer: wahr });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    async gibWeiter(id, name) {
        if (!this.bereit() || this.zielTasten.length === 0) return;

        // Erst sperren, dann senden. Nie nur Farbe: der Satz sagt, wohin.
        this.warte(true);
        this.zielTasten.forEach(t => { t.disabled = true; });
        this.hinweis.textContent = `Weiter an ${name} …`;
        try {
            await this.senden({ statementNumber: this.nummer, passTo: id });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    bereit() {
        return !!this.senden && !this.locked && !this.wartet && this.nummer > 0;
    }

    warte(an) {
        this.wartet = an;
        if (this.warteUhr != null) this.timer.clearTimeout(this.warteUhr);
        this.warteUhr = an ? this.timer.setTimeout(() => this.warte(false), WARTEZEIT_MS) : null;
    }

    // Die Runde ist zu: wer sie beim Knall hatte, dann die Aussagen samt Wahrheit.
    zeigeErgebnis(artInfo) {
        this.halte();
        this.halter.textContent = endeText(artInfo);
        this.liste.innerHTML = "";

        const aufloesung = artInfo?.resolution ?? [];
        this.hinweis.textContent = aufloesung.length > 0 ? "Die Aussagen dieser Runde:" : "";

        for (const eintrag of aufloesung) {
            const zeile = document.createElement("p");
            zeile.className = "kartoffel-ergebnis";
            zeile.textContent = aufloesungText(eintrag);
            this.liste.appendChild(zeile);
        }
    }

    zeigeEnde(artInfo) {
        this.zeigeErgebnis(artInfo);
    }

    halte() {
        this.senden = null;
        this.nummer = 0;
        this.warte(false);
        this.tasten.hidden = true;
        this.aussage.textContent = "";
        this.ziele.innerHTML = "";
        this.zielTasten = [];
    }

    leeren() {
        this.halte();
        this.letzte = null;
        this.liste.innerHTML = "";
        this.halter.textContent = "";
        this.hinweis.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    dispose() {
        if (this.tasten) this.leeren();
    }
}
