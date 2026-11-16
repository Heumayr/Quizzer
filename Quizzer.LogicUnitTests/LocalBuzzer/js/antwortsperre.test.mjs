// Sperre je Spieler fuer die gewoehnliche Frage (M35 C3b): wer bei Auktion, Experte oder Allianz nicht
// antworten darf, sieht statt des Layouts den Satz, wer antwortet - und bekommt das Layout zurueck,
// sobald die Sperre geht. Der Server prueft die Abgabe selbst; hier geht es um das, was der Gast sieht.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { LayoutManager, EXCLUDED_TEXT } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/layoutManager.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const spur = [];
const manager = new LayoutManager({ allLocked: false, currentLayoutLocked: false });

manager.register(1, {
    render(host) {
        spur.push("render");
        const el = document.createElement("div");
        el.className = "buzz-btn";
        host.appendChild(el);
    },
    update() { spur.push("update"); },
    setLocked(g) { spur.push("locked=" + !!g); },
    dispose() { spur.push("dispose"); },
});

const satz = () => wurzel.querySelector(".ausgeschlossen-text");
const knopf = () => wurzel.querySelector(".buzz-btn");

// --- Gesperrt von Anfang an (auch so kommt ein wiederverbindendes Telefon an) ---
manager.switchTo(1, { host: wurzel, currentLayout: 1, excluded: true, currentLayoutLocked: true });

pruefe(satz() != null, "Gesperrt steht kein Satz da - der Gast sieht nur, dass nichts geht.");
pruefe((satz()?.textContent ?? "").includes(EXCLUDED_TEXT), "Der Satz sagt nicht, dass andere antworten: " + satz()?.textContent);
pruefe(EXCLUDED_TEXT === "Diese Frage beantworten andere", "Der Satz hat sich geaendert: " + EXCLUDED_TEXT);
pruefe(knopf() == null && !spur.includes("render"), "Gesperrt steht trotzdem der Buzzer da.");

// --- Dasselbe noch einmal: nichts neu aufbauen ---
manager.update({ host: wurzel, currentLayout: 1, excluded: true, currentLayoutLocked: true });
pruefe(spur.length === 0, "Ein gleicher Stand baute neu auf: " + spur.join(", "));

// --- Die Sperre geht: das Layout kommt zurueck ---
manager.update({ host: wurzel, currentLayout: 1, excluded: false, currentLayoutLocked: false });
pruefe(spur.includes("render") && knopf() != null, "Ohne Sperre kommt das Layout nicht zurueck.");
pruefe(satz() == null, "Ohne Sperre steht der Satz noch da.");

// --- Die Sperre kommt mitten in der Frage ---
spur.length = 0;
manager.update({ host: wurzel, currentLayout: 1, excluded: true, currentLayoutLocked: true });
pruefe(spur.includes("dispose") && satz() != null && knopf() == null, "Die Sperre mitten in der Frage zeigt den Satz nicht: " + spur.join(", "));

// --- M35 C7b: mit eigenem Satz vom Server (Joker-Sperre) steht dieser, nicht der allgemeine ---
const grund = "Anna hat dich per Joker gesperrt – du schaust zu.";
spur.length = 0;
manager.update({ host: wurzel, currentLayout: 1, excluded: true, excludedReason: grund, currentLayoutLocked: true });
pruefe(satz()?.textContent === grund, "Der Satz des Servers steht nicht da: " + satz()?.textContent);
manager.update({ host: wurzel, currentLayout: 1, excluded: true, excludedReason: grund, currentLayoutLocked: true });
pruefe(spur.length === 0 && satz()?.textContent === grund, "Derselbe Satz baute neu auf: " + spur.join(", "));
manager.update({ host: wurzel, currentLayout: 1, excluded: true, currentLayoutLocked: true });
pruefe((satz()?.textContent ?? "").includes(EXCLUDED_TEXT), "Ohne Satz des Servers kommt der allgemeine nicht zurueck: " + satz()?.textContent);

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("antwortsperre: alle Zusicherungen gruen.");
