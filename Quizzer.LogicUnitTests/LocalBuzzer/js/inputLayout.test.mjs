// Das Eingabe-Layout der Telefonseite - die Schaetzfrage.
//
// Gemessen 2026-09-07: nach "Runde zuruecksetzen" stand weiter "Abgegeben: 42" im Feld,
// obwohl die Abgabe serverseitig geloescht war. Die Ursache war dieselbe wie bei der
// Tastenwahl - die Layout-Kennung kannte den Ruecksetzstand nicht.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { InputLayout } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/layouts/inputLayout.js");

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
            inputType: "text",
            placeholder: "Wie hoch?"
        }
    };
}

const layout = new InputLayout({ submitInput: async () => {} });

layout.render(wurzel, zustand(1));

const feld = () => wurzel.querySelector(".input-field");
const knopf = () => wurzel.querySelector(".commit-btn");
const zeile = () => wurzel.querySelector(".input-submitted");

pruefe(feld().disabled === false, "Das Eingabefeld ist gesperrt, obwohl die Runde offen ist.");

// --- Abgeben ---
feld().value = "3798";
await layout.handleCommitClick();

pruefe(layout.submittedValue === "3798", "Die Abgabe wurde nicht uebernommen.");
pruefe(zeile().hidden === false, "Die Abgabezeile bleibt versteckt.");
pruefe(zeile().textContent.includes("3798"), "Die Abgabezeile nennt den Wert nicht: " + zeile().textContent);
pruefe(knopf().textContent === "Erneut abgeben",
    "Der Knopf laedt nicht zum Aendern ein: " + knopf().textContent);

// --- Die Gegenrichtung zuerst: derselbe Stand aendert nichts ---
layout.update(zustand(1));

pruefe(layout.submittedValue === "3798",
    "Ohne Zuruecksetzen ging die Abgabe verloren - der Spieler saehe seinen Tipp nicht mehr.");

// --- Zuruecksetzen ---
layout.update(zustand(2));

pruefe(layout.submittedValue === null,
    "Nach dem Zuruecksetzen gilt der alte Tipp weiter, obwohl er serverseitig geloescht ist.");

pruefe(zeile().hidden === true,
    "Nach dem Zuruecksetzen steht weiter 'Abgegeben: …' da: " + zeile().textContent);

pruefe(feld().value === "",
    "Nach dem Zuruecksetzen steht der alte Wert noch im Feld: '" + feld().value + "'");

pruefe(knopf().textContent === "Eingabe bestätigen",
    "Der Knopf traegt weiter die alte Beschriftung: " + knopf().textContent);

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("inputLayout: alle Zusicherungen gruen.");
