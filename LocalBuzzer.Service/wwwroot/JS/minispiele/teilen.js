import { Spielerwahl } from "./spielerwahl.js";

// Minispiel Teilen oder Nehmen (M35 C2): der Ausloeser waehlt einen Partner, dann entscheiden beide
// geheim. Die Wahl des anderen schickt der Server erst mit der Aufloesung.

function paarText(artInfo) {
    const waehler = artInfo?.chooserName;
    const partner = artInfo?.partnerName;

    if (waehler && partner) return `Teilen oder Nehmen: ${waehler} und ${partner}`;
    if (waehler) return `Teilen oder Nehmen: ${waehler} wählt einen Partner`;
    return "Teilen oder Nehmen";
}

// Der Ausgang aus Sicht dieses Telefons - dieselben vier Faelle wie TeilenNehmenEvaluator.
export function ausgang(eigene, fremde) {
    if (eigene == null || fremde == null) return "Einer von euch hat nicht gewählt – der Spielleiter wertet.";
    if (!eigene && !fremde) return "Ihr teilt beide – jeder bekommt die Hälfte.";
    if (eigene && !fremde) return "🏆 Du nimmst alles – dein Gegenüber hat geteilt.";
    if (!eigene && fremde) return "Dein Gegenüber nimmt alles – du gehst leer aus.";
    return "Ihr nehmt beide – niemand bekommt etwas.";
}

export class TeilenModul {
    constructor() {
        this.wahl = new Spielerwahl();
        this.locked = true;
        this.gesendet = false;
        this.tasten = [];
        this.kopf = null;
        this.inhalt = null;
        this.text = null;
    }

    mount(feld) {
        const kopf = document.createElement("p");
        kopf.className = "teilen-kopf";

        const inhalt = document.createElement("div");
        inhalt.className = "teilen-inhalt";

        const text = document.createElement("p");
        text.className = "teilen-text";
        text.setAttribute("aria-live", "polite");

        feld.append(kopf, inhalt, text);
        Object.assign(this, { kopf, inhalt, text });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.inhalt.innerHTML = "";
        this.kopf.textContent = paarText(artInfo);

        if (artInfo?.choosesPartner) {
            this.text.textContent = "";
            this.wahl.zeige(this.inhalt, "Wähle deinen Partner:", artInfo.options, senden);
            return;
        }

        this.gesendet = false;
        const teilen = this.taste("🤝 Teilen", false, senden);
        const nehmen = this.taste("💰 Nehmen", true, senden);
        this.tasten = [teilen, nehmen];
        this.text.textContent = "Entscheide geheim: teilen oder nehmen?";
    }

    taste(beschriftung, nimmt, senden) {
        const taste = document.createElement("button");
        taste.type = "button";
        taste.className = "teilen-taste";
        taste.textContent = beschriftung;
        taste.addEventListener("click", () => this.entscheide(taste, nimmt, senden));
        this.inhalt.appendChild(taste);
        return taste;
    }

    async entscheide(taste, nimmt, senden) {
        if (this.locked || this.gesendet) return;

        // Erst sperren, dann senden. Nie nur Farbe: der Satz sagt die Wahl.
        this.gesendet = true;
        this.tasten.forEach(t => { t.disabled = true; });
        taste.className = "teilen-taste teilen-aktiv";
        this.text.textContent = `Du ${nimmt ? "nimmst" : "teilst"} – warte auf die Auflösung.`;

        try {
            await senden({ take: nimmt });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    zeigeErgebnis(artInfo) {
        this.inhalt.innerHTML = "";
        this.kopf.textContent = paarText(artInfo);

        if (!artInfo?.isPlayer) {
            this.text.textContent = "Die beiden entscheiden – schaut auf den Beamer.";
        } else if (artInfo.otherTake != null) {
            this.text.textContent = ausgang(artInfo.ownTake, artInfo.otherTake);
        } else {
            this.text.textContent = artInfo.ownTake == null
                ? "Noch keine Wahl."
                : `Du ${artInfo.ownTake ? "nimmst" : "teilst"} – warte auf die Auflösung.`;
        }
    }

    zeigeEnde(artInfo) {
        this.inhalt.innerHTML = "";
        this.kopf.textContent = paarText(artInfo);
        this.text.textContent = artInfo?.isPlayer
            ? ausgang(artInfo.ownTake, artInfo.otherTake)
            : "Entschieden – schaut auf den Beamer.";
    }

    leeren() {
        this.tasten = [];
        if (this.inhalt) this.inhalt.innerHTML = "";
        if (this.kopf) this.kopf.textContent = "";
        if (this.text) this.text.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
        this.wahl.setLocked(this.locked);
    }

    dispose() {
        this.leeren();
    }
}
