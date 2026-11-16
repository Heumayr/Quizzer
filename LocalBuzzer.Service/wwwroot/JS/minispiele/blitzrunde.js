// Minispiel Blitzrunde (M35 C5): eine Aussage, zwei Knoepfe - wahr oder falsch. Die naechste Aussage
// schickt der Server erst nach der Antwort, und ob sie stimmte, sagt er erst nach der Runde: sonst
// koennte man es dem Nachbarn verraten, der dieselbe Aussage noch vor sich hat.

// Nicht wartend bleiben, wenn die Antwort des Servers ausbleibt - dann darf man erneut senden.
const WARTEZEIT_MS = 3000;
const TAKT_MS = 250;

export function restText(ms) {
    return `Noch ${Math.max(0, Math.ceil(ms / 1000))} s`;
}

export function fortschrittText(beantwortet, gesamt) {
    return `${beantwortet} von ${gesamt} beantwortet`;
}

// Der Satz nach der Runde, aus Sicht dieses Telefons - richtig und falsch gibt es erst dann.
export function endeText(artInfo) {
    if (artInfo?.ownCorrect == null) return "Gleich kommt die Auflösung.";

    const ohne = Math.max(0, (artInfo?.totalStatements ?? 0) - (artInfo?.ownCorrect ?? 0) - (artInfo?.ownWrong ?? 0));
    return `✓ ${artInfo.ownCorrect} richtig, ✗ ${artInfo?.ownWrong ?? 0} falsch` + (ohne > 0 ? `, ${ohne} ohne Antwort.` : ".");
}

export class BlitzrundeModul {
    constructor({ timer, now } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.now = now ?? (() => performance.now());
        this.senden = null;
        this.locked = true;
        this.wartet = false;
        this.nummer = 0;
        this.gesamt = 0;
        this.beantwortet = 0;
        this.ende = 0;
        this.uhr = null;
        this.warteUhr = null;
    }

    mount(feld) {
        const inhalt = document.createElement("div");
        inhalt.className = "blitzrunde-inhalt";

        const stand = document.createElement("p");
        stand.className = "blitzrunde-stand";

        const aussage = document.createElement("p");
        aussage.className = "blitzrunde-aussage";
        aussage.setAttribute("aria-live", "polite");

        const hinweis = document.createElement("p");
        hinweis.className = "blitzrunde-hinweis";
        hinweis.setAttribute("aria-live", "polite");

        const tasten = document.createElement("div");
        tasten.className = "blitzrunde-tasten";

        // Nie allein ueber Farbe: Zeichen und Wort auf jedem Knopf.
        const wahr = document.createElement("button");
        wahr.type = "button";
        wahr.className = "blitzrunde-taste blitzrunde-wahr";
        wahr.textContent = "✓ Wahr";
        wahr.setAttribute("aria-label", "Die Aussage ist wahr");
        wahr.addEventListener("click", () => this.urteile(true));

        const falsch = document.createElement("button");
        falsch.type = "button";
        falsch.className = "blitzrunde-taste blitzrunde-falsch";
        falsch.textContent = "✗ Falsch";
        falsch.setAttribute("aria-label", "Die Aussage ist falsch");
        falsch.addEventListener("click", () => this.urteile(false));

        tasten.append(wahr, falsch);
        inhalt.append(stand, aussage, hinweis, tasten);
        feld.appendChild(inhalt);
        Object.assign(this, { inhalt, stand, aussage, hinweis, tasten });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.senden = senden;
        this.nummer = 0;
        this.tasten.hidden = false;
        this.uebernimm(artInfo);
        this.ticke();
    }

    // Die Antwort des Servers auf eine Abgabe: naechste Aussage oder (verworfen) dieselbe.
    aktualisiere(artInfo) {
        if (!this.senden) return;
        this.uebernimm(artInfo);
    }

    uebernimm(artInfo) {
        this.nummer = artInfo?.statementNumber ?? 0;
        this.gesamt = artInfo?.totalStatements ?? 0;
        this.beantwortet = artInfo?.ownAnswered ?? 0;
        this.warte(false);
        this.ende = this.now() + (artInfo?.remainingMs ?? (artInfo?.durationSeconds ?? 0) * 1000);
        this.aussage.textContent = artInfo?.statementText ?? "";
        this.hinweis.textContent = this.nummer > 0 ? `Aussage ${this.nummer} von ${this.gesamt}: wahr oder falsch?` : "";
        this.zeigeStand();
    }

    async urteile(wahr) {
        if (!this.bereit()) return;

        // Erst sperren, dann senden - ein zweiter Tipp darf dieselbe Aussage nicht noch einmal schicken.
        this.warte(true);
        try {
            await this.senden({ statementNumber: this.nummer, answer: wahr });
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
            this.aussage.textContent = "Die Zeit ist um.";
            this.hinweis.textContent = "";
            return;
        }
        this.uhr = this.timer.setTimeout(() => this.ticke(), TAKT_MS);
    }

    zeigeStand() {
        this.stand.textContent = `${fortschrittText(this.beantwortet, this.gesamt)} · ${restText(this.rest())}`;
    }

    // Alle beantwortet oder die Runde ist zu: kein Knopf mehr; richtig und falsch erst, wenn der Server sie schickt.
    zeigeErgebnis(artInfo) {
        this.halte();
        this.stand.textContent = fortschrittText(artInfo?.ownAnswered ?? 0, artInfo?.totalStatements ?? 0);
        this.hinweis.textContent = endeText(artInfo);
    }

    zeigeEnde(artInfo) {
        this.zeigeErgebnis(artInfo);
    }

    halte() {
        this.senden = null;
        this.warte(false);
        if (this.uhr != null) this.timer.clearTimeout(this.uhr);
        this.uhr = null;
        this.tasten.hidden = true;
        this.aussage.textContent = "";
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
        if (this.tasten) this.leeren();
    }
}
