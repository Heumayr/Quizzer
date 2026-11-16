// Sonderregel Auktion (M35 C3b) am Telefon: dasselbe Modul wie der Einsatz, eigene Texte - ein geheimes Gebot,
// 0 heisst passen, genau einmal, ueber der Grenze bleibt es am Telefon.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { AuktionModul } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/sonderregeln/auktion.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new AuktionModul();
modul.mount(wurzel);
modul.setLocked(false);

const kopf = () => wurzel.querySelector(".einsatz-kopf").textContent;
const text = () => wurzel.querySelector(".einsatz-text").textContent;
const eingabe = () => wurzel.querySelector(".einsatz-eingabe");
const taste = () => wurzel.querySelector(".einsatz-taste");

pruefe(taste().textContent === "Bieten", "Die Taste heisst nicht Bieten: " + taste().textContent);

const gesendet = [];
modul.starte({ max: 200 }, async (payload) => { gesendet.push(payload); });
pruefe(kopf().includes("0 bis 200") && kopf().includes("passen"), "Grenze oder Passen fehlt: " + kopf());

eingabe().value = "201";
taste().click();
pruefe(gesendet.length === 0, "Ueber der Grenze geboten.");

eingabe().value = "0";
taste().click();
taste().click();
pruefe(gesendet.length === 1 && gesendet[0].amount === 0, "Passen ging nicht genau einmal als 0 raus: " + JSON.stringify(gesendet));
pruefe(text().includes("passt"), "Passen steht nicht als Text da: " + text());

// --- Wiederverbunden bzw. nach der Vorphase: das eigene Gebot ---
modul.zeigeErgebnis({ ownAmount: 150 });
pruefe(text() === "Dein Gebot: 150", "Das eigene Gebot kommt nicht zurueck: " + text());
modul.zeigeEnde({ ownAmount: null });
pruefe(text().includes("Kein Gebot"), "Ohne Gebot fehlt der Hinweis: " + text());

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("auktion: alle Zusicherungen gruen.");
