// Sonderregel Finale (M37 E4) am Telefon: das Modul des Einsatzes mit eigenen Texten - ein geheimer Einsatz bis zur
// eigenen Grenze, genau einmal; ohne Einsatz heisst es 0, nicht "um die Feldpunkte".

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { FinaleModul } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/sonderregeln/finale.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new FinaleModul();
modul.mount(wurzel);
modul.setLocked(false);

const kopf = () => wurzel.querySelector(".einsatz-kopf").textContent;
const text = () => wurzel.querySelector(".einsatz-text").textContent;
const eingabe = () => wurzel.querySelector(".einsatz-eingabe");
const taste = () => wurzel.querySelector(".einsatz-taste");

pruefe(taste().textContent === "Einsatz setzen", "Die Taste heisst nicht 'Einsatz setzen': " + taste().textContent);

const gesendet = [];
modul.starte({ max: 300 }, async (payload) => { gesendet.push(payload); });
pruefe(kopf().includes("0 bis 300") && kopf().includes("Finale"), "Grenze oder Finale fehlt: " + kopf());
pruefe(kopf().includes("niemand sonst"), "Der Kopf sagt nicht, dass der Einsatz geheim ist: " + kopf());

eingabe().value = "301";
taste().click();
eingabe().value = "-5";
taste().click();
pruefe(gesendet.length === 0, "Ueber der Grenze oder negativ gesetzt: " + JSON.stringify(gesendet));

eingabe().value = "120";
taste().click();
taste().click();
pruefe(gesendet.length === 1 && gesendet[0].amount === 120, "Der Einsatz ging nicht genau einmal raus: " + JSON.stringify(gesendet));
pruefe(text().includes("120"), "Der eigene Einsatz steht nicht da: " + text());

// --- Wiederverbunden bzw. nach der Vorphase: der eigene Einsatz, ohne einen die 0 ---
modul.zeigeErgebnis({ ownAmount: 80 });
pruefe(text() === "Dein Einsatz fürs Finale: 80", "Der eigene Einsatz kommt nicht zurueck: " + text());
modul.zeigeEnde({ ownAmount: null });
pruefe(text().includes("setzt 0") && !text().includes("Feldpunkte"), "Ohne Einsatz fehlt die 0: " + text());

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("finale: alle Zusicherungen gruen.");
