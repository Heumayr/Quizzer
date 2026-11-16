// Minispiel Buchstabensalat (M35 C5): das Wirrwarr des offenen Begriffs, ein Eingabefeld, senden. Den
// Begriff kennt nur der Server - er prueft jede Eingabe; wer zuerst richtig liegt, loest ihn, und jedes
// Telefon bekommt den naechsten. Gross/klein und Umlaute (ae fuer ä) gleicht der Server aus.

// Nicht wartend bleiben, wenn die Antwort des Servers ausbleibt - dann darf man erneut senden.
const WARTEZEIT_MS = 3000;
const TAKT_MS = 250;
const MAX_ZEICHEN = 60;

export function restText(ms) {
    return `Noch ${Math.max(0, Math.ceil(ms / 1000))} s`;
}

export function geloestText(n) {
    return `${n} gelöst`;
}

// Der Satz am Ende, aus Sicht dieses Telefons.
export function endeText(artInfo) {
    const eigen = `Du hast ${geloestText(artInfo?.ownSolved ?? 0)}.`;
    const namen = artInfo?.winnerNames ?? [];

    if (artInfo?.isWinner) return `🏆 Die meisten! ${eigen}`;
    if (namen.length === 0) return `${eigen} Niemand hat einen Begriff gelöst.`;
    return `${eigen} Die meisten hat ${namen.join(", ")}.`;
}

export class BuchstabensalatModul {
    constructor({ timer, now } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.now = now ?? (() => performance.now());
        this.senden = null;
        this.locked = true;
        this.wartet = false;
        this.nummer = 0;
        this.gesamt = 0;
        this.geloest = 0;
        this.ende = 0;
        this.uhr = null;
        this.warteUhr = null;
    }

    mount(feld) {
        const inhalt = document.createElement("div");
        inhalt.className = "buchstabensalat-inhalt";

        const stand = document.createElement("p");
        stand.className = "buchstabensalat-stand";

        const wirrwarr = document.createElement("p");
        wirrwarr.className = "buchstabensalat-wirrwarr";
        wirrwarr.setAttribute("aria-live", "polite");

        const zeile = document.createElement("div");
        zeile.className = "buchstabensalat-zeile";

        const eingabe = document.createElement("input");
        eingabe.className = "buchstabensalat-eingabe";
        eingabe.type = "text";
        eingabe.value = "";
        eingabe.setAttribute("aria-label", "Deine Lösung");
        eingabe.setAttribute("autocomplete", "off");
        eingabe.setAttribute("autocapitalize", "off");
        eingabe.setAttribute("autocorrect", "off");
        eingabe.setAttribute("spellcheck", "false");
        eingabe.setAttribute("enterkeyhint", "send");
        eingabe.setAttribute("maxlength", String(MAX_ZEICHEN));
        eingabe.addEventListener("keydown", (e) => {
            if (e?.key !== "Enter") return;
            e.preventDefault?.();
            this.sende();
        });

        const knopf = document.createElement("button");
        knopf.type = "button";
        knopf.className = "buchstabensalat-senden";
        knopf.textContent = "✓ Lösen";
        knopf.setAttribute("aria-label", "Lösung senden");
        knopf.addEventListener("click", () => this.sende());

        const hinweis = document.createElement("p");
        hinweis.className = "buchstabensalat-hinweis";
        hinweis.setAttribute("aria-live", "polite");

        zeile.append(eingabe, knopf);
        inhalt.append(stand, wirrwarr, zeile, hinweis);
        feld.appendChild(inhalt);
        Object.assign(this, { inhalt, stand, wirrwarr, zeile, eingabe, knopf, hinweis });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.senden = senden;
        this.nummer = 0;
        this.geloest = artInfo?.ownSolved ?? 0;
        this.zeile.hidden = false;
        this.uebernimm(artInfo);
        this.ticke();
    }

    // Die Antwort des Servers - auf die eigene Eingabe oder weil ein anderer geloest hat.
    aktualisiere(artInfo) {
        if (!this.senden) return;
        this.uebernimm(artInfo);
    }

    uebernimm(artInfo) {
        const nummer = artInfo?.termNumber ?? 0;
        const selbst = (artInfo?.ownSolved ?? 0) > this.geloest;
        const neu = nummer !== this.nummer;

        this.nummer = nummer;
        this.gesamt = artInfo?.totalTerms ?? 0;
        this.geloest = artInfo?.ownSolved ?? 0;
        this.warte(false);
        this.ende = this.now() + (artInfo?.remainingMs ?? (artInfo?.durationSeconds ?? 0) * 1000);
        this.wirrwarr.textContent = artInfo?.scramble ?? "";

        // Ein neuer Begriff: das Feld leeren - die alte Eingabe gehoert zum alten Begriff.
        if (neu) this.eingabe.value = "";

        // Nie allein ueber Farbe: Zeichen und Wort.
        if (selbst && artInfo?.lastSolved) this.hinweis.textContent = `✓ Richtig! „${artInfo.lastSolved}“ gehört dir.`;
        else if (neu && artInfo?.lastSolved) this.hinweis.textContent = `${artInfo?.lastSolver ?? "Jemand"} hat „${artInfo.lastSolved}“ gelöst – weiter!`;
        else if (artInfo?.lastWrong) this.hinweis.textContent = "✗ Leider nicht – versuch es weiter.";
        else this.hinweis.textContent = "Tippe den Begriff und dann ✓.";

        this.zeigeStand();
    }

    async sende() {
        const text = String(this.eingabe.value ?? "").trim();
        if (!this.bereit() || text === "") return;

        // Erst sperren, dann senden - ein zweiter Tipp darf dieselbe Eingabe nicht noch einmal schicken.
        this.warte(true);
        try {
            await this.senden({ termNumber: this.nummer, guess: text.slice(0, MAX_ZEICHEN) });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    bereit() {
        return !!this.senden && !this.locked && !this.wartet && this.nummer > 0 && this.rest() > 0;
    }

    warte(an) {
        this.wartet = an;
        if (this.warteUhr != null) this.timer.clearTimeout(this.warteUhr);
        this.warteUhr = an ? this.timer.setTimeout(() => this.warte(false), WARTEZEIT_MS) : null;
    }

    rest() {
        return Math.max(0, this.ende - this.now());
    }

    ticke() {
        this.zeigeStand();
        if (this.rest() <= 0) {
            this.uhr = null;
            this.wirrwarr.textContent = "Die Zeit ist um.";
            return;
        }
        this.uhr = this.timer.setTimeout(() => this.ticke(), TAKT_MS);
    }

    zeigeStand() {
        const begriff = this.nummer > 0 ? `Begriff ${this.nummer} von ${this.gesamt} · ` : "";
        this.stand.textContent = `${begriff}${geloestText(this.geloest)} · ${restText(this.rest())}`;
    }

    zeigeErgebnis(artInfo) {
        this.halte();
        this.stand.textContent = geloestText(artInfo?.ownSolved ?? 0);
    }

    zeigeEnde(artInfo) {
        this.halte();
        this.hinweis.textContent = endeText(artInfo);
        this.stand.textContent = geloestText(artInfo?.ownSolved ?? 0);
    }

    halte() {
        this.senden = null;
        this.warte(false);
        if (this.uhr != null) this.timer.clearTimeout(this.uhr);
        this.uhr = null;
        this.zeile.hidden = true;
        this.wirrwarr.textContent = "";
        this.eingabe.value = "";
    }

    leeren() {
        this.halte();
        this.stand.textContent = "";
        this.hinweis.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    dispose() {
        if (this.zeile) this.leeren();
    }
}
