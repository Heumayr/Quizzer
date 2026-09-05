import { COOKIE_DAYS, COOKIE_NAME } from "./constants.js";

export const dom = {
    name: document.getElementById("name"),
    connection: document.getElementById("connection"),
    status: document.getElementById("status"),
    error: document.getElementById("error_display"),
    host: document.getElementById("layout_host")
};

// Wartezeiten fuer die ersten Wiederverbindungsversuche; danach gilt RETRY_DELAY_STEADY_MS ohne Ende.
const RETRY_DELAYS_MS = [0, 1000, 2000, 5000];
const RETRY_DELAY_STEADY_MS = 10000;

const CONNECTION_LABELS = Object.freeze({
    connected: "● verbunden",
    reconnecting: "○ verbinde neu …",
    disconnected: "○ getrennt"
});

export function setCookie(name, value, days = COOKIE_DAYS) {
    const expires = new Date(Date.now() + days * 864e5).toUTCString();
    document.cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value)}; Expires=${expires}; Path=/; SameSite=Lax`;
}

export function getCookie(name) {
    const key = encodeURIComponent(name) + "=";
    return document.cookie
        .split(";")
        .map(s => s.trim())
        .find(s => s.startsWith(key))
        ?.slice(key.length) ?? null;
}

export function isGuid(s) {
    return typeof s === "string" &&
        /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(s.trim());
}

export function resolvePlayerId() {
    const params = new URLSearchParams(location.search);
    const urlIdRaw = params.get("id");
    const urlId = urlIdRaw?.trim() ?? null;

    if (urlId && isGuid(urlId)) {
        setCookie(COOKIE_NAME, urlId, COOKIE_DAYS);
        return urlId;
    }

    const cookieIdRaw = getCookie(COOKIE_NAME);
    const cookieId = cookieIdRaw ? decodeURIComponent(cookieIdRaw).trim() : null;

    if (cookieId && isGuid(cookieId)) {
        return cookieId;
    }

    const input = prompt("Player-ID (GUID) fehlt. Bitte einfügen:", "");
    const typed = input?.trim() ?? "";

    if (typed && isGuid(typed)) {
        setCookie(COOKIE_NAME, typed, COOKIE_DAYS);
        return typed;
    }

    return null;
}

export function showError(msg) {
    dom.error.textContent = msg ?? "";
    dom.error.classList.toggle("show", !!msg);
}

export function getErrMsg(e) {
    const m = e?.message ?? String(e ?? "");
    const match = m.match(/HubException:\s*(.*)$/);
    return (match ? match[1] : m).trim();
}

export function toast(type, msg, options) {
    if (typeof window.show_toast === "function") {
        window.show_toast(type, msg, options);
    }
}

// Rueckfallregel fuer withAutomaticReconnect: liefert nie null, versucht es also ohne Ende weiter.
export function nextRetryDelay(retryContext) {
    const count = retryContext?.previousRetryCount ?? 0;
    return count < RETRY_DELAYS_MS.length ? RETRY_DELAYS_MS[count] : RETRY_DELAY_STEADY_MS;
}

// Verbindungsanzeige in der Kopfzeile: Punkt und Wort, nicht nur Farbe.
export function setConnectionIndicator(kind) {
    const key = CONNECTION_LABELS[kind] ? kind : "disconnected";
    if (!dom.connection) return;
    dom.connection.textContent = CONNECTION_LABELS[key];
    dom.connection.dataset.state = key;
}
