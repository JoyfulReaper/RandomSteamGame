
export async function getAuthenticatedSteamId() {
    const response = await fetch("/auth/steam-id", {
        method: "GET",
        credentials: "same-origin",
        cache: "no-store"
    });

    if (response.status === 401) {
        return null;
    }

    if (!response.ok) {
        throw new Error(`Steam identity request failed: ${response.status}`);
    }

    const data = await response.json();
    const steamId = data?.steamId;

    return typeof steamId === "string" && /^\d{17}$/.test(steamId)
        ? steamId
        : null;
}

export function consumePickerLoginReturn() {
    const url = new URL(window.location.href);

    if (url.searchParams.get("useSteamAccount") !== "1") {
        return false;
    }

    url.searchParams.delete("useSteamAccount");

    // Remove the one-time marker without reloading the page.
    window.history.replaceState(
        window.history.state,
        "",
        url.pathname + url.search + url.hash
    );

    return true;
}