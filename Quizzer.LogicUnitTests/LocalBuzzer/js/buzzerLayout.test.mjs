// Das Buzzer-Layout - der Knopf, den die Gaeste am haeufigsten druecken.
//
// Zwei Dinge, die am Abend zaehlen: ein zweiter Druck darf nichts mehr senden, und nach
// einer neuen Runde muss der Knopf wieder gehen.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

// Node bringt ein eigenes navigator mit, das sich nicht ersetzen laesst - nur ergaenzen.
// buzzerLayout ruft navigator.vibrate?.(40), der optionale Aufruf traegt den Fall ohnehin.
if (!globalThis.navigator.vibrate) {
    Object.defineProperty(globalThis.navigator, "vibrate", { value: () => {} });
}

const { BuzzerLayout } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/layouts/buzzerLayout.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

let gesendet = 0;

const layout = new BuzzerLayout({ buzz: async () => { gesendet++; } });

layout.render(wurzel, { playerName: "Anna", winner: null });

const knopf = () => wurzel.querySelector(".buzz-btn");

pruefe(knopf().textContent === "Gesperrt",
    "Vor der Freigabe steht nicht 'Gesperrt' auf dem Knopf: " + knopf().textContent);

layout.setLocked(false);

pruefe(knopf().disabled === false, "Der Knopf bleibt gesperrt, obwohl die Runde offen ist.");
pruefe(knopf().textContent === "BUZZ", "Der Knopf laedt nicht zum Druecken ein: " + knopf().textContent);

// --- Druecken ---
await layout.press();

pruefe(gesendet === 1, "Der Druck kam nicht beim Server an.");
pruefe(knopf().disabled === true, "Nach dem Druck ist der Knopf noch offen.");

// Ein zweiter Druck darf nichts mehr senden - sonst zaehlt eine Runde zwei Buzzer.
await layout.press();

pruefe(gesendet === 1, "Ein zweiter Druck hat noch einmal gesendet - " + gesendet + " statt 1.");

// --- Der Gewinner steht fest ---
layout.update({ playerName: "Anna", winner: "Bert" });

pruefe(knopf().textContent === "Bert war schneller",
    "Der Knopf sagt nicht, wer schneller war: " + knopf().textContent);

layout.update({ playerName: "Anna", winner: "Anna" });

pruefe(knopf().textContent === "Du bist dran!",
    "Der Gewinner erfaehrt es nicht auf seinem eigenen Telefon: " + knopf().textContent);

// --- Neue Runde ---
layout.update({ playerName: "Anna", winner: null });
layout.setLocked(false);

pruefe(knopf().disabled === false,
    "Nach der neuen Runde bleibt der Knopf gesperrt - der Spieler ist raus.");

pruefe(knopf().textContent === "BUZZ",
    "Nach der neuen Runde steht die alte Beschriftung da: " + knopf().textContent);

await layout.press();

pruefe(gesendet === 2, "In der neuen Runde laesst sich nicht mehr buzzern.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("buzzerLayout: alle Zusicherungen gruen.");
