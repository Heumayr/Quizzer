// Die erste automatische Pruefung der Telefonseite.
//
// Gemessen 2026-09-07: nach "Runde zuruecksetzen" blieb der Bestaetigen-Knopf eines
// Spielers, der schon abgegeben hatte, FUER IMMER gesperrt - er trug weiter
// "Abgegeben". Die Ursache: das Layout baut sich nur neu auf, wenn sich seine Kennung
// aendert, und die enthielt den Ruecksetzstand nicht.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { KeySelectLayout } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/layouts/keySelectLayout.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;

    console.error("ROT: " + satz);
    fehler++;
}

function zustand(resetCount, gesperrt = false) {
    return {
        playerId: "11111111-1111-1111-1111-111111111111",
        resetCount,
        currentLayoutLocked: gesperrt,
        allLocked: false,
        layoutInfo: {
            questionId: "22222222-2222-2222-2222-222222222222",
            keysAndDesignations: { A: "Klavier", B: "Geige" },
            showDesignations: true,
            maxAllowedSelections: 1
        }
    };
}

const layout = new KeySelectLayout({ submitSelection: async () => {} });

layout.render(wurzel, zustand(1));

const tasten = () => wurzel.querySelectorAll(".key-btn");
const knopf = () => wurzel.querySelector(".commit-btn");

// --- Abgeben ---
tasten()[0].click();
pruefe(knopf().disabled === false, "Nach der Auswahl ist der Bestaetigen-Knopf gesperrt.");

await layout.handleCommitClick();

pruefe(knopf().disabled === true, "Nach der Abgabe ist der Knopf nicht gesperrt.");
pruefe(knopf().textContent === "Abgegeben ✓",
    "Nach der Abgabe steht nicht 'Abgegeben' auf dem Knopf: " + knopf().textContent);

// --- Die Gegenrichtung zuerst: derselbe Stand darf NICHT entsperren ---
layout.update(zustand(1));

pruefe(knopf().disabled === true,
    "Ohne Zuruecksetzen wurde wieder entsperrt - dann kann derselbe Spieler zweimal abgeben.");

// --- M37 P8, P6-5: nach "Abgegeben ✓" folgen die Tasten dem Knopf - ein Klick aendert die sichtbare Auswahl nicht mehr ---
pruefe(tasten().every(t => t.disabled === true),
    "Nach der Abgabe sind die Antworttasten mit dem Stand der offenen Runde wieder frei.");

tasten()[0].click(); // die abgegebene Taste - frei hiesse das: abgewaehlt

pruefe(tasten()[0].classList.contains("selected"),
    "Nach der Abgabe aendert ein Klick die sichtbare Auswahl, abgegeben bleibt die alte.");

// --- Zuruecksetzen ---
layout.update(zustand(2));

pruefe(knopf().textContent === "Auswahl bestätigen",
    "Nach dem Zuruecksetzen steht weiter 'Abgegeben' auf dem Knopf: " + knopf().textContent);

pruefe(layout.submitted === false,
    "Nach dem Zuruecksetzen gilt der Spieler weiter als abgegeben.");

pruefe(tasten().every(t => t.disabled === false),
    "Nach dem Zuruecksetzen sind die Antworttasten gesperrt.");

tasten()[1].click();

pruefe(knopf().disabled === false,
    "Nach dem Zuruecksetzen laesst sich nicht erneut abgeben - der Spieler ist fuer diese "
    + "Frage aus dem Spiel, und die Runde kann nie mehr schliessen.");

// --- Reihenfolgefrage (M12): die Klickfolge ist die Antwort, abgegeben wird erst komplett ---
{
    const wurzel2 = baueDom().wurzel;
    let gesendet = null;

    const reihe = new KeySelectLayout({ submitSelection: async p => { gesendet = p; } });

    reihe.render(wurzel2, {
        playerId: "11111111-1111-1111-1111-111111111111",
        resetCount: 1,
        currentLayoutLocked: false,
        allLocked: false,
        layoutInfo: {
            questionId: "33333333-3333-3333-3333-333333333333",
            keysAndDesignations: { A: "Steinzeit", B: "Mittelalter", C: "Antike" },
            showDesignations: true,
            maxAllowedSelections: 3,
            ordered: true
        }
    });

    const t = () => wurzel2.querySelectorAll(".key-btn");
    const k = () => wurzel2.querySelector(".commit-btn");

    pruefe(k().textContent === "Reihenfolge bestätigen",
        "Bei der Reihenfolge steht nicht 'Reihenfolge bestätigen' auf dem Knopf: " + k().textContent);

    t()[0].click(); // A -> 1
    t()[2].click(); // C -> 2

    pruefe(k().disabled === true,
        "Mit zwei von drei Elementen laesst sich schon abgeben - der dritte Platz bliebe leer.");
    pruefe(t()[2].textContent.startsWith("2. "),
        "Das zweite angetippte Element zeigt nicht seinen Platz: " + t()[2].textContent);

    t()[0].click(); // A abwaehlen -> C rutscht auf 1
    pruefe(t()[2].textContent.startsWith("1. "),
        "Nach dem Abwaehlen ruecken die Plaetze nicht nach: " + t()[2].textContent);
    pruefe(!t()[0].textContent.startsWith("1. ") && !t()[0].textContent.startsWith("2. "),
        "Ein abgewaehltes Element traegt noch einen Platz: " + t()[0].textContent);

    t()[0].click(); // A -> 2
    t()[1].click(); // B -> 3

    pruefe(k().disabled === false, "Mit allen drei Elementen laesst sich nicht abgeben.");

    await reihe.handleCommitClick();

    pruefe(gesendet && gesendet.selectedKeys.join("") === "CAB",
        "Gesendet wird nicht die Klickfolge C, A, B: " + JSON.stringify(gesendet?.selectedKeys));
}

// --- Zuordnung (M12): das Telefon sagt, zu welchem Begriff gerade gewaehlt wird ---
{
    const wurzel3 = baueDom().wurzel;
    let gesendet = null;

    const paare = new KeySelectLayout({ submitSelection: async p => { gesendet = p; } });

    paare.render(wurzel3, {
        playerId: "11111111-1111-1111-1111-111111111111",
        resetCount: 1,
        currentLayoutLocked: false,
        allLocked: false,
        layoutInfo: {
            questionId: "44444444-4444-4444-4444-444444444444",
            keysAndDesignations: { A: "Bern", B: "Wien", C: "Berlin" },
            showDesignations: true,
            maxAllowedSelections: 3,
            ordered: true,
            prompts: ["Österreich", "Deutschland", "Schweiz"]
        }
    });

    const t = () => wurzel3.querySelectorAll(".key-btn");
    const zeile = () => wurzel3.querySelector(".prompt-line");

    pruefe(wurzel3.querySelector(".commit-btn").textContent === "Zuordnung bestätigen",
        "Bei der Zuordnung steht nicht 'Zuordnung bestaetigen' auf dem Knopf.");
    pruefe(zeile() && !zeile().hidden && zeile().textContent.includes("1 · Österreich"),
        "Die Telefonseite sagt nicht, dass zuerst zu 'Oesterreich' gewaehlt wird: " + zeile()?.textContent);

    t()[1].click(); // Wien zu 1

    pruefe(zeile().textContent.includes("2 · Deutschland"),
        "Nach dem ersten Tipp springt die Ansage nicht auf Begriff 2: " + zeile().textContent);

    t()[2].click(); // Berlin zu 2
    t()[0].click(); // Bern zu 3

    pruefe(zeile().textContent.includes("bestätigen"),
        "Nach dem letzten Tipp fordert die Seite nicht zum Bestaetigen auf: " + zeile().textContent);

    await paare.handleCommitClick();

    pruefe(gesendet && gesendet.selectedKeys.join("") === "BCA",
        "Gesendet wird nicht B, C, A: " + JSON.stringify(gesendet?.selectedKeys));
}

// --- Wahr oder falsch (M12): je Aussage zwei Knoepfe, abgegeben wird erst komplett ---
{
    const wurzel4 = baueDom().wurzel;
    let gesendet = null;

    const tf = new KeySelectLayout({ submitSelection: async p => { gesendet = p; } });

    tf.render(wurzel4, {
        playerId: "11111111-1111-1111-1111-111111111111",
        resetCount: 1,
        currentLayoutLocked: false,
        allLocked: false,
        layoutInfo: {
            questionId: "55555555-5555-5555-5555-555555555555",
            keysAndDesignations: { A: "Wale sind Fische.", B: "Wien liegt an der Donau.", C: "2 + 2 = 4" },
            showDesignations: true,
            maxAllowedSelections: 3,
            trueFalse: true
        }
    });

    const k = () => wurzel4.querySelector(".commit-btn");
    const wahr = key => wurzel4.querySelectorAll(".tf-true").find(b => b.dataset.key === key);
    const falsch = key => wurzel4.querySelectorAll(".tf-false").find(b => b.dataset.key === key);

    pruefe(wurzel4.querySelectorAll(".tf-row").length === 3, "Es steht nicht je Aussage eine Zeile da.");
    pruefe(k().textContent === "Urteile abgeben", "Der Knopf heisst nicht 'Urteile abgeben': " + k().textContent);

    falsch("A").click();
    wahr("B").click();

    pruefe(k().disabled === true, "Mit zwei von drei Urteilen laesst sich schon abgeben.");

    wahr("C").click();
    wahr("A").click();   // umentschieden
    falsch("A").click(); // und zurueck

    pruefe(k().disabled === false, "Mit allen drei Urteilen laesst sich nicht abgeben.");
    pruefe(falsch("A").classList.contains("selected") && !wahr("A").classList.contains("selected"),
        "Nach dem Umentscheiden sind beide Knoepfe einer Aussage gewaehlt.");

    await tf.handleCommitClick();

    pruefe(gesendet && gesendet.selectedKeys.join("") === "BC",
        "Gesendet werden nicht die als wahr beurteilten B und C: " + JSON.stringify(gesendet?.selectedKeys));
}

// --- M37 P8, P6-5: kommt der Stand der offenen Runde, bevor der Aufruf zurueckkehrt, steht die Auswahl nach der Abgabe trotzdem fest ---
for (const trueFalse of [false, true]) {
    const wurzel5 = baueDom().wurzel;
    const offen = () => {
        const z = zustand(1);
        return { ...z, layoutInfo: { ...z.layoutInfo, trueFalse, maxAllowedSelections: trueFalse ? 2 : 1 } };
    };
    const frueh = new KeySelectLayout({ submitSelection: async () => frueh.update(offen()) });
    frueh.render(wurzel5, offen());

    const t = () => wurzel5.querySelectorAll(".key-btn");
    const art = trueFalse ? "Wahr oder falsch" : "Auswahl";

    // Auswahl: A. Wahr oder falsch: A wahr, B falsch - die Knoepfe stehen je Aussage als wahr, falsch.
    t()[0].click();
    if (trueFalse) t()[3].click();

    await frueh.handleCommitClick();
    pruefe(frueh.submitted === true, `${art}: die Abgabe gilt nicht - der Fall misst nichts.`);

    t()[trueFalse ? 1 : 0].click(); // frei hiesse das: A abgewaehlt bzw. A falsch

    pruefe(t().every(x => x.disabled === true), `${art}: kam der Stand vor der Rueckkehr, sind die Tasten nach der Abgabe frei.`);
    pruefe(t()[0].classList.contains("selected"), `${art}: nach der Abgabe aendert ein Klick die sichtbare Auswahl.`);
}

// --- P10-4 (M37 P11): die Stimme nennt die Runde, die das Telefon gerade sieht - eine alte verwirft der Server ---
{
    const wurzel5 = baueDom().wurzel;
    const gesendet = [];
    const runde = new KeySelectLayout({ submitSelection: async p => { gesendet.push(p); } });
    const taste = () => wurzel5.querySelectorAll(".key-btn")[0];

    runde.render(wurzel5, zustand(7));
    taste().click();
    await runde.handleCommitClick();

    runde.update(zustand(8)); // dieselbe Frage: die Wahl bleibt vorgewählt
    if (!taste().classList.contains("selected")) taste().click();
    await runde.handleCommitClick();

    pruefe(gesendet.map(p => p.resetCount).join(",") === "7,8",
        "Die Stimme nennt nicht die Runde, die das Telefon sah (resetCount): " + JSON.stringify(gesendet));

    // Kommt die neue Runde, bevor der Aufruf zurück ist, zählt die Wahl dort nicht - die Tasten bleiben frei.
    const wurzel6 = baueDom().wurzel;
    const spaet = new KeySelectLayout({ submitSelection: async () => spaet.update(zustand(9)) });
    spaet.render(wurzel6, zustand(8));
    wurzel6.querySelectorAll(".key-btn")[0].click();
    await spaet.handleCommitClick();

    pruefe(spaet.submitted === false, "Nach einer neuen Runde während des Sendens gilt die Wahl als abgegeben - der Server hat nichts.");
    pruefe([...wurzel6.querySelectorAll(".key-btn")].every(x => x.disabled === false),
        "Nach einer neuen Runde während des Sendens sind die Tasten gesperrt.");
}

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("keySelectLayout: alle Zusicherungen gruen.");
