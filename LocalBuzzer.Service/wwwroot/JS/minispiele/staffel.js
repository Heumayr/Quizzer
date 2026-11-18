import { TEAM_FARBEN } from "../team.js";

// Minispiel Staffel (M37 T11): die Mitglieder eines Teams tippen der Reihe nach je einen Teil, ein Fehler gibt ans naechste
// Team weiter. Ein Eingabefeld hat nur der Laeufer; er schickt den Zug mit, den sein Telefon sah - der Server prueft Zug und
// Antwort. Die Loesungen kennt nur der Server, das Telefon bekommt sie erst nach der Runde. Namen nur als Text, die Teamfarbe
// nur als Klasse team-farbe-N (styles/team.css), nie als Farbwert aus der Nachricht und nie allein.

// Nicht wartend bleiben, wenn die Antwort des Servers ausbleibt - dann darf der Laeufer erneut senden.
const WARTEZEIT_MS = 3000;
const MAX_ZEICHEN = 80;

export function teileText(anzahl) {
    return anzahl === 1 ? "1 Teil" : `${anzahl ?? 0} Teile`;
}

// Wer gerade laeuft, aus Sicht dieses Telefons: der Laeufer sieht seinen Teil, die anderen, wer dran ist.
export function standText(artInfo) {
    const teil = `Teil ${artInfo?.part ?? 0} von ${artInfo?.totalParts ?? 0}`;
    if (artInfo?.isRunner) return `Du bist dran: ${teil}`;
    if (artInfo?.runnerName) return `${artInfo.runnerName} ist dran (${artInfo?.runnerTeam ?? "?"}) – ${teil}`;
    return "Die Staffel ist durch.";
}

// Was zuletzt geschah - ohne Antwort und ohne Lösung, die kennt bis zur Auflösung nur der Server.
export function letzterText(artInfo) {
    if (!artInfo?.lastPart) return "";
    const wer = artInfo?.lastTeam || "?";
    if (artInfo?.lastPassed) return `Teil ${artInfo.lastPart}: ${wer} hat weitergegeben.`;
    if (artInfo?.lastAccepted) return `Teil ${artInfo.lastPart}: ✓ ${wer} – vom Spielleiter gewertet.`;
    return artInfo?.lastCorrect ? `Teil ${artInfo.lastPart}: ✓ ${wer}` : `Teil ${artInfo.lastPart}: ✗ ${wer} lag daneben.`;
}

// „✚ Rot: 2 Teile · läuft · euer Team“ - Symbol und Name tragen, die Farbe nie allein.
export function teamZeile(eintrag) {
    const name = `${eintrag?.symbol ?? ""} ${eintrag?.name ?? ""}`.trim();
    return `${name}: ${teileText(eintrag?.solved ?? 0)}`
        + (eintrag?.isOnTurn ? " · läuft" : "")
        + (eintrag?.isOwn ? " · euer Team" : "")
        + (eintrag?.isWinner ? " · ★ Sieger" : "");
}

// „Gewonnen: ✚ Rot.“ - die Siegerteams, wie der Server sie markiert.
export function siegerText(teams) {
    const sieger = (teams ?? []).filter(eintrag => eintrag?.isWinner).map(eintrag => `${eintrag?.symbol ?? ""} ${eintrag?.name ?? ""}`.trim());
    return sieger.length > 0 ? `Gewonnen: ${sieger.join(", ")}.` : "Kein Team liegt vorn – keine Punkte.";
}

// „Teil 3: Erde – ★ Blau“ bzw. „– verfehlt“; erst nach der Runde vom Server.
export function stueckText(stueck) {
    return `Teil ${stueck?.number ?? "?"}: ${stueck?.solution ?? ""} – ${stueck?.team ?? "verfehlt"}`;
}

export class StaffelModul {
    constructor({ timer } = {}) {
        this.timer = timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.senden = null;
        this.locked = true;
        this.wartet = false;
        this.zug = 0;
        this.warteUhr = null;
    }

    mount(feld) {
        const stand = document.createElement("p");
        stand.className = "staffel-stand";
        stand.setAttribute("aria-live", "polite");

        const zeile = document.createElement("div");
        zeile.className = "staffel-zeile";

        const eingabe = document.createElement("input");
        eingabe.className = "staffel-eingabe";
        eingabe.type = "text";
        eingabe.value = "";
        eingabe.setAttribute("aria-label", "Deine Antwort für diesen Teil");
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
        knopf.className = "staffel-senden";
        knopf.textContent = "✓ Abgeben";
        knopf.setAttribute("aria-label", "Antwort für diesen Teil senden");
        knopf.addEventListener("click", () => this.sende());

        const hinweis = document.createElement("p");
        hinweis.className = "staffel-hinweis";
        hinweis.setAttribute("aria-live", "polite");

        const liste = document.createElement("ul");
        liste.className = "staffel-teams";

        const teile = document.createElement("ul");
        teile.className = "staffel-teile";

        zeile.append(eingabe, knopf);
        feld.append(stand, zeile, hinweis, liste, teile);
        Object.assign(this, { stand, zeile, eingabe, knopf, hinweis, liste, teile });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.senden = senden;
        this.zeige(artInfo);
    }

    // Jemand hat geantwortet oder die Spielleitung hat eingegriffen: wer läuft, wechselt für alle.
    aktualisiere(artInfo) {
        if (!this.senden) return;
        this.zeige(artInfo);
    }

    zeige(artInfo) {
        // Ein neuer Zug gibt das Feld frei - derselbe nicht: ein zweiter Tipp darf denselben Teil nicht noch einmal schicken.
        const zug = artInfo?.isRunner ? (artInfo?.turn ?? 0) : 0;
        if (zug !== this.zug) {
            this.zug = zug;
            this.warte(false);
            this.eingabe.value = "";
        }

        this.zeile.hidden = this.zug <= 0;
        this.stand.textContent = standText(artInfo);
        this.hinweis.textContent = letzterText(artInfo) || (this.zug > 0 ? "Tippe deinen Teil und dann ✓." : "");
        this.zeigeTeams(artInfo?.teams);
        this.zeigeTeile([]);
    }

    async sende() {
        const text = String(this.eingabe.value ?? "").trim();
        if (!this.bereit() || text === "") return;

        // Erst sperren, dann senden - der Zug, den dieses Telefon sah, geht mit.
        this.warte(true);
        this.eingabe.value = "";
        try {
            await this.senden({ turn: this.zug, answer: text.slice(0, MAX_ZEICHEN) });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    bereit() {
        return !!this.senden && !this.locked && !this.wartet && this.zug > 0;
    }

    warte(an) {
        this.wartet = an;
        if (this.warteUhr != null) this.timer.clearTimeout(this.warteUhr);
        this.warteUhr = an ? this.timer.setTimeout(() => this.warte(false), WARTEZEIT_MS) : null;
    }

    // Abgegeben (alle Teile durch) oder Runde vorbei: die Teams und - erst jetzt - die Auflösung vom Server.
    zeigeErgebnis(artInfo) {
        this.halte();
        this.stand.textContent = "Die Staffel ist vorbei.";
        this.hinweis.textContent = letzterText(artInfo);
        this.zeigeTeams(artInfo?.teams);
        this.zeigeTeile(artInfo?.resolution);
    }

    zeigeEnde(artInfo) {
        this.zeigeErgebnis(artInfo);
        this.hinweis.textContent = artInfo?.isWinner ? "🏆 Euer Team gewinnt die Staffel!" : siegerText(artInfo?.teams);
    }

    // Je Team eine Zeile; die Farbe nur als Klasse der Palette, was außerhalb liegt, bekommt keine.
    zeigeTeams(teams) {
        if (!this.liste) return;
        this.liste.innerHTML = "";

        for (const eintrag of teams ?? []) {
            const zeile = document.createElement("li");
            zeile.className = "staffel-team";
            zeile.textContent = teamZeile(eintrag);

            if (Number.isInteger(eintrag?.colorIndex) && eintrag.colorIndex >= 0 && eintrag.colorIndex < TEAM_FARBEN) {
                zeile.classList.add(`team-farbe-${eintrag.colorIndex}`);
            }

            this.liste.appendChild(zeile);
        }
    }

    zeigeTeile(liste) {
        if (!this.teile) return;
        this.teile.innerHTML = "";

        for (const stueck of liste ?? []) {
            const zeile = document.createElement("li");
            zeile.className = stueck?.team ? "staffel-teil" : "staffel-teil staffel-verfehlt";
            zeile.textContent = stueckText(stueck);
            this.teile.appendChild(zeile);
        }
    }

    halte() {
        this.senden = null;
        this.warte(false);
        this.zug = 0;
        if (this.zeile) this.zeile.hidden = true;
        if (this.eingabe) this.eingabe.value = "";
    }

    leeren() {
        this.halte();
        if (this.stand) this.stand.textContent = "";
        if (this.hinweis) this.hinweis.textContent = "Gleich geht es los";
        if (this.liste) this.liste.innerHTML = "";
        if (this.teile) this.teile.innerHTML = "";
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    dispose() {
        this.leeren();
    }
}
