
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


export async function getSteamLoginStatus() {
    const response = await fetch("/auth/me", {
        credentials: "same-origin",
        cache: "no-store"
    });

    if (!response.ok) {
        throw new Error(`Authentication status failed: ${response.status}`);
    }

    const data = await response.json();

    return {
        authenticated: data.authenticated === true,
        loginAvailable: data.loginAvailable === true
    };
}

export async function signOutOfSteam() {
    const tokenResponse = await fetch("/auth/logout-token", {
        credentials: "same-origin",
        cache: "no-store"
    });

    if (!tokenResponse.ok) {
        throw new Error(`Logout token request failed: ${tokenResponse.status}`);
    }

    const { requestToken } = await tokenResponse.json();

    if (!requestToken) {
        throw new Error("Missing logout verification token");
    }

    const response = await fetch("/auth/logout", {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        headers: {
            "RequestVerificationToken": requestToken
        }
    });

    if (!response.ok) {
        throw new Error(`Logout failed: ${response.status}`);
    }

    window.location.assign("/");
}
