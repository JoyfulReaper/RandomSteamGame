export function getCookie(name) {
    const encodedName = encodeURIComponent(name);
    const cookies = document.cookie ? document.cookie.split("; ") : [];

    for (const cookie of cookies) {
        const separatorIndex = cookie.indexOf("=");
        const key = separatorIndex >= 0 ? cookie.slice(0, separatorIndex) : cookie;
        const value = separatorIndex >= 0 ? cookie.slice(separatorIndex + 1) : "";

        if (key === encodedName) {
            return decodeURIComponent(value);
        }
    }

    return "";
}

export function setCookie(name, value, days) {
    let expires = "";
    if (days) {
        const date = new Date();
        date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
        expires = "; expires=" + date.toUTCString();
    }

    const secure = window.location.protocol === "https:" ? "; Secure" : "";
    document.cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value || "")}${expires}; path=/; SameSite=Lax${secure}`;
}

export function deleteCookie(name) {
    document.cookie = `${encodeURIComponent(name)}=; Max-Age=0; path=/; SameSite=Lax`;
}
