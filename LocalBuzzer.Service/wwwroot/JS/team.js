// Das Team dieses Telefons im Teamspiel (M37 T4): Symbol und Name auf der Farbe des Teams. Die Farbe setzt nur die
// Klasse team-farbe-N - styles/team.css spiegelt die Palette TeamFarben des Servers -, nie einen Farbwert aus der
// Nachricht, und nie die Farbe allein. Der Name ist frei gewaehlt und kommt nur als Text hinein.

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

    const [symbol, name] = teile(ziel);
    symbol.textContent = team.symbol ?? "";
    name.textContent = team.name ?? "";

    if (Number.isInteger(team.colorIndex) && team.colorIndex >= 0 && team.colorIndex < TEAM_FARBEN) {
        ziel.classList.add(`team-farbe-${team.colorIndex}`);
    }

    ziel.hidden = false;
}

// Symbol und Name werden einmal angelegt und danach nur beschriftet.
function teile(ziel) {
    const symbol = ziel.querySelector(".team-symbol");
    const name = ziel.querySelector(".team-name");

    if (symbol && name) return [symbol, name];

    const neuesSymbol = document.createElement("span");
    neuesSymbol.className = "team-symbol";
    neuesSymbol.setAttribute("aria-hidden", "true");

    const neuerName = document.createElement("span");
    neuerName.className = "team-name";

    ziel.append(neuesSymbol, neuerName);
    return [neuesSymbol, neuerName];
}
