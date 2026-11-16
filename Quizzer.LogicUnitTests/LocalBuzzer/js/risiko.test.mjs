// Sonderregel Risiko (M35 C3) am Telefon: doppelt spielen, ja oder nein - genau einmal, und die Wahl
// steht als Satz da, nie nur als Farbe.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { RisikoModul, wahlText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/sonderregeln/risiko.js");

let fehler = 0;

const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new RisikoModul();
modul.mount(wurzel);
modul.setLocked(false);

const text = () => wurzel.querySelector(".risiko-text").textContent;
const tasten = () => wurzel.querySelectorAll(".risiko-taste");

const gesendet = [];
modul.starte({}, async (payload) => { gesendet.push(payload); });
pruefe(tasten().length === 2, "Ja und nein fehlen.");

tasten()[0].click();
tasten()[1].click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].doubled === true, "Nicht genau ein 'doppelt' gesendet: " + JSON.stringify(gesendet));
pruefe(text().includes("doppelt"), "Die Wahl steht nicht als Satz da: " + text());
pruefe(tasten()[0].classList.contains("risiko-aktiv"), "Die gewaehlte Taste ist nicht markiert.");

// --- Gesperrt: nichts geht raus ---
modul.starte({}, async (payload) => { gesendet.push(payload); });
modul.setLocked(true);
tasten()[1].click();
await ruhe();
pruefe(gesendet.length === 1, "Gesperrt und trotzdem gesendet.");

// --- Nach der Vorphase: die eigene Wahl, ohne Wahl der Hinweis ---
modul.zeigeErgebnis({ ownDoubled: false });
pruefe(text() === wahlText(false) && text().includes("einfach"), "Die eigene Wahl kommt nicht zurueck: " + text());
modul.zeigeEnde({ ownDoubled: null });
pruefe(text().includes("wie immer"), "Ohne Wahl fehlt der Hinweis: " + text());

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("risiko: alle Zusicherungen gruen.");
