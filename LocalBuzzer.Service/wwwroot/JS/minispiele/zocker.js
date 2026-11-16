// Minispiel Zocker (M35 C4): ein Knopf „Aussteigen". Den steigenden Multiplikator zeigt der Beamer;
// wann es abstuerzt, weiss nur der Server - das Telefon erfaehrt den Absturzpunkt erst nach der Runde.
// Ob ein Ausstieg vor dem Absturz kam, entscheidet die Serveruhr beim Eintreffen, nicht das Telefon.

// 185 -> "×1,85" - dieselbe Schreibweise wie am Beamer (ZockerEvaluator.Text).
export function multiplikatorText(hundertstel) {
    return "×" + (hundertstel / 100).toFixed(2).replace(".", ",");
}

// Der Ausgang aus Sicht dieses Telefons, sobald der Server den Absturzpunkt geschickt hat.
export function ausgang(artInfo) {
    const eigen = artInfo?.ownMultiplier;
    const absturz = artInfo?.crashMultiplier;

    if (absturz == null) {
        return eigen != null
            ? `✓ Bei ${multiplikatorText(eigen)} ausgestiegen.`
            : "Kein Ausstieg – der Spielleiter wertet.";
    }

    const wo = multiplikatorText(absturz);

    if (eigen != null) {
        return artInfo?.crashed
            ? `✓ Bei ${multiplikatorText(eigen)} ausgestiegen – abgestürzt ist es bei ${wo}.`
            : `✓ Bei ${multiplikatorText(eigen)} ausgestiegen – abgestürzt wäre es bei ${wo}.`;
    }

    return `💥 Abgestürzt bei ${wo} – du warst noch drin.`;
}

export class ZockerModul {
    constructor() {
        this.locked = true;
        this.gesendet = false;
        this.senden = null;
        this.taste = null;
        this.text = null;
    }

    mount(feld) {
        const taste = document.createElement("button");
        taste.type = "button";
        taste.className = "zocker-taste";
        taste.textContent = "Aussteigen!";
        taste.addEventListener("click", () => this.steigeAus());

        const text = document.createElement("p");
        text.className = "zocker-text";
        text.setAttribute("aria-live", "polite");

        feld.append(taste, text);
        Object.assign(this, { taste, text });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.senden = senden;
        this.gesendet = false;
        this.taste.hidden = false;
        this.taste.disabled = false;
        this.text.textContent = "Der Multiplikator steigt – schau auf den Beamer und steig aus, bevor es abstürzt!";
    }

    async steigeAus() {
        if (this.locked || this.gesendet || !this.senden) return;

        // Erst sperren, dann senden - ein zweiter Tipp darf nichts mehr schicken. Der Satz sagt den Stand.
        this.gesendet = true;
        this.taste.disabled = true;
        this.text.textContent = "Ausstieg gesendet – er zählt, wenn er vor dem Absturz ankommt.";

        try {
            await this.senden({ cashOut: true });
        } catch {
            // Gemeldet hat es index.js.
        }
    }

    zeigeErgebnis(artInfo) {
        this.taste.hidden = true;
        this.text.textContent = artInfo?.ownMultiplier != null
            ? `✓ Bei ${multiplikatorText(artInfo.ownMultiplier)} ausgestiegen – warte auf den Absturz.`
            : "Warte auf den Absturz.";
    }

    zeigeEnde(artInfo) {
        this.taste.hidden = true;
        this.text.textContent = ausgang(artInfo);
    }

    leeren() {
        this.senden = null;
        if (this.taste) this.taste.hidden = true;
        if (this.text) this.text.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
    }

    dispose() {
        this.leeren();
    }
}
