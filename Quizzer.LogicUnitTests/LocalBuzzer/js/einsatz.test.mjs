// Sonderregel Einsatz (M35 C3) am Telefon: eine ganze Zahl von 0 bis zur Obergrenze, genau einmal.
// Was nicht passt, bleibt am Telefon und sagt warum; der Server prueft trotzdem noch einmal.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { EinsatzModul, pruefeEinsatz } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/sonderregeln/einsatz.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

// --- Die Pruefung allein ---
pruefe(pruefeEinsatz("0", 300).ok, "Null ist kein gueltiger Einsatz.");
pruefe(pruefeEinsatz(" 300 ", 300).wert === 300, "Genau die Grenze ist kein gueltiger Einsatz.");
pruefe(!pruefeEinsatz("301", 300).ok, "Ueber der Grenze angenommen.");
pruefe(!pruefeEinsatz("-5", 300).ok, "Ein negativer Einsatz angenommen.");
pruefe(!pruefeEinsatz("12.5", 300).ok, "Eine Kommazahl angenommen.");
pruefe(!pruefeEinsatz("", 300).ok, "Ein leeres Feld angenommen.");

// --- Das Modul ---
const modul = new EinsatzModul();
modul.mount(wurzel);
modul.setLocked(false);

const kopf = () => wurzel.querySelector(".einsatz-kopf").textContent;
const text = () => wurzel.querySelector(".einsatz-text").textContent;
const eingabe = () => wurzel.querySelector(".einsatz-eingabe");
const setzen = () => wurzel.querySelector(".einsatz-taste").click();

const gesendet = [];
modul.starte({ max: 300 }, async (payload) => { gesendet.push(payload); });
pruefe(kopf().includes("0 bis 300"), "Die Obergrenze steht nicht da: " + kopf());

eingabe().value = "400";
setzen();
pruefe(gesendet.length === 0, "Ueber der Grenze gesendet.");
pruefe(text().includes("Höchstens 300"), "Der Grund steht nicht da: " + text());

eingabe().value = "250";
setzen();
setzen();
pruefe(gesendet.length === 1 && gesendet[0].amount === 250, "Nicht genau ein Einsatz von 250 gesendet: " + JSON.stringify(gesendet));
pruefe(eingabe().disabled, "Nach dem Setzen laesst sich weiter tippen.");
pruefe(text().includes("250"), "Der eigene Einsatz steht nicht als Text da: " + text());

// --- Gesperrt: nichts geht raus ---
const vorher = gesendet.length;
modul.starte({ max: 300 }, async (payload) => { gesendet.push(payload); });
modul.setLocked(true);
eingabe().value = "10";
setzen();
pruefe(gesendet.length === vorher, "Gesperrt und trotzdem gesendet.");

// --- Wiederverbunden bzw. nach der Vorphase: die eigene Abgabe, sonst der Hinweis ---
modul.zeigeErgebnis({ ownAmount: 120 });
pruefe(text() === "Dein Einsatz: 120", "Die eigene Abgabe kommt nicht zurueck: " + text());
modul.zeigeEnde({ ownAmount: null });
pruefe(text().includes("Feldpunkte"), "Ohne Einsatz fehlt der Hinweis auf die Feldpunkte: " + text());

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("einsatz: alle Zusicherungen gruen.");
