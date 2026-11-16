// Minispiel Erklaerbaer (M35 C6): das Telefon des Erklaerers zeigt den Begriff, alle anderen ein Eingabefeld.
// Den Begriff kennt sonst nur der Server - er prueft jede Eingabe; der erste Treffer schliesst die Runde. Gross/klein
// und Umlaute (ae fuer ä) gleicht der Server aus; einen Versuch des Erklaerers verwirft er.

// Nicht wartend bleiben, wenn die Antwort des Servers ausbleibt - dann darf man erneut senden.
const WARTEZEIT_MS = 3000;
const TAKT_MS = 250;
const MAX_ZEICHEN = 60;

export function restText(ms) {
    return `Noch ${Math.max(0, Math.ceil(ms / 1000))} s`;
}

// Nach der Runde, aus Sicht dieses Telefons - nie allein ueber Farbe.
export function ergebnisText(artInfo) {
    const begriff = artInfo?.solution ? `Gesucht war „${artInfo.solution}“. ` : "";

    if (artInfo?.isSolver) return `${begriff}✓ Du hast es erraten!`;
    if (artInfo?.solverName) return `${begriff}${artInfo.solverName} hat es erraten.`;
    return `${begriff}Niemand hat es erraten.`;
}

export class ErklaerbaerModul {
    constructor({ timer, now } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.now = now ?? (() => performance.now());
        this.senden = null;
        this.locked = true;
        this.wartet = false;
        this.erklaerer = false;
        this.name = "";
        this.ende = 0;
        this.uhr = null;
        this.warteUhr = null;
    }

    mount(feld) {
        const inhalt = document.createElement("div");
        inhalt.className = "baer-inhalt";

        const stand = document.createElement("p");
        stand.className = "baer-stand";

        const begriff = document.createElement("p");
        begriff.className = "baer-begriff";

        const zeile = document.createElement("div");
        zeile.className = "baer-zeile";

        const eingabe = document.createElement("input");
        eingabe.className = "baer-eingabe";
        eingabe.type = "text";
        eingabe.value = "";
        eingabe.setAttribute("aria-label", "Dein Rateversuch");
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
        knopf.className = "baer-senden";
        knopf.textContent = "✓ Raten";
        knopf.setAttribute("aria-label", "Rateversuch senden");
        knopf.addEventListener("click", () => this.sende());

        const hinweis = document.createElement("p");
        hinweis.className = "baer-hinweis";
        hinweis.setAttribute("aria-live", "polite");

        zeile.append(eingabe, knopf);
        inhalt.append(stand, begriff, zeile, hinweis);
        feld.appendChild(inhalt);
        Object.assign(this, { inhalt, stand, begriff, zeile, eingabe, knopf, hinweis });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.senden = senden;
        this.erklaerer = !!artInfo?.isExplainer;
        this.name = artInfo?.explainerName ?? "";
        this.eingabe.value = "";

        // Der Erklaerer sieht den Begriff und raet nicht; alle anderen sehen ihn nie.
        this.zeile.hidden = this.erklaerer;
        this.begriff.textContent = this.erklaerer ? (artInfo?.term ?? "") : "";
        this.hinweis.textContent = this.erklaerer
            ? "Erkläre diesen Begriff – ohne ihn zu sagen!"
            : `${this.name || "Jemand"} erklärt – tippe den Begriff und dann ✓.`;

        this.uebernimm(artInfo);
        this.ticke();
    }

    // Die Antwort des Servers auf den eigenen Versuch.
    aktualisiere(artInfo) {
        if (!this.senden) return;
        this.uebernimm(artInfo);
        if (!this.erklaerer && artInfo?.lastWrong) this.hinweis.textContent = "✗ Leider nicht – versuch es weiter.";
    }

    uebernimm(artInfo) {
        this.warte(false);
        this.ende = this.now() + (artInfo?.remainingMs ?? (artInfo?.durationSeconds ?? 0) * 1000);
        this.zeigeStand();
    }

    async sende() {
        const text = String(this.eingabe.value ?? "").trim();
        if (!this.bereit() || text === "") return;

        // Erst sperren, dann senden - ein zweiter Tipp darf dieselbe Eingabe nicht noch einmal schicken.
        this.warte(true);
        this.eingabe.value = "";
        try {
            await this.senden({ guess: text.slice(0, MAX_ZEICHEN) });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    bereit() {
        return !!this.senden && !this.erklaerer && !this.locked && !this.wartet && this.rest() > 0;
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
            this.hinweis.textContent = "Die Zeit ist um.";
            return;
        }
        this.uhr = this.timer.setTimeout(() => this.ticke(), TAKT_MS);
    }

    zeigeStand() {
        const wer = this.erklaerer ? "Du erklärst" : `${this.name || "Jemand"} erklärt`;
        this.stand.textContent = `${wer} · ${restText(this.rest())}`;
    }

    zeigeErgebnis(artInfo) {
        this.halte();
        this.hinweis.textContent = ergebnisText(artInfo);
    }

    zeigeEnde(artInfo) {
        this.zeigeErgebnis(artInfo);
    }

    halte() {
        this.senden = null;
        this.warte(false);
        if (this.uhr != null) this.timer.clearTimeout(this.uhr);
        this.uhr = null;
        this.zeile.hidden = true;
        this.begriff.textContent = "";
        this.eingabe.value = "";
        this.stand.textContent = "";
    }

    leeren() {
        this.halte();
        this.hinweis.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    dispose() {
        if (this.zeile) this.leeren();
    }
}
