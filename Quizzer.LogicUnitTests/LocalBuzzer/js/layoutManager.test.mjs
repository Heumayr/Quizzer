// Der Layoutwechsel auf dem Telefon. Er laeuft, sobald der Spielleiter eine Frage eines
// anderen Typs oeffnet - Buzzer, Tastenwahl, Eingabe. Klemmt er, bleibt das Telefon beim
// Layout der vorigen Frage stehen, und der Gast drueckt ins Leere.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { LayoutManager } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/layoutManager.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

// Ein Layout, das mitschreibt, was mit ihm geschieht.
function macheLayout(name, spur) {
    return {
        render(host) {
            spur.push(name + ":render");
            const el = document.createElement("div");
            el.className = "layout-" + name;
            host.appendChild(el);
        },
        update() { spur.push(name + ":update"); },
        setLocked(g) { spur.push(name + ":locked=" + !!g); },
        dispose() { spur.push(name + ":dispose"); },
    };
}

const spur = [];
const zustand = { allLocked: false, currentLayoutLocked: false };
const manager = new LayoutManager(zustand);

manager.register("buzzer", macheLayout("buzzer", spur));
manager.register("keyselect", macheLayout("keyselect", spur));

const kontext = (gesperrt = false) => ({
    host: wurzel,
    currentLayoutLocked: gesperrt,
    allLocked: false,
});

// --- Erster Aufbau ---
manager.switchTo("buzzer", kontext());

pruefe(wurzel.querySelector(".layout-buzzer") !== null, "Das Buzzer-Layout wurde nicht gebaut.");
pruefe(spur.includes("buzzer:render"), "render wurde nicht gerufen.");

// --- Wechsel ---
spur.length = 0;
manager.switchTo("keyselect", kontext());

pruefe(spur[0] === "buzzer:dispose",
    "Das alte Layout wurde nicht zuerst abgeraeumt - seine Ereignisse haengen weiter am DOM. "
    + "Spur: " + spur.join(", "));

pruefe(wurzel.querySelector(".layout-buzzer") === null,
    "Das alte Layout steht noch im Baum - der Gast sieht zwei Layouts uebereinander.");

pruefe(wurzel.querySelector(".layout-keyselect") !== null, "Das neue Layout wurde nicht gebaut.");

// --- Unbekanntes Layout: eine Meldung statt eines leeren Bildschirms ---
spur.length = 0;
manager.switchTo("gibtesnicht", kontext());

pruefe(spur[0] === "keyselect:dispose",
    "Auch beim Wechsel ins Leere muss das alte Layout abgeraeumt werden. Spur: " + spur.join(", "));

pruefe(manager.activeLayout === null,
    "Nach einem unbekannten Layout haelt der Manager noch das alte - update ginge dann dorthin.");

// --- update ohne aktives Layout darf nicht werfen ---
manager.update(kontext());

// --- Sperren und Freigeben ---
manager.switchTo("buzzer", kontext());
spur.length = 0;

manager.lockAll();
pruefe(zustand.allLocked === true, "lockAll hat den Zustand nicht gesetzt.");
pruefe(spur.includes("buzzer:locked=true"), "lockAll hat das Layout nicht gesperrt.");

spur.length = 0;
zustand.currentLayoutLocked = false;
manager.unlockAll();

pruefe(zustand.allLocked === false, "unlockAll hat den Zustand nicht geloescht.");
pruefe(spur.includes("buzzer:locked=false"),
    "unlockAll hat das Layout nicht freigegeben. Spur: " + spur.join(", "));

// Die Gegenrichtung: ist das eigene Layout gesperrt, gibt unlockAll es NICHT frei.
spur.length = 0;
zustand.currentLayoutLocked = true;
manager.unlockAll();

pruefe(spur.includes("buzzer:locked=true"),
    "unlockAll hat ein Layout freigegeben, das der Server gesperrt haelt - der Spieler koennte "
    + "in einer fremden Runde buzzern. Spur: " + spur.join(", "));

// --- Was der Gast sieht, wenn die Verbindung weg ist ---
spur.length = 0;
let gedrueckt = 0;

manager.switchTo("buzzer", kontext());
spur.length = 0;

manager.showNotice(wurzel, {
    text: "Verbindung verloren",
    buttonLabel: "Neu verbinden",
    onButton: () => { gedrueckt++; },
});

pruefe(spur[0] === "buzzer:dispose",
    "Die Meldung ersetzt das Layout, ohne es abzuraeumen - seine Ereignisse haengen weiter. "
    + "Spur: " + spur.join(", "));

pruefe(wurzel.querySelector(".layout-buzzer") === null,
    "Der Buzzer steht noch da, waehrend die Verbindung weg ist - der Gast drueckt ins Leere.");

const meldung = wurzel.querySelector(".notice-text");
const meldeknopf = wurzel.querySelector(".notice-btn");

pruefe(meldung !== null && meldung.textContent === "Verbindung verloren",
    "Der Grund steht nicht da: " + (meldung ? meldung.textContent : "keine Meldung"));

pruefe(meldeknopf !== null && meldeknopf.textContent === "Neu verbinden",
    "Es gibt keinen Weg zurueck: " + (meldeknopf ? meldeknopf.textContent : "kein Knopf"));

meldeknopf.click();

pruefe(gedrueckt === 1,
    "Der Knopf tut nichts - der Gast sitzt fest, bis er die Seite von Hand neu laedt.");

pruefe(manager.activeLayout === null,
    "Nach der Meldung haelt der Manager noch ein Layout - ein spaeteres update ginge dorthin.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("layoutManager: alle Zusicherungen gruen.");
