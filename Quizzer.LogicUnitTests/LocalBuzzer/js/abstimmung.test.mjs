// Die Team-Abstimmung am Telefon (M37 T7, F-M37-13): vor dem Schluss die eigene Stimme und „2 von 3 haben abgestimmt",
// danach „Euer Team: B (2 von 3)" - alles nur als Text. Im Teambanner unter dem Sprecher; ohne Abstimmung verborgen.

import { readFileSync } from "node:fs";
import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const modulPfad = new URL("../../../LocalBuzzer.Service/wwwroot/JS/abstimmung.js", import.meta.url);
const { abstimmungsText, zeigeAbstimmung } = await import(modulPfad);
const { zeigeTeam } = await import(new URL("../../../LocalBuzzer.Service/wwwroot/JS/team.js", import.meta.url));

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

// --- Vor dem Schluss: eigene Stimme und Stand des Teams ---
pruefe(abstimmungsText({ own: ["B"], voted: 2, of: 3, closed: false, result: "" }) === "Deine Stimme: B · 2 von 3 haben abgestimmt",
    "Die eigene Stimme steht nicht da: " + abstimmungsText({ own: ["B"], voted: 2, of: 3, closed: false, result: "" }));
pruefe(abstimmungsText({ own: [], voted: 1, of: 3, closed: false, result: "" }) === "Du hast noch nicht abgestimmt · 1 von 3 haben abgestimmt",
    "Ohne eigene Stimme: " + abstimmungsText({ own: [], voted: 1, of: 3, closed: false, result: "" }));
pruefe(abstimmungsText({ own: ["A", "C"], voted: 1, of: 2, closed: false }) === "Deine Stimme: A + C · 1 von 2 haben abgestimmt",
    "Eine Mehrfachauswahl steht nicht da.");

// --- Vor dem Schluss zeigt das Telefon kein Ergebnis, auch wenn eins mitkäme ---
pruefe(!abstimmungsText({ own: ["B"], voted: 3, of: 3, closed: false, result: "A (2 von 3)" }).includes("A (2 von 3)"),
    "Vor dem Schluss steht die Antwort des Teams da.");

// --- Nach dem Schluss: die Antwort des eigenen Teams; vor der Auswertung der Stand ---
pruefe(abstimmungsText({ own: ["B"], voted: 3, of: 3, closed: true, result: "B (2 von 3)" }) === "Euer Team: B (2 von 3)",
    "Nach dem Schluss: " + abstimmungsText({ own: ["B"], voted: 3, of: 3, closed: true, result: "B (2 von 3)" }));
pruefe(abstimmungsText({ own: [], voted: 2, of: 3, closed: true, result: "" }) === "Abstimmung zu – 2 von 3 haben abgestimmt",
    "Zu, aber noch nicht ausgewertet: " + abstimmungsText({ own: [], voted: 2, of: 3, closed: true, result: "" }));

// --- Ohne Abstimmung nichts ---
pruefe(abstimmungsText(null) === "" && abstimmungsText(undefined) === "", "Ohne Abstimmung steht Text da.");

// --- Im Teambanner: als Text, ohne Abstimmung verborgen, keine zweite Zeile ---
const boese = "<img src=x onerror=alert(1)>";
const zeile = () => wurzel.querySelector(".team-abstimmung");

zeigeTeam(wurzel, { name: "Rot", symbol: "✚", colorIndex: 5, speaker: "Anna", vote: { own: [boese], voted: 1, of: 2, closed: false, result: "" } });
pruefe(zeile()?.textContent === `Deine Stimme: ${boese} · 1 von 2 haben abgestimmt`, "Die Stimme kam nicht als Text an: " + zeile()?.textContent);
pruefe(zeile()?.hidden === false, "Die Abstimmung ist verborgen.");

zeigeTeam(wurzel, { name: "Rot", symbol: "✚", colorIndex: 5, speaker: "Anna", vote: { own: [], voted: 2, of: 2, closed: true, result: boese } });
pruefe(zeile()?.textContent === `Euer Team: ${boese}`, "Das Ergebnis kam nicht als Text an: " + zeile()?.textContent);

zeigeTeam(wurzel, { name: "Rot", symbol: "✚", colorIndex: 5, speaker: "Anna", vote: null });
pruefe(zeile()?.hidden === true, "Ohne Abstimmung steht die Zeile da.");
pruefe(wurzel.querySelectorAll(".team-abstimmung").length === 1, "Der Wechsel legte eine zweite Abstimmungszeile an.");

// --- Ohne Ziel kein Fehler ---
zeigeAbstimmung(null, { own: ["A"], voted: 1, of: 1, closed: false });
pruefe(!/innerHTML/.test(readFileSync(modulPfad, "utf8")), "abstimmung.js benutzt innerHTML.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("abstimmung: alle Zusicherungen gruen.");
