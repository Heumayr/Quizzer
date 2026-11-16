// Sonderregel Auktion (M35 C3b) am Telefon: ein geheimes Gebot von 0 bis zur Obergrenze, genau einmal.
// Dasselbe Modul wie der Einsatz, nur andere Texte; die Grenze prueft der Server noch einmal.

import { EinsatzModul } from "./einsatz.js";

export class AuktionModul extends EinsatzModul {
    tastenText() { return "Bieten"; }

    eingabeName() { return "Gebot in Punkten"; }

    kopfText(max) { return `Biete geheim 0 bis ${max} Punkte – 0 heißt passen.`; }

    ergebnisText(wert) {
        if (wert == null) return "Kein Gebot abgegeben.";
        return wert === 0 ? "Du passt." : `Dein Gebot: ${wert}`;
    }
}
