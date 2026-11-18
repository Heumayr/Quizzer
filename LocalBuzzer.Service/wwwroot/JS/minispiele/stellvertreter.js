import { DuelDisciplines } from "../constants.js";
import { TEAM_FARBEN } from "../team.js";
import { ReaktionModul } from "./reaktion.js";
import { ZUEGE, zugName } from "./duell.js";

// Minispiel Stellvertreter-Duell (M37 T10): erst waehlt jedes Team am Telefon seinen Stellvertreter - ein Mitglied oder das
// Los -, dann spielen nur die Gewaehlten: Reaktion (Modul aus B3) oder bei genau zwei Teams Schere-Stein-Papier. Gelost wird
// am Server; das Telefon schickt nur "Los", nie eine Zahl. Namen kommen nur als Text hinein, die Teamfarbe nur als Klasse
// team-farbe-N (styles/team.css), nie als Farbwert aus der Nachricht und nie allein.

// „✚ Rot: Anna (gelost)“ bzw. „✚ Rot: wählt …“ - Symbol und Name tragen, die Farbe nie allein.
export function teamText(eintrag) {
    const name = `${eintrag?.symbol ?? ""} ${eintrag?.name ?? ""}`.trim();
    if (!eintrag?.proxyName) return `${name}: wählt …`;
    return `${name}: ${eintrag.proxyName}${eintrag.byLot ? " (gelost)" : ""}`;
}

// „Gewonnen: ✚ Rot.“ - die Siegerteams, wie der Server sie markiert.
export function siegerText(teams) {
    const sieger = (teams ?? []).filter(eintrag => eintrag.isWinner).map(eintrag => `${eintrag.symbol ?? ""} ${eintrag.name ?? ""}`.trim());
    return sieger.length > 0 ? `Gewonnen: ${sieger.join(", ")}.` : "Das Duell ist vorbei.";
}

export class StellvertreterModul {
    constructor(optionen = {}) {
        this.optionen = optionen;
        this.reaktion = null;
        this.locked = true;
        this.gesendet = false;
        this.tasten = [];
        this.liste = null;
        this.inhalt = null;
        this.text = null;
    }

    mount(feld) {
        const liste = document.createElement("ul");
        liste.className = "stellvertreter-teams";

        const inhalt = document.createElement("div");
        inhalt.className = "stellvertreter-inhalt";

        const text = document.createElement("p");
        text.className = "stellvertreter-text";
        text.setAttribute("aria-live", "polite");

        feld.append(liste, inhalt, text);
        Object.assign(this, { liste, inhalt, text });
        this.leeren();
    }

    // Die Runde ist offen: erst die Wahl fuers Team, dann - nur als Stellvertreter - Zug oder Reaktion.
    starte(artInfo, senden) {
        this.raeume();
        this.zeigeTeams(artInfo?.teams);

        if (artInfo?.choosesProxy) {
            this.zeigeWahl(artInfo.options, senden);
            return;
        }

        if (!artInfo?.isProxy) {
            this.text.textContent = "Schaut auf den Beamer – die Stellvertreter spielen.";
            return;
        }

        if (artInfo.discipline === DuelDisciplines.Reaction) {
            this.text.textContent = "Du spielst für dein Team!";
            this.reaktionsModul().starte(artInfo.reaction, senden);
            return;
        }

        this.zugTasten(senden);
        this.text.textContent = "Du spielst für dein Team: Schere, Stein oder Papier – geheim.";
    }

    // Die Mitglieder des eigenen Teams und das Los; ein Tipp schickt genau eine Wahl.
    zeigeWahl(optionen, senden) {
        this.gesendet = false;
        this.text.textContent = "Wählt euren Stellvertreter – oder lasst das Los entscheiden.";

        for (const spieler of optionen ?? []) {
            const taste = document.createElement("button");
            taste.type = "button";
            taste.className = "stellvertreter-taste";
            taste.textContent = spieler.name ?? "?";
            taste.addEventListener("click", () => this.waehle({ chosen: spieler.id, lot: false }, taste, senden));
            this.inhalt.appendChild(taste);
            this.tasten.push(taste);
        }

        const los = document.createElement("button");
        los.type = "button";
        los.className = "stellvertreter-taste stellvertreter-los";
        los.textContent = "🎲 Losen lassen";
        los.addEventListener("click", () => this.waehle({ chosen: null, lot: true }, los, senden));
        this.inhalt.appendChild(los);
        this.tasten.push(los);
    }

    async waehle(wahl, taste, senden) {
        if (this.locked || this.gesendet) return;

        // Erst sperren, dann senden - ein zweiter Tipp darf nichts mehr schicken. Nie nur Farbe: der Text sagt die Wahl.
        this.gesendet = true;
        this.tasten.forEach(t => { t.disabled = true; });
        taste.className = "stellvertreter-taste stellvertreter-aktiv";
        this.text.textContent = wahl.lot
            ? "Das Los entscheidet – gleich steht fest, wer spielt."
            : `Gewählt: ${taste.textContent} – gleich geht es los.`;

        try {
            await senden(wahl);
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    zugTasten(senden) {
        this.gesendet = false;

        for (const z of ZUEGE) {
            const taste = document.createElement("button");
            taste.type = "button";
            taste.className = "duell-zug";
            taste.textContent = `${z.symbol} ${z.name}`;
            taste.addEventListener("click", () => this.ziehe(z, taste, senden));
            this.inhalt.appendChild(taste);
            this.tasten.push(taste);
        }
    }

    async ziehe(z, taste, senden) {
        if (this.locked || this.gesendet) return;

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

    // Ein anderes Team hat gewaehlt - nur die Liste wechselt, die eigenen Tasten bleiben.
    aktualisiere(artInfo) {
        this.zeigeTeams(artInfo?.teams);
    }

    // Abgegeben (auch nach dem Wiederverbinden) oder Runde vorbei: der eigene Zug, nach der Runde auch der fremde.
    zeigeErgebnis(artInfo) {
        this.raeume();
        this.zeigeTeams(artInfo?.teams);

        if (!artInfo?.isProxy) {
            this.text.textContent = "Schaut auf den Beamer – dort steht die Runde.";
            return;
        }

        if (artInfo.discipline === DuelDisciplines.Reaction) {
            this.text.textContent = "";
            this.reaktionsModul().zeigeErgebnis(artInfo.reaction);
            return;
        }

        const eigener = zugName(artInfo.ownMove);

        this.text.textContent = artInfo.otherMove == null
            ? `Du: ${eigener} – warte auf die Auflösung.`
            : `Du: ${eigener} · Gegner: ${zugName(artInfo.otherMove)} · Stand ${artInfo.ownWins}:${artInfo.otherWins}`;
    }

    zeigeEnde(artInfo) {
        this.raeume();
        this.zeigeTeams(artInfo?.teams);

        if (artInfo?.isWinner) {
            this.text.textContent = "🏆 Euer Team gewinnt das Duell!";
        } else if (artInfo?.isDraw) {
            this.text.textContent = "Unentschieden – keine Punkte.";
        } else {
            this.text.textContent = siegerText(artInfo?.teams);
        }
    }

    // Je Team eine Zeile mit seinem Stellvertreter; das eigene markiert mit Text, die Farbe nur als Klasse der Palette.
    zeigeTeams(teams) {
        if (!this.liste) return;
        this.liste.innerHTML = "";

        for (const eintrag of teams ?? []) {
            const zeile = document.createElement("li");
            zeile.className = "stellvertreter-team";
            zeile.textContent = teamText(eintrag) + (eintrag.isOwn ? " · euer Team" : "") + (eintrag.isWinner ? " · ★ Sieger" : "");

            if (Number.isInteger(eintrag.colorIndex) && eintrag.colorIndex >= 0 && eintrag.colorIndex < TEAM_FARBEN) {
                zeile.classList.add(`team-farbe-${eintrag.colorIndex}`);
            }

            this.liste.appendChild(zeile);
        }
    }

    leeren() {
        this.raeume();
        if (this.liste) this.liste.innerHTML = "";
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
        this.reaktion?.setLocked(this.locked);
    }

    dispose() {
        this.raeume();
    }
}
