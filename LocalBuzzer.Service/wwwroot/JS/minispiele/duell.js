import { DuelDisciplines, SchereSteinPapier } from "../constants.js";
import { ReaktionModul } from "./reaktion.js";
import { Spielerwahl } from "./spielerwahl.js";

// Minispiel Duell (M35 C2): der Ausloeser waehlt seinen Gegner, dann spielen nur die beiden.
// Schere-Stein-Papier ist geheim - den fremden Zug schickt der Server erst mit dem Ende der Runde.
// Die Disziplin Reaktion nimmt das Modul aus B3, gemessen wird dort am Telefon.

export const ZUEGE = Object.freeze([
    { zug: SchereSteinPapier.Schere, name: "Schere", symbol: "✌" },
    { zug: SchereSteinPapier.Stein, name: "Stein", symbol: "✊" },
    { zug: SchereSteinPapier.Papier, name: "Papier", symbol: "✋" }
]);

export function zugName(zug) {
    return ZUEGE.find(z => z.zug === zug)?.name ?? "nichts";
}

function paarText(artInfo) {
    const herausforderer = artInfo?.challengerName;
    const gegner = artInfo?.opponentName;

    if (herausforderer && gegner) return `Duell: ${herausforderer} gegen ${gegner}`;
    if (herausforderer) return `Duell: ${herausforderer} wählt einen Gegner`;
    return "Duell";
}

export class DuellModul {
    constructor(optionen = {}) {
        this.optionen = optionen;
        this.wahl = new Spielerwahl();
        this.reaktion = null;
        this.locked = true;
        this.gesendet = false;
        this.tasten = [];
        this.kopf = null;
        this.inhalt = null;
        this.text = null;
    }

    mount(feld) {
        const kopf = document.createElement("p");
        kopf.className = "duell-kopf";

        const inhalt = document.createElement("div");
        inhalt.className = "duell-inhalt";

        const text = document.createElement("p");
        text.className = "duell-text";
        text.setAttribute("aria-live", "polite");

        feld.append(kopf, inhalt, text);
        Object.assign(this, { kopf, inhalt, text });
        this.leeren();
    }

    // Die Runde ist offen: erst die Wahl des Gegners, dann der Zug bzw. die Reaktion.
    starte(artInfo, senden) {
        this.raeume();
        this.kopf.textContent = paarText(artInfo);

        if (artInfo?.choosesOpponent) {
            this.text.textContent = "";
            this.wahl.zeige(this.inhalt, "Wähle deinen Gegner:", artInfo.options, senden);
            return;
        }

        if (artInfo?.discipline === DuelDisciplines.Reaction) {
            this.text.textContent = "";
            this.reaktionsModul().starte(artInfo.reaction, senden);
            return;
        }

        this.gesendet = false;
        this.tasten = ZUEGE.map(z => this.zugTaste(z, senden));
        this.text.textContent = "Wähle geheim: Schere, Stein oder Papier.";
    }

    zugTaste(z, senden) {
        const taste = document.createElement("button");
        taste.type = "button";
        taste.className = "duell-zug";
        taste.textContent = `${z.symbol} ${z.name}`;
        taste.addEventListener("click", () => this.ziehe(z, taste, senden));
        this.inhalt.appendChild(taste);
        return taste;
    }

    async ziehe(z, taste, senden) {
        if (this.locked || this.gesendet) return;

        // Erst sperren, dann senden - ein zweiter Tipp darf nichts mehr schicken.
        this.gesendet = true;
        this.tasten.forEach(t => { t.disabled = true; });
        taste.className = "duell-zug duell-zug-aktiv";
        this.text.textContent = `Du hast ${z.name} gewählt – warte auf die Auflösung.`;

        try {
            await senden({ move: z.zug });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    // Abgegeben (auch nach dem Wiederverbinden) oder Runde vorbei: der eigene Zug, nach dem Ende der Runde auch der fremde.
    zeigeErgebnis(artInfo) {
        this.raeume();
        this.kopf.textContent = paarText(artInfo);

        if (!artInfo?.isDuellist) {
            this.text.textContent = "Schaut auf den Beamer – dort steht die Runde.";
            return;
        }

        if (artInfo.discipline === DuelDisciplines.Reaction) {
            this.text.textContent = "";
            this.reaktionsModul().zeigeErgebnis(artInfo.reaction);
            return;
        }

        const eigener = zugName(artInfo.ownMove);
        const stand = `Stand ${artInfo.ownWins}:${artInfo.otherWins}`;

        this.text.textContent = artInfo.otherMove == null
            ? `Du: ${eigener} – warte auf die Auflösung.`
            : `Du: ${eigener} · Gegner: ${zugName(artInfo.otherMove)} · ${stand}`;
    }

    zeigeEnde(artInfo) {
        this.raeume();
        this.kopf.textContent = paarText(artInfo);

        if (artInfo?.isWinner) {
            this.text.textContent = `🏆 Gewonnen! (${artInfo.ownWins}:${artInfo.otherWins})`;
        } else if (artInfo?.isDraw) {
            this.text.textContent = "Unentschieden – keine Punkte.";
        } else if (artInfo?.winnerName) {
            this.text.textContent = `Gewonnen hat ${artInfo.winnerName}.`;
        } else {
            this.text.textContent = "Das Duell ist vorbei.";
        }
    }

    leeren() {
        this.raeume();
        if (this.kopf) this.kopf.textContent = "";
        if (this.text) this.text.textContent = "Gleich geht es los";
    }

    // Leert das Feld; eine laufende Reaktion haelt ihre Uhren an.
    raeume() {
        this.reaktion?.dispose();
        this.reaktion = null;
        this.tasten = [];
        if (this.inhalt) this.inhalt.innerHTML = "";
    }

    reaktionsModul() {
        this.reaktion = new ReaktionModul(this.optionen);
        this.reaktion.mount(this.inhalt);
        this.reaktion.setLocked(this.locked);
        return this.reaktion;
    }

    setLocked(locked) {
        this.locked = !!locked;
        this.wahl.setLocked(this.locked);
        this.reaktion?.setLocked(this.locked);
    }

    dispose() {
        this.raeume();
    }
}
