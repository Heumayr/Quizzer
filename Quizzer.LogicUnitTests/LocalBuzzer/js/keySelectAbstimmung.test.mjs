// Das Tasten-Layout in der Team-Abstimmung (M37 P6, R2-3): es zeigt die Stimme, die der Server zaehlt.
//
// Am Code belegt: eine an der Grenze (5 Stimmen je 10 s) verworfene Stimme meldete "Abgegeben ✓" mit der neuen
// Auswahl, gezaehlt blieb die alte. Der Hub schickt seinen Stand, bevor der Aufruf zurueckkommt - der Ersatz hier auch.

import { baueDom } from "./dom-ersatz.mjs";

baueDom();

const { KeySelectLayout } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/layouts/keySelectLayout.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;

    console.error("ROT: " + satz);
    fehler++;
}

function zustand(teamVote) {
    return {
        playerId: "11111111-1111-1111-1111-111111111111",
        resetCount: 1,
        currentLayoutLocked: false,
        allLocked: false,
        teamVote,
        layoutInfo: {
            questionId: "66666666-6666-6666-6666-666666666666",
            keysAndDesignations: { A: "Rom", B: "Paris", C: "Wien" },
            showDesignations: true,
            maxAllowedSelections: 1
        }
    };
}

const laeuft = own => ({ own, voted: own.length > 0 ? 1 : 0, of: 2, closed: false, result: "" });

// Ein Telefon, dessen Server vor dem Rueckkehren des Aufrufs `antwort` als eigene Stimme meldet.
function telefon(antwort, start = laeuft([])) {
    const { wurzel } = baueDom();
    const toasts = [];
    globalThis.window.show_toast = (art, text) => toasts.push({ art, text });

    const layout = new KeySelectLayout({ submitSelection: async () => layout.update(zustand(laeuft(antwort))) });
    layout.render(wurzel, zustand(start));

    const taste = key => wurzel.querySelectorAll(".key-btn").find(b => b.dataset.key === key);
    const gewaehlt = () => wurzel.querySelectorAll(".key-btn").filter(b => b.classList.contains("selected")).map(b => b.dataset.key).join("");
    const knopf = () => wurzel.querySelector(".commit-btn");

    return { layout, taste, gewaehlt, knopf, toasts };
}

// --- Wiederkommen mitten in der Abstimmung: das Telefon zeigt die gezaehlte Stimme ---
{
    const t = telefon(["A"], laeuft(["A"]));

    pruefe(t.gewaehlt() === "A" && t.knopf().textContent === "Abgegeben ✓" && t.knopf().disabled === true,
        "Beim Wiederkommen zeigt das Telefon die gezaehlte Stimme nicht: " + t.gewaehlt() + " / " + t.knopf().textContent);

    // M37 P8, P6-5: die Tasten folgen dem Knopf.
    t.taste("A").click();
    pruefe(t.taste("A").disabled === true && t.gewaehlt() === "A",
        "Beim Wiederkommen laesst sich die gezaehlte Stimme sichtbar umwaehlen: " + t.gewaehlt());
}

// --- Verworfen: es zaehlt weiter A (aus einer frueheren Seite), das Telefon zeigt A und sagt es ---
{
    const t = telefon(["A"]);

    t.taste("B").click();
    await t.layout.handleCommitClick();

    pruefe(t.gewaehlt() === "A", "Nach der verworfenen Stimme steht B gewaehlt, gezaehlt ist A: " + t.gewaehlt());
    pruefe(t.knopf().disabled === true, "Nach der verworfenen Stimme laesst sich noch einmal abgeben, obwohl A zaehlt.");
    pruefe(t.toasts.some(x => x.art === "warning" && x.text.endsWith(": A")),
        "Das Telefon sagt nicht, dass weiter A zaehlt: " + JSON.stringify(t.toasts));
    pruefe(!t.toasts.some(x => x.art === "info"), "Das Telefon meldet die verworfene Stimme als uebermittelt.");
}

// --- Verworfen ohne gezaehlte Stimme: das Telefon gibt wieder frei ---
{
    const t = telefon([]);

    t.taste("C").click();
    await t.layout.handleCommitClick();

    pruefe(t.knopf().textContent !== "Abgegeben ✓", "Ohne gezaehlte Stimme steht 'Abgegeben ✓' da.");
    pruefe(t.knopf().disabled === false, "Ohne gezaehlte Stimme laesst sich nicht noch einmal abgeben.");
    pruefe(t.taste("C").disabled === false, "Ohne gezaehlte Stimme bleiben die Tasten gesperrt.");
}

// --- Still verworfen, ohne Stand dazwischen: das Telefon bleibt nicht gesperrt stehen ---
{
    const { wurzel } = baueDom();
    const layout = new KeySelectLayout({ submitSelection: async () => {} });
    layout.render(wurzel, zustand(laeuft([])));

    wurzel.querySelectorAll(".key-btn")[2].click();
    await layout.handleCommitClick();

    pruefe(wurzel.querySelector(".commit-btn").disabled === false,
        "Ohne Stand und ohne gezaehlte Stimme bleibt das Telefon gesperrt stehen.");
}

// --- Angenommen: unveraendert "Abgegeben ✓" und "uebermittelt" ---
{
    const t = telefon(["B"]);

    t.taste("B").click();
    await t.layout.handleCommitClick();

    pruefe(t.gewaehlt() === "B" && t.knopf().textContent === "Abgegeben ✓",
        "Die angenommene Stimme steht nicht als abgegeben da: " + t.gewaehlt() + " / " + t.knopf().textContent);
    pruefe(t.toasts.some(x => x.art === "info") && !t.toasts.some(x => x.art === "warning"),
        "Die angenommene Stimme meldet etwas anderes als 'uebermittelt': " + JSON.stringify(t.toasts));

    // M37 P8, P6-5: T7 sah kein Umstimmen am Telefon vor - nach "Abgegeben ✓" sind die Tasten gesperrt.
    t.taste("B").click();
    pruefe(t.taste("B").disabled === true && t.gewaehlt() === "B",
        "Nach 'Abgegeben ✓' laesst sich die Stimme sichtbar umwaehlen, gezaehlt bleibt B: " + t.gewaehlt());
}

// --- Ohne Abstimmung bleibt alles wie vorher ---
{
    const { wurzel } = baueDom();
    const layout = new KeySelectLayout({ submitSelection: async () => {} });
    layout.render(wurzel, zustand(null));

    wurzel.querySelectorAll(".key-btn")[1].click();
    await layout.handleCommitClick();

    pruefe(wurzel.querySelector(".commit-btn").textContent === "Abgegeben ✓", "Ohne Abstimmung steht nicht 'Abgegeben ✓' da.");
}

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("keySelectAbstimmung: alle Zusicherungen gruen.");
