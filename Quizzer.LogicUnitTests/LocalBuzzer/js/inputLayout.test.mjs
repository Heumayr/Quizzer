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

// M37 E4: das Feld nimmt hoechstens so viel an, wie der Server behaelt (BuzzerInputState.MaxLength).
pruefe(feld().getAttribute("maxlength") === "80", "Das Feld hat keine Grenze von 80 Zeichen: " + feld().getAttribute("maxlength"));

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

// --- P10-4 (M37 P11): die Abgabe nennt die Runde, die das Telefon gerade sieht - eine alte verwirft der Server ---
{
    const wurzel2 = baueDom().wurzel;
    const gesendet = [];
    const runde = new InputLayout({ submitInput: async p => { gesendet.push(p); } });

    runde.render(wurzel2, zustand(7));
    wurzel2.querySelector(".input-field").value = "12";
    await runde.handleCommitClick();

    runde.update(zustand(8));
    wurzel2.querySelector(".input-field").value = "13";
    await runde.handleCommitClick();

    pruefe(gesendet.map(p => p.resetCount).join(",") === "7,8",
        "Die Abgabe nennt nicht die Runde, die das Telefon sah (resetCount): " + JSON.stringify(gesendet));

    // Kommt die neue Runde, bevor der Aufruf zurück ist, zählt die Abgabe dort nicht - das Telefon sagt nicht „Abgegeben“.
    const spaet = new InputLayout({ submitInput: async () => spaet.update(zustand(9)) });
    const wurzel3 = baueDom().wurzel;
    spaet.render(wurzel3, zustand(8));
    wurzel3.querySelector(".input-field").value = "14";
    await spaet.handleCommitClick();

    pruefe(spaet.submittedValue === null, "Nach einer neuen Runde während des Sendens steht 'Abgegeben: 14' - der Server hat nichts.");
    pruefe(wurzel3.querySelector(".commit-btn").disabled === false, "Nach einer neuen Runde während des Sendens lässt sich nicht abgeben.");
}

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("inputLayout: alle Zusicherungen gruen.");
