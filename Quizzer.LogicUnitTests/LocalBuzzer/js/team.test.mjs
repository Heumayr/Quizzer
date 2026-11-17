// Das Team am Telefon (M37 T4): Symbol und Name auf der Farbe des Teams, die Farbe nur als Klasse aus der Palette,
// der Name nur als Text. Ohne Team verschwindet das Banner, beim Wechsel bleibt keine alte Farbe stehen.
// M37 T5: darunter „Sprecher: Anna“, ebenfalls nur als Text; ohne Sprecher verborgen.

import { readFileSync } from "node:fs";
import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const modulPfad = new URL("../../../LocalBuzzer.Service/wwwroot/JS/team.js", import.meta.url);
const { zeigeTeam, TEAM_FARBEN } = await import(modulPfad);

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const farben = () => [...wurzel.classList.werte].filter(k => k.startsWith("team-farbe-"));
const symbol = () => wurzel.querySelector(".team-symbol");
const name = () => wurzel.querySelector(".team-name");
const sprecher = () => wurzel.querySelector(".team-sprecher");

// --- Ein Team: Symbol, Name, Farbe ---
zeigeTeam(wurzel, { name: "Die Füchse", symbol: "▲", colorIndex: 1 });
pruefe(wurzel.hidden === false, "Das Banner ist mit Team verborgen.");
pruefe(symbol()?.textContent === "▲", "Das Symbol steht nicht da: " + symbol()?.textContent);
pruefe(name()?.textContent === "Die Füchse", "Der Name steht nicht da: " + name()?.textContent);
pruefe(farben().join() === "team-farbe-1", "Die Farbe ist nicht team-farbe-1: " + farben().join());
pruefe(symbol()?.getAttribute("aria-hidden") === "true", "Das Symbol wird vorgelesen.");

// --- Ein Name mit Markup bleibt Text ---
const boese = "<img src=x onerror=alert(1)>";
zeigeTeam(wurzel, { name: boese, symbol: "●", colorIndex: 0 });
pruefe(name()?.textContent === boese, "Der Name kam nicht als Text an: " + name()?.textContent);
pruefe(!/innerHTML/.test(readFileSync(modulPfad, "utf8")), "team.js benutzt innerHTML - ein Teamname ist frei gewählt.");

// --- Sprecher (M37 T5): „Sprecher: Anna“, als Text, ohne Sprecher verborgen ---
zeigeTeam(wurzel, { name: "Rot", symbol: "✚", colorIndex: 5, speaker: "Anna" });
pruefe(sprecher()?.textContent === "Sprecher: Anna", "Der Sprecher steht nicht da: " + sprecher()?.textContent);
pruefe(sprecher()?.hidden === false, "Der Sprecher ist verborgen.");
zeigeTeam(wurzel, { name: "Rot", symbol: "✚", colorIndex: 5, speaker: boese });
pruefe(sprecher()?.textContent === "Sprecher: " + boese, "Der Sprecher kam nicht als Text an: " + sprecher()?.textContent);
zeigeTeam(wurzel, { name: "Rot", symbol: "✚", colorIndex: 5, speaker: "" });
pruefe(sprecher()?.hidden === true, "Ohne Sprecher steht die Zeile da.");
pruefe(wurzel.querySelectorAll(".team-sprecher").length === 1, "Der Wechsel legte ein zweites Sprecherfeld an.");

// --- Wechsel: keine alte Farbe, kein zweites Paar ---
zeigeTeam(wurzel, { name: "Blau", symbol: "★", colorIndex: 4 });
pruefe(farben().join() === "team-farbe-4", "Nach dem Wechsel stehen mehrere Farben: " + farben().join());
pruefe(wurzel.querySelectorAll(".team-name").length === 1, "Der Wechsel legte ein zweites Namensfeld an.");

// --- Eine Farbe außerhalb der Palette setzt keine Klasse ---
for (const falsch of [-1, TEAM_FARBEN, 2.5, "3", null, undefined]) {
    zeigeTeam(wurzel, { name: "Grau", symbol: "", colorIndex: falsch });
    pruefe(farben().length === 0, `Farbe ${falsch} setzte eine Klasse: ` + farben().join());
}

// --- Ohne Team: verborgen, keine Farbe ---
zeigeTeam(wurzel, { name: "Rot", symbol: "✚", colorIndex: 5 });
zeigeTeam(wurzel, null);
pruefe(wurzel.hidden === true, "Ohne Team steht das Banner da.");
pruefe(farben().length === 0, "Ohne Team bleibt eine Farbe stehen: " + farben().join());

// --- Ohne Ziel kein Fehler (eine alte Seite ohne #team) ---
zeigeTeam(null, { name: "Rot", symbol: "✚", colorIndex: 5 });

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("team: alle Zusicherungen gruen.");
