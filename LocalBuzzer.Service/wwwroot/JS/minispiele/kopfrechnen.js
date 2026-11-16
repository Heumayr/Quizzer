// Minispiel Kopfrechnen (M35 C5): eine Aufgabe, ein Ziffernblock, senden. Die Loesung kennt nur der
// Server - er prueft jede Antwort und schickt die naechste Aufgabe fuer dieses Telefon zurueck. Der
// Ziffernblock ist eigen statt der Telefontastatur: die schiebt sich ueber die Aufgabe und hat auf
// manchen Telefonen keine Eingabetaste.

// Nicht wartend bleiben, wenn die Antwort des Servers ausbleibt - dann darf man erneut senden.
const WARTEZEIT_MS = 3000;
const TAKT_MS = 250;
const MAX_ZIFFERN = 5;

export function restText(ms) {
    return `Noch ${Math.max(0, Math.ceil(ms / 1000))} s`;
}

export function richtigText(n) {
    return `${n} richtig`;
}

// Der Satz am Ende, aus Sicht dieses Telefons.
export function endeText(artInfo) {
    const eigen = `${artInfo?.ownCorrect ?? 0} von ${artInfo?.ownAnswered ?? 0} richtig.`;
    const namen = artInfo?.winnerNames ?? [];

    if (artInfo?.isWinner) return `🏆 Gewonnen! ${eigen}`;
    if (namen.length === 0) return `${eigen} Niemand hat eine richtig.`;
    return `${eigen} Gewonnen hat ${namen.join(", ")} mit ${artInfo?.winnerCorrect ?? 0} richtigen.`;
}

export class KopfrechnenModul {
    constructor({ timer, now } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.now = now ?? (() => performance.now());
        this.senden = null;
        this.locked = true;
        this.wartet = false;
        this.nummer = 0;
        this.ziffern = "";
        this.ende = 0;
        this.uhr = null;
        this.warteUhr = null;
        this.tasten = [];
    }

    mount(feld) {
        const inhalt = document.createElement("div");
        inhalt.className = "kopfrechnen-inhalt";

        const stand = document.createElement("p");
        stand.className = "kopfrechnen-stand";

        const aufgabe = document.createElement("p");
        aufgabe.className = "kopfrechnen-aufgabe";

        const eingabe = document.createElement("p");
        eingabe.className = "kopfrechnen-eingabe";
        eingabe.setAttribute("aria-live", "polite");

        const hinweis = document.createElement("p");
        hinweis.className = "kopfrechnen-hinweis";
        hinweis.setAttribute("aria-live", "polite");

        const block = document.createElement("div");
        block.className = "kopfrechnen-tasten";

        for (const ziffer of ["1", "2", "3", "4", "5", "6", "7", "8", "9"]) block.append(this.taste(ziffer, ziffer, () => this.tippe(ziffer)));
        block.append(
            this.taste("⌫", "Letzte Ziffer löschen", () => this.loesche()),
            this.taste("0", "0", () => this.tippe("0")),
            this.taste("✓", "Antwort senden", () => this.sende(), "kopfrechnen-taste kopfrechnen-senden"));

        inhalt.append(stand, aufgabe, eingabe, hinweis, block);
        feld.appendChild(inhalt);
        Object.assign(this, { inhalt, stand, aufgabe, eingabe, hinweis, block });
        this.leeren();
    }

    taste(text, beschriftung, aktion, klasse = "kopfrechnen-taste") {
        const knopf = document.createElement("button");
        knopf.type = "button";
        knopf.className = klasse;
        knopf.textContent = text;
        knopf.setAttribute("aria-label", beschriftung);
        knopf.addEventListener("click", aktion);
        this.tasten.push(knopf);
        return knopf;
    }

    starte(artInfo, senden) {
        this.senden = senden;
        this.nummer = 0;
        this.block.hidden = false;
        this.uebernimm(artInfo);
        this.ticke();
    }

    // Die Antwort des Servers auf eine Abgabe: naechste Aufgabe oder (verworfen) dieselbe.
    aktualisiere(artInfo) {
        if (!this.senden) return;
        this.uebernimm(artInfo);
    }

    uebernimm(artInfo) {
        const nummer = artInfo?.taskNumber ?? 0;
        if (nummer !== this.nummer) {
            this.nummer = nummer;
            this.ziffern = "";
        }

        this.warte(false);
        this.ende = this.now() + (artInfo?.remainingMs ?? (artInfo?.durationSeconds ?? 0) * 1000);
        this.aufgabe.textContent = artInfo?.taskText ? `${artInfo.taskText} = ?` : "";
        this.richtig = artInfo?.ownCorrect ?? 0;

        // Nie allein ueber Farbe: Zeichen und Wort.
        if (artInfo?.lastCorrect === true) this.hinweis.textContent = "✓ Richtig!";
        else if (artInfo?.lastCorrect === false) this.hinweis.textContent = "✗ Falsch – weiter geht's.";
        else this.hinweis.textContent = "Tippe das Ergebnis und dann ✓.";

        this.zeigeEingabe();
        this.zeigeStand();
    }

    tippe(ziffer) {
        if (!this.bereit() || this.ziffern.length >= MAX_ZIFFERN) return;
        this.ziffern = this.ziffern === "0" ? ziffer : this.ziffern + ziffer;
        this.zeigeEingabe();
    }

    loesche() {
        if (!this.bereit()) return;
        this.ziffern = this.ziffern.slice(0, -1);
        this.zeigeEingabe();
    }

    async sende() {
        if (!this.bereit() || this.ziffern === "") return;

        // Erst sperren, dann senden - ein zweiter Tipp darf dieselbe Aufgabe nicht noch einmal schicken.
        this.warte(true);
        try {
            await this.senden({ taskNumber: this.nummer, answer: Number(this.ziffern) });
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
            this.aufgabe.textContent = "Die Zeit ist um.";
            return;
        }
        this.uhr = this.timer.setTimeout(() => this.ticke(), TAKT_MS);
    }

    zeigeEingabe() {
        this.eingabe.textContent = this.ziffern === "" ? "…" : this.ziffern;
    }

    zeigeStand() {
        this.stand.textContent = `${richtigText(this.richtig ?? 0)} · ${restText(this.rest())}`;
    }

    zeigeErgebnis(artInfo) {
        this.halte();
        this.stand.textContent = richtigText(artInfo?.ownCorrect ?? 0);
    }

    zeigeEnde(artInfo) {
        this.halte();
        this.aufgabe.textContent = "";
        this.hinweis.textContent = endeText(artInfo);
        this.stand.textContent = richtigText(artInfo?.ownCorrect ?? 0);
    }

    halte() {
        this.senden = null;
        this.warte(false);
        if (this.uhr != null) this.timer.clearTimeout(this.uhr);
        this.uhr = null;
        this.block.hidden = true;
        this.eingabe.textContent = "";
    }

    leeren() {
        this.halte();
        this.stand.textContent = "";
        this.aufgabe.textContent = "";
        this.hinweis.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    dispose() {
        if (this.block) this.leeren();
    }
}
