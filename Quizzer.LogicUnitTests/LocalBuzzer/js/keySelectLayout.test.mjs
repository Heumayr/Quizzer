// Die erste automatische Pruefung der Telefonseite.
//
// Gemessen 2026-09-07: nach "Runde zuruecksetzen" blieb der Bestaetigen-Knopf eines
// Spielers, der schon abgegeben hatte, FUER IMMER gesperrt - er trug weiter
// "Abgegeben". Die Ursache: das Layout baut sich nur neu auf, wenn sich seine Kennung
// aendert, und die enthielt den Ruecksetzstand nicht.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { KeySelectLayout } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/layouts/keySelectLayout.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;

    console.error("ROT: " + satz);
    fehler++;
}

function zustand(resetCount, gesperrt = false) {
    return {
        playerId: "11111111-1111-1111-1111-111111111111",
        resetCount,
        currentLayoutLocked: gesperrt,
        allLocked: false,
        layoutInfo: {
            questionId: "22222222-2222-2222-2222-222222222222",
            keysAndDesignations: { A: "Klavier", B: "Geige" },
            showDesignations: true,
            maxAllowedSelections: 1
        }
    };
}

const layout = new KeySelectLayout({ submitSelection: async () => {} });

layout.render(wurzel, zustand(1));

const tasten = () => wurzel.querySelectorAll(".key-btn");
const knopf = () => wurzel.querySelector(".commit-btn");

// --- Abgeben ---
tasten()[0].click();
pruefe(knopf().disabled === false, "Nach der Auswahl ist der Bestaetigen-Knopf gesperrt.");

await layout.handleCommitClick();

pruefe(knopf().disabled === true, "Nach der Abgabe ist der Knopf nicht gesperrt.");
pruefe(knopf().textContent === "Abgegeben ✓",
    "Nach der Abgabe steht nicht 'Abgegeben' auf dem Knopf: " + knopf().textContent);

// --- Die Gegenrichtung zuerst: derselbe Stand darf NICHT entsperren ---
layout.update(zustand(1));

pruefe(knopf().disabled === true,
    "Ohne Zuruecksetzen wurde wieder entsperrt - dann kann derselbe Spieler zweimal abgeben.");

// --- Zuruecksetzen ---
layout.update(zustand(2));

pruefe(knopf().textContent === "Auswahl bestätigen",
    "Nach dem Zuruecksetzen steht weiter 'Abgegeben' auf dem Knopf: " + knopf().textContent);

pruefe(layout.submitted === false,
    "Nach dem Zuruecksetzen gilt der Spieler weiter als abgegeben.");

pruefe(tasten().every(t => t.disabled === false),
    "Nach dem Zuruecksetzen sind die Antworttasten gesperrt.");

tasten()[1].click();

pruefe(knopf().disabled === false,
    "Nach dem Zuruecksetzen laesst sich nicht erneut abgeben - der Spieler ist fuer diese "
    + "Frage aus dem Spiel, und die Runde kann nie mehr schliessen.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("keySelectLayout: alle Zusicherungen gruen.");
