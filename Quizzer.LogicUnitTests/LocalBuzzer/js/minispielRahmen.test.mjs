// Der Minispiel-Rahmen am Telefon (M35 B3): Warten, Countdown, Uebergabe an das Modul der Art,
// Ergebnis - und dass ein Zustand, der mitten in der Wartezeit noch einmal kommt, die Uhr nicht
// neu startet. Abgegeben wird mit Art und Durchgang, die das Telefon gesehen hat.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();

if (!globalThis.navigator.vibrate) {
    Object.defineProperty(globalThis.navigator, "vibrate", { value: () => {} });
}

const { MinigameLayout } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/rahmen.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const uhr = baueUhr();
const gesendet = [];
const rahmen = new MinigameLayout(
    { submitAction: async (aktion) => { gesendet.push(aktion); } },
    { timer: uhr.timer, now: uhr.now });

function zustand(phase, durchgang, extra = {}, kindInfo = {}) {
    return {
        resetCount: 1,
        currentLayoutLocked: phase !== 1,
        allLocked: false,
        layoutInfo: {
            kind: 2, title: "Reaktion", rulesText: "Wartet auf Grün.", phase,
            currentRound: extra.currentRound ?? 1, totalRounds: 3, durchgang,
            countdownSeconds: 3, aborted: false, ownSubmitted: false, ...extra, kindInfo
        }
    };
}

const status = () => wurzel.querySelector(".minispiel-status").textContent;
const runde = () => wurzel.querySelector(".minispiel-runde").textContent;
const flaeche = () => wurzel.querySelector(".reaktion-taste");
const text = () => wurzel.querySelector(".reaktion-text")?.textContent ?? "";
const tippe = () => (flaeche().hoerer.pointerdown ?? []).forEach(fn => fn({ button: 0 }));

// Wie layoutManager.switchTo: altes Layout entsorgen, Wirt leeren, neu rendern.
function neuAufbauen(z) {
    rahmen.dispose();
    wurzel.innerHTML = "";
    rahmen.render(wurzel, z);
}

// --- Warten ---
rahmen.render(wurzel, zustand(0, 7, { currentRound: 0 }));
rahmen.setLocked(true);

pruefe(wurzel.querySelector(".minispiel-titel").textContent === "Reaktion", "Die Ueberschrift nennt die Art nicht.");
pruefe(runde() === "3 Runden", "Vor der ersten Runde steht nicht, wie viele es werden: " + runde());
pruefe(status().includes("Wartet auf Grün"), "Beim Warten steht der Regeltext nicht da: " + status());

// --- Runde startet: Countdown, dann rot ---
const laufend = zustand(1, 8, {}, { waitMs: 2000 });
rahmen.update(laufend);
rahmen.setLocked(false);

pruefe(runde() === "Runde 1 von 3", "Die Runde wird nicht als 'n von m' gezeigt: " + runde());
pruefe(status() === "Gleich geht es los: 3", "Der Countdown beginnt nicht bei 3: " + status());

uhr.vor(1000);
pruefe(status() === "Gleich geht es los: 2", "Der Countdown zaehlt nicht herunter: " + status());

uhr.vor(2000);
pruefe(status() === "Los!", "Nach dem Countdown steht nicht 'Los!': " + status());
pruefe(flaeche().classList.contains("reaktion-warten"), "Nach dem Countdown ist das Modul nicht im Wartezustand.");

// Derselbe Stand noch einmal (etwa nach der Abgabe eines anderen) darf die Uhr nicht neu starten.
uhr.vor(1500);
rahmen.update(laufend);
uhr.vor(500);
pruefe(flaeche().classList.contains("reaktion-jetzt"),
    "Ein wiederholter Stand hat die Wartezeit neu gestartet - gruen kam nicht nach 2 s.");

uhr.vor(250);
tippe();

pruefe(gesendet.length === 1, "Der Tipp ging nicht ueber SpielAktion hinaus.");
pruefe(gesendet[0]?.kind === 2 && gesendet[0]?.durchgang === 8,
    "Art und Durchgang gehen nicht mit: " + JSON.stringify(gesendet[0]));
pruefe(gesendet[0]?.payload?.milliseconds === 250, "Die Nutzlast traegt nicht die gemessene Zeit: " + JSON.stringify(gesendet[0]));

// --- Rundenende: die eigene Zeit ---
rahmen.update(zustand(2, 8, {}, { ownOutcome: 1, ownMilliseconds: 250, bestMilliseconds: 250 }));
pruefe(status().includes("Runde 1 vorbei"), "Das Rundenende wird nicht gesagt: " + status());
pruefe(text().includes("250 ms"), "Nach der Runde steht die eigene Zeit nicht da: " + text());

// --- Naechste Runde: neuer Durchgang geht mit ---
rahmen.update(zustand(1, 9, { currentRound: 2 }, { waitMs: 2000 }));
uhr.vor(3000 + 2000 + 300);
tippe();
pruefe(gesendet[1]?.durchgang === 9, "In der zweiten Runde geht der alte Durchgang mit: " + JSON.stringify(gesendet[1]));

// --- Wiederverbunden nach der eigenen Abgabe: sie kommt zurueck, kein zweiter Versuch ---
const vorher = gesendet.length;
neuAufbauen(zustand(1, 9, { currentRound: 2, ownSubmitted: true }, { ownOutcome: 2 }));
rahmen.setLocked(true);
uhr.vor(10000);
tippe();
pruefe(gesendet.length === vorher, "Nach dem Wiederverbinden liess sich ein zweites Mal abgeben.");
pruefe(text().includes("Frühstart"), "Nach dem Wiederverbinden fehlt die eigene Abgabe: " + text());

// --- Sperre je Spieler: wer diese Runde nicht mitspielt, schaut zu - ohne Countdown und Uhr ---
const gesperrt = zustand(1, 12, { currentRound: 3 }, { waitMs: 2000 });
gesperrt.currentLayoutLocked = true;
neuAufbauen(gesperrt);
rahmen.setLocked(true);
pruefe(status().includes("schaust zu"), "Ein gesperrter Spieler erfaehrt nicht, dass er zuschaut: " + status());
uhr.vor(10000);
pruefe(!flaeche().classList.contains("reaktion-jetzt"), "Beim gesperrten Spieler laeuft die Reaktion trotzdem.");

// M35 C2: sagt die Art, wer gerade spielt, steht das statt des allgemeinen Satzes da.
const zuschauer = zustand(1, 13, { currentRound: 1, watcherText: "Anna wählt einen Gegner …" }, {});
zuschauer.currentLayoutLocked = true;
neuAufbauen(zuschauer);
pruefe(status() === "Anna wählt einen Gegner …", "Der Zuschauertext der Art kommt nicht an: " + status());

// --- Ende ---
rahmen.update(zustand(3, 9, { currentRound: 3 },
    { isWinner: false, winnerNames: ["Bert"], winnerMilliseconds: 199, bestMilliseconds: 250 }));
pruefe(status() === "Minispiel vorbei.", "Das Ende wird nicht gesagt: " + status());
pruefe(text().includes("Bert"), "Am Ende fehlt der Sieger: " + text());

rahmen.update(zustand(3, 10, { aborted: true }));
pruefe(status().includes("beendet"), "Der Abbruch wird nicht gesagt: " + status());

// --- Eine Art, die das Telefon nicht kennt ---
neuAufbauen(zustand(1, 11, { kind: 99 }));
pruefe(wurzel.querySelector(".layout-empty")?.textContent.includes("kennt das Telefon noch nicht"),
    "Eine unbekannte Art wird nicht gesagt.");

// --- M35 C3: Vorphase einer Sonderregel - ihr Modul statt der Art, ihr Regeltext, und die Regel geht mit ---
pruefe(gesendet[0]?.specialRule === 0, "Ein Minispiel schickt eine Sonderregel mit: " + JSON.stringify(gesendet[0]));

const vorphase = (regel, durchgang, kindInfo) => zustand(1, durchgang,
    { kind: 0, specialRule: regel, title: "Sonderregel: Einsatz", rulesText: "Setze Punkte.", countdownSeconds: 0, totalRounds: 1 },
    kindInfo);

neuAufbauen(vorphase(1, 20, { max: 300 }));
rahmen.setLocked(false);
pruefe(wurzel.querySelector(".einsatz-eingabe") != null, "Die Vorphase Einsatz zeigt kein Eingabefeld.");
pruefe(runde() === "Vor der Frage", "Die Vorphase zaehlt Runden: " + runde());
pruefe(status() === "Setze Punkte.", "In der Vorphase steht der Regeltext nicht da: " + status());

wurzel.querySelector(".einsatz-eingabe").value = "120";
wurzel.querySelector(".einsatz-taste").click();
const einsatz = gesendet.at(-1);
pruefe(einsatz?.specialRule === 1 && einsatz?.kind === 0 && einsatz?.durchgang === 20 && einsatz?.payload?.amount === 120,
    "Der Einsatz geht nicht mit Regel, Durchgang und Betrag hinaus: " + JSON.stringify(einsatz));

neuAufbauen(vorphase(2, 21, {}));
rahmen.setLocked(false);
pruefe(wurzel.querySelectorAll(".risiko-taste").length === 2, "Die Vorphase Risiko zeigt nicht ja und nein.");
pruefe(wurzel.querySelector(".einsatz-eingabe") == null, "Beim Wechsel der Regel blieb das alte Modul stehen.");

rahmen.update(zustand(3, 22, { kind: 0, specialRule: 2 }, { ownDoubled: true }));
pruefe(status().includes("gleich kommt die Frage"), "Das Ende der Vorphase klingt wie ein Minispiel: " + status());

rahmen.dispose();

// --- M35 C5: ein neuer Stand nur fuer dieses Telefon (gleiche Signatur) geht an das Modul, ohne Neustart ---
const stub = {
    gestartet: 0, aktualisiert: [],
    mount() {}, starte() { this.gestartet++; }, aktualisiere(info) { this.aktualisiert.push(info); },
    leeren() {}, zeigeErgebnis() {}, zeigeEnde() {}, setLocked() {}
};
const eigen = new MinigameLayout({ submitAction: async () => {} },
    { timer: uhr.timer, now: uhr.now, module: new Map([[10, () => stub]]) });
const rechnen = (aufgabe) => ({
    resetCount: 1, currentLayoutLocked: false, allLocked: false,
    layoutInfo: { kind: 10, title: "Kopfrechnen", phase: 1, currentRound: 1, totalRounds: 1, durchgang: 30,
        countdownSeconds: 0, ownSubmitted: false, kindInfo: { taskNumber: aufgabe } }
});

eigen.render(document.createElement("div"), rechnen(1));
eigen.update(rechnen(2));
pruefe(stub.gestartet === 1, "Ein Stand mitten in der Runde hat das Modul neu gestartet: " + stub.gestartet);
pruefe(stub.aktualisiert.length === 1 && stub.aktualisiert[0].taskNumber === 2,
    "Der neue Stand erreicht das Modul nicht: " + JSON.stringify(stub.aktualisiert));
eigen.dispose();

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("minispielRahmen: alle Zusicherungen gruen.");
