// Sonderregel Finale (M37 E4) am Telefon: ein geheimer Einsatz von 0 bis zur Obergrenze, genau einmal.
// Dasselbe Modul wie der Einsatz, nur andere Texte - wer nichts setzt, setzt 0; die Grenze prueft der Server noch einmal.

import { EinsatzModul } from "./einsatz.js";

export class FinaleModul extends EinsatzModul {
    tastenText() { return "Einsatz setzen"; }

    eingabeName() { return "Einsatz fürs Finale in Punkten"; }

    kopfText(max) { return `Finale: setze geheim 0 bis ${max} Punkte – niemand sonst sieht deinen Einsatz.`; }

    ergebnisText(wert) {
        return wert != null ? `Dein Einsatz fürs Finale: ${wert}` : "Kein Einsatz – du setzt 0.";
    }
}
