// Minispiel Bluff (M35 C6): Phase 1 - eine falsche Antwort eintippen, die echt klingt. Phase 2 - unter allen
// Antworten die echte waehlen; der eigene Bluff steht nicht in der Liste, und der Server nimmt ihn auch nicht an.
// Welche Antwort echt ist und wer welchen Bluff schrieb, erfaehrt das Telefon nie vor dem Ende - Urheber nie.

const WARTEZEIT_MS = 3000;
const TAKT_MS = 250;
const MAX_ZEICHEN = 60;

export function restText(ms) {
    return `Noch ${Math.max(0, Math.ceil(ms / 1000))} s`;
}

// Nach der Wahl, aus Sicht dieses Telefons - nie allein ueber Farbe.
export function endeText(artInfo) {
    const echt = artInfo?.realAnswer ? `Echt war „${artInfo.realAnswer}“. ` : "";
    const gefunden = artInfo?.foundReal ? "✓ Du hast sie gefunden!" : "✗ Du hast sie nicht gefunden.";
    const n = artInfo?.fooledCount ?? 0;
    const reinfall = artInfo?.ownStruck ? " Dein Bluff war zu nah an der Wahrheit und wurde gestrichen."
        : n > 0 ? ` ${n} ${n === 1 ? "ist" : "sind"} auf deinen Bluff hereingefallen.` : "";
    return `${echt}${gefunden}${reinfall}`;
}

export class BluffModul {
    constructor({ timer, now } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.now = now ?? (() => performance.now());
        this.senden = null;
        this.locked = true;
        this.wartet = false;
        this.stufe = 0;
        this.ende = 0;
        this.uhr = null;
        this.warteUhr = null;
    }

    mount(feld) {
        const inhalt = document.createElement("div");
        inhalt.className = "bluff-inhalt";

        const stand = document.createElement("p");
        stand.className = "bluff-stand";

        const zeile = document.createElement("div");
        zeile.className = "bluff-zeile";

        const eingabe = document.createElement("input");
        eingabe.className = "bluff-eingabe";
        eingabe.type = "text";
        eingabe.value = "";
        eingabe.setAttribute("aria-label", "Dein Bluff");
        eingabe.setAttribute("autocomplete", "off");
        eingabe.setAttribute("enterkeyhint", "send");
        eingabe.setAttribute("maxlength", String(MAX_ZEICHEN));
        eingabe.addEventListener("keydown", (e) => {
            if (e?.key !== "Enter") return;
            e.preventDefault?.();
            this.sendeBluff();
        });

        const knopf = document.createElement("button");
        knopf.type = "button";
        knopf.className = "bluff-senden";
        knopf.textContent = "✓ Abgeben";
        knopf.setAttribute("aria-label", "Bluff abgeben");
        knopf.addEventListener("click", () => this.sendeBluff());

        const liste = document.createElement("div");
        liste.className = "bluff-liste";

        const hinweis = document.createElement("p");
        hinweis.className = "bluff-hinweis";
        hinweis.setAttribute("aria-live", "polite");

        zeile.append(eingabe, knopf);
        inhalt.append(stand, zeile, liste, hinweis);
        feld.appendChild(inhalt);
        Object.assign(this, { inhalt, stand, zeile, eingabe, knopf, liste, hinweis });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.senden = senden;
        this.stufe = artInfo?.stage ?? 0;
        this.eingabe.value = "";
        this.zeile.hidden = this.stufe !== 1;
        this.zeigeOptionen(this.stufe === 2 ? artInfo?.options ?? [] : []);
        this.hinweis.textContent = this.stufe === 1
            ? "Denk dir eine falsche Antwort aus, die echt klingt."
            : "Welche Antwort ist die echte? Dein eigener Bluff steht nicht dabei.";
        this.uebernimm(artInfo);
        this.ticke();
    }

    aktualisiere(artInfo) {
        if (!this.senden) return;
        this.uebernimm(artInfo);
    }

    uebernimm(artInfo) {
        this.warte(false);
        this.ende = this.now() + (artInfo?.remainingMs ?? (artInfo?.durationSeconds ?? 0) * 1000);
        this.zeigeStand();
    }

    // Je Antwort eine Taste mit ihrem Text - die Nummer kommt vom Server, das Telefon rechnet nichts.
    zeigeOptionen(optionen) {
        this.liste.innerHTML = "";
        for (const option of optionen) {
            const taste = document.createElement("button");
            taste.type = "button";
            taste.className = "bluff-option";
            taste.textContent = option.text;
            taste.addEventListener("click", () => this.waehle(option.number));
            this.liste.appendChild(taste);
        }
    }

    async sendeBluff() {
        const text = String(this.eingabe.value ?? "").trim();
        if (!this.bereit() || this.stufe !== 1 || text === "") return;

        this.warte(true);
        try {
            await this.senden({ bluff: text.slice(0, MAX_ZEICHEN) });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    async waehle(nummer) {
        if (!this.bereit() || this.stufe !== 2) return;

        this.warte(true);
        try {
            await this.senden({ choice: nummer });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    bereit() {
        return !!this.senden && !this.locked && !this.wartet && this.rest() > 0;
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
        const phase = this.stufe === 2 ? "Phase 2: wählen" : "Phase 1: bluffen";
        this.stand.textContent = `${phase} · ${restText(this.rest())}`;
    }

    // Abgegeben oder Phase vorbei: was dieses Telefon abgegeben hat, in Worten.
    zeigeErgebnis(artInfo) {
        this.halte();
        const gewaehlt = (artInfo?.options ?? []).find(o => o.number === artInfo?.ownChoice);
        this.hinweis.textContent = gewaehlt ? `Gewählt: „${gewaehlt.text}“ – warte auf die anderen.`
            : artInfo?.ownBluff ? `Dein Bluff: „${artInfo.ownBluff}“ – gleich wird gewählt.`
            : "Warte auf die anderen.";
    }

    zeigeEnde(artInfo) {
        this.halte();
        this.hinweis.textContent = artInfo?.realAnswer ? endeText(artInfo) : "Das Minispiel ist vorbei.";
    }

    halte() {
        this.senden = null;
        this.warte(false);
        if (this.uhr != null) this.timer.clearTimeout(this.uhr);
        this.uhr = null;
        this.zeile.hidden = true;
        this.eingabe.value = "";
        this.stand.textContent = "";
        this.zeigeOptionen([]);
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
