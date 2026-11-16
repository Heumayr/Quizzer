// Sonderregeln mit Spielerwahl (M35 C3b: Wette, Experte, Allianz) am Telefon: die Frage des Servers ueber den
// Tasten, genau eine Wahl { chosen: id }, gesperrt nichts, und die eigene Wahl kommt als Satz zurueck.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { WahlModul, wahlText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/sonderregeln/wahl.js");

let fehler = 0;

const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const optionen = [{ id: "a-1", name: "Anna" }, { id: "b-2", name: "Bert" }];
const tasten = () => wurzel.querySelectorAll(".spielerwahl-taste");
const frage = () => wurzel.querySelector(".spielerwahl-frage")?.textContent ?? "";
const text = () => wurzel.querySelector(".wahl-text").textContent;

const modul = new WahlModul();
modul.mount(wurzel);

const gesendet = [];
modul.starte({ prompt: "Auf wen wettest du?", options: optionen }, async (p) => { gesendet.push(p); });
pruefe(frage() === "Auf wen wettest du?", "Die Frage des Servers steht nicht da: " + frage());
pruefe(tasten().length === 2, "Nicht je Option eine Taste.");

// --- Gesperrt: nichts geht raus ---
tasten()[1].click();
await ruhe();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat gewählt.");

// --- Entsperrt: genau eine Wahl ---
modul.setLocked(false);
tasten()[1].click();
tasten()[0].click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].chosen === "b-2", "Nicht genau einmal die gewählte Id: " + JSON.stringify(gesendet));

// --- Wiederverbunden bzw. nach der Vorphase: die eigene Wahl als Satz (nicht die erste Taste - gemessen: das hielt
// eine Zusicherung mit der ersten Option gruen, auch wenn der Name aus der falschen Taste kam) ---
modul.zeigeErgebnis({ ownChoice: "b-2", options: optionen });
pruefe(text() === "Deine Wahl: Bert", "Die eigene Wahl kommt nicht als Satz zurueck: " + text());
pruefe(tasten().length === 0, "Nach der Wahl stehen noch Tasten.");
pruefe(wahlText({ ownChoice: null }).includes("Keine Wahl"), "Ohne Wahl fehlt der Hinweis.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("wahl: alle Zusicherungen gruen.");
