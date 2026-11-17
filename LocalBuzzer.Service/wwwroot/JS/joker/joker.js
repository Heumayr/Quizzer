// Joker-Runde am Telefon (M35 C7b): wer offene Joker haelt, sieht nur seine eigenen und waehlt einen oder
// keinen; die Sperre fragt danach, wen sie trifft. Gesendet wird genau einmal { jokerId, target } - der Server
// prueft trotzdem alles. Erst nach dem Schliessen und Einloesen kommt, wer welchen Joker eingesetzt hat (V12).

export const KEIN_JOKER = "Keinen Joker";

// Der Spielleiter hat die Wahl nicht eingeloest (zweiter Riegel) - das Telefon nimmt sie zurueck (V12).
export const ABGELEHNT = "Dein Joker wurde nicht eingelöst – er zählt für diese Frage nicht.";

// Die eigene Wahl als Satz - nie nur Farbe.
export function eigeneWahlText(artInfo) {
    if (artInfo?.ownRejected) return ABGELEHNT;

    const id = artInfo?.ownJokerId;
    if (id == null) return "Kein Joker eingesetzt.";

    const name = (artInfo?.options ?? []).find(option => option.id === id)?.name ?? "Joker";
    const gegen = artInfo?.ownTarget == null ? null : (artInfo?.targets ?? []).find(ziel => ziel.id === artInfo.ownTarget)?.name ?? "?";
    return gegen ? `Dein Joker: ${name} gegen ${gegen}` : `Dein Joker: ${name}`;
}

// Eine Zeile je eingesetztem Joker, nach dem Schliessen fuer alle gleich.
export function ergebnisZeilen(artInfo) {
    return (artInfo?.results ?? []).map(eintrag =>
        eintrag.target ? `${eintrag.player}: ${eintrag.joker} gegen ${eintrag.target}` : `${eintrag.player}: ${eintrag.joker}`);
}

export class JokerModul {
    constructor() {
        this.locked = true;
        this.gesendet = false;
        this.inhalt = null;
        this.text = null;
        this.tasten = [];
        this.amEnde = false;
    }

    mount(feld) {
        const inhalt = document.createElement("div");
        inhalt.className = "joker-inhalt";

        const text = document.createElement("p");
        text.className = "joker-text";
        text.setAttribute("aria-live", "polite");

        feld.append(inhalt, text);
        Object.assign(this, { inhalt, text });
        this.leeren();
    }

    starte(artInfo, senden) {
        this.gesendet = false;
        this.zeigeJoker(artInfo, senden);
    }

    // Die eigenen Joker und „Keinen Joker" als Tasten.
    zeigeJoker(artInfo, senden) {
        this.leereTasten();
        this.text.textContent = "Setzt du für diese Frage einen Joker ein?";

        for (const option of artInfo?.options ?? []) {
            const taste = this.taste(option.name, option.effect);
            taste.className = "joker-knopf";
            taste.addEventListener("click", () => option.needsTarget
                ? this.zeigeZiele(artInfo, option, senden)
                : this.sende(senden, { jokerId: option.id, target: null }, `Dein Joker: ${option.name}`));
        }

        const keiner = this.taste(KEIN_JOKER, "");
        keiner.className = "joker-keiner";
        keiner.addEventListener("click", () => this.sende(senden, { jokerId: null, target: null }, "Kein Joker eingesetzt."));
    }

    // Die Sperre: wen sie trifft - nie sich selbst, das schickt der Server gar nicht erst.
    zeigeZiele(artInfo, option, senden) {
        if (this.locked || this.gesendet) return;

        this.leereTasten();
        this.text.textContent = `${option.name}: Wen sperrst du für diese Frage?`;

        for (const ziel of artInfo?.targets ?? []) {
            const taste = this.taste(ziel.name, "");
            taste.className = "joker-ziel";
            taste.addEventListener("click", () =>
                this.sende(senden, { jokerId: option.id, target: ziel.id }, `Dein Joker: ${option.name} gegen ${ziel.name}`));
        }

        const abbruch = this.taste("Zurück", "");
        abbruch.className = "joker-abbruch";
        abbruch.addEventListener("click", () => this.zeigeJoker(artInfo, senden));
    }

    taste(name, wirkung) {
        const taste = document.createElement("button");
        taste.type = "button";
        taste.textContent = name;
        taste.disabled = this.locked;

        if (wirkung) {
            const satz = document.createElement("span");
            satz.className = "joker-wirkung";
            satz.textContent = wirkung;
            taste.appendChild(satz);
        }

        this.inhalt.appendChild(taste);
        this.tasten.push(taste);
        return taste;
    }

    async sende(senden, nutzlast, satz) {
        if (this.locked || this.gesendet) return;

        // Erst sperren, dann senden - ein zweiter Tipp darf nichts mehr schicken.
        this.gesendet = true;
        this.tasten.forEach(t => { t.disabled = true; });
        this.text.textContent = `${satz} – warte auf die anderen.`;

        try {
            await senden(nutzlast);
        } catch {
            // Gemeldet hat es index.js; die Taste geht wieder auf.
            this.gesendet = false;
            this.tasten.forEach(t => { t.disabled = this.locked; });
        }
    }

    zeigeErgebnis(artInfo) {
        this.leereTasten();
        this.text.textContent = eigeneWahlText(artInfo);
    }

    zeigeEnde(artInfo) {
        this.leereTasten();
        this.amEnde = true;

        // Bis der Spielleiter eingeloest hat, steht nichts fest - eine abgelehnte Wahl erscheint so nirgends (V12).
        if (!artInfo?.confirmed) {
            this.text.textContent = "Die Joker werden eingelöst …";
            return;
        }

        if (artInfo.ownRejected) {
            const abgelehnt = document.createElement("p");
            abgelehnt.className = "joker-abgelehnt";
            abgelehnt.textContent = ABGELEHNT;
            this.inhalt.appendChild(abgelehnt);
        }

        const zeilen = ergebnisZeilen(artInfo);

        if (artInfo.blockedBy) {
            const gesperrt = document.createElement("p");
            gesperrt.className = "joker-gesperrt";
            gesperrt.textContent = `Du bist für diese Frage gesperrt – Joker von ${artInfo.blockedBy}.`;
            this.inhalt.appendChild(gesperrt);
        }

        for (const zeile of zeilen) {
            const absatz = document.createElement("p");
            absatz.className = "joker-ergebnis";
            absatz.textContent = zeile;
            this.inhalt.appendChild(absatz);
        }

        this.text.textContent = zeilen.length > 0 ? "Diese Joker sind im Spiel:" : "Niemand hat einen Joker eingesetzt.";
    }

    // Gleicher Rahmenstand, neuer Inhalt - die Einloesung des Spielleiters kommt so an (V12). Mitten in der Wahl nie.
    aktualisiere(artInfo) {
        if (this.amEnde) this.zeigeEnde(artInfo);
    }

    leereTasten() {
        if (this.inhalt) this.inhalt.innerHTML = "";
        this.tasten = [];
        this.amEnde = false;
    }

    leeren() {
        this.leereTasten();
        if (this.text) this.text.textContent = "Gleich geht es los";
    }

    setLocked(locked) {
        this.locked = !!locked;
        this.tasten.forEach(t => { t.disabled = this.locked || this.gesendet; });
    }

    dispose() {
        this.leeren();
    }
}
