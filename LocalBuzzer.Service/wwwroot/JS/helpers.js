import { COOKIE_DAYS, COOKIE_NAME } from "./constants.js";

export const dom = {
    name: document.getElementById("name"),
    status: document.getElementById("status"),
    error: document.getElementById("error_display"),
    host: document.getElementById("layout_host")
};

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

export function toast(type, msg) {
    if (typeof window.show_toast === "function") {
        window.show_toast(type, msg);
    }
}