// Minispiel Gluecksrad (M35 C4) am Telefon: nur der Dreher waehlt ein Ziel, genau einmal; alle anderen
// schauen auf den Beamer. Was das Rad ergab, sagt der Server erst mit der Aufloesung.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { GluecksradModul } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/gluecksrad.js");

let fehler = 0;

// Auch was erst nach einem await gesendet wuerde, soll vor der Zusicherung da sein.
const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new GluecksradModul();
modul.mount(wurzel);
modul.setLocked(false);

const kopf = () => wurzel.querySelector(".gluecksrad-kopf").textContent;
const text = () => wurzel.querySelector(".gluecksrad-text").textContent;
const tasten = () => wurzel.querySelectorAll(".spielerwahl-taste");

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

// --- Der Dreher waehlt sein Ziel - genau einmal ---
const waehlt = {
    choosesTarget: true, segmentName: "Klauen", choicePrompt: "Wen beklaust du?",
    options: [{ id: "b-1", name: "Bert" }, { id: "c-1", name: "Carl" }], resultText: null
};
modul.starte(waehlt, senden);
pruefe(kopf().includes("Klauen"), "Der Dreher sieht sein Feld nicht: " + kopf());
pruefe(wurzel.querySelector(".spielerwahl-frage").textContent === "Wen beklaust du?", "Die Frage des Feldes fehlt.");
pruefe(tasten().length === 2, "Nicht je Mitspieler eine Taste.");
tasten()[0].click();
tasten()[1].click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].chosen === "b-1", "Nicht genau eine Wahl gesendet: " + JSON.stringify(gesendet));

// --- Gesperrt: nichts ---
gesendet = [];
modul.starte(waehlt, senden);
modul.setLocked(true);
tasten()[0].click();
await ruhe();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat gewählt.");
modul.setLocked(false);

// --- Ohne Zielwahl: keine Tasten, kein Feld ---
modul.starte({ choosesTarget: false, segmentName: null, options: [] }, senden);
pruefe(tasten().length === 0, "Ohne Zielwahl stehen Tasten da.");
pruefe(text().includes("Beamer"), "Ohne Zielwahl fehlt der Hinweis auf den Beamer: " + text());
pruefe(!kopf().includes("zeigt:"), "Ohne Zielwahl nennt das Telefon ein Feld: " + kopf());

// --- Abgegeben und Ende ---
modul.zeigeErgebnis({ segmentName: "Klauen" });
pruefe(text().includes("Gewählt"), "Nach der Wahl fehlt die Bestätigung: " + text());

modul.zeigeEnde({ segmentName: "Klauen", resultText: "Klauen – Anna beklaut Bert." });
pruefe(kopf().includes("Klauen") && text() === "Klauen – Anna beklaut Bert.", "Die Auflösung kommt nicht an: " + kopf() + " / " + text());

modul.zeigeEnde({ segmentName: null, resultText: null });
pruefe(text().includes("Beamer"), "Ohne Auflösung fehlt der Hinweis: " + text());

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("gluecksrad: alle Zusicherungen gruen.");
