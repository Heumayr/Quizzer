// Das Team dieses Telefons im Teamspiel (M37 T4): Symbol und Name auf der Farbe des Teams. Die Farbe setzt nur die
// Klasse team-farbe-N - styles/team.css spiegelt die Palette TeamFarben des Servers -, nie einen Farbwert aus der
// Nachricht, und nie die Farbe allein. Name und Sprecher (M37 T5) sind frei gewaehlt und kommen nur als Text hinein.
// M37 T7: darunter die Team-Abstimmung dieses Telefons (abstimmung.js).

import { zeigeAbstimmung } from "./abstimmung.js";

// So viele Plaetze hat die Palette; ClientStateContractUnitTests haelt die Zahl gleich mit TeamFarben.
export const TEAM_FARBEN = 8;

export function zeigeTeam(ziel, team) {
    if (!ziel) return;

    for (let i = 0; i < TEAM_FARBEN; i++) {
        ziel.classList.remove(`team-farbe-${i}`);
    }

    if (!team) {
        ziel.hidden = true;
        return;
    }

    const [symbol, name, sprecher, abstimmung] = teile(ziel);
    symbol.textContent = team.symbol ?? "";
    name.textContent = team.name ?? "";
    sprecher.textContent = team.speaker ? `Sprecher: ${team.speaker}` : "";
    sprecher.hidden = !team.speaker;
    zeigeAbstimmung(abstimmung, team.vote ?? null);

    if (Number.isInteger(team.colorIndex) && team.colorIndex >= 0 && team.colorIndex < TEAM_FARBEN) {
        ziel.classList.add(`team-farbe-${team.colorIndex}`);
    }

    ziel.hidden = false;
}

// Symbol, Name, Sprecher und Abstimmung werden einmal angelegt und danach nur beschriftet.
function teile(ziel) {
    const symbol = ziel.querySelector(".team-symbol") ?? haenge(ziel, "team-symbol");
    symbol.setAttribute("aria-hidden", "true");

    return [symbol, ...["team-name", "team-sprecher", "team-abstimmung"].map(klasse => ziel.querySelector(`.${klasse}`) ?? haenge(ziel, klasse))];
}

function haenge(ziel, klasse) {
    const teil = document.createElement("span");
    teil.className = klasse;
    ziel.append(teil);
    return teil;
}
