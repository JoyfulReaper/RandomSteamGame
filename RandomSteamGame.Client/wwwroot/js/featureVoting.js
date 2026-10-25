const requestOptions = { mode: "same-origin", credentials: "same-origin", cache: "no-store" };

function result(response, proposals = null) {
    const value = Number(response.headers?.get("Retry-After"));
    return {
        status: response.status,
        proposals,
        retryAfterSeconds: Number.isFinite(value) && value > 0 ? Math.min(Math.ceil(value), 86400) : null
    };
}

export async function load() {
    try {
        const response = await fetch("/api/features", requestOptions);
        if (!response.ok) return result(response);
        const proposals = await response.json();
        if (!Array.isArray(proposals)) return { status: 0 };
        return result(response, proposals);
    } catch {
        return { status: 0 };
    }
}

export async function setSupported(id, supported) {
    if (!Number.isSafeInteger(id) || id <= 0 || typeof supported !== "boolean") return { status: 400 };
    try {
        const tokenResponse = await fetch("/api/antiforgery/token", requestOptions);
        if (!tokenResponse.ok) return result(tokenResponse);
        const { requestToken, headerName } = await tokenResponse.json();
        if (typeof requestToken !== "string" || !requestToken || typeof headerName !== "string" || !headerName)
            return { status: 0 };
        return result(await fetch(`/api/features/${id}/vote`, {
            ...requestOptions,
            method: "POST",
            headers: { "Content-Type": "application/json", [headerName]: requestToken },
            body: JSON.stringify({ supported })
        }));
    } catch {
        // A failed response doesn't prove that the server rejected the write. Reload before retrying.
        return { status: 0 };
    }
}

// Native disabled controls and sorting can drop keyboard focus after a request.
export function focusVote(id) {
    const button = document.getElementById(`feature-vote-${id}`);
    const target = button && !button.disabled ? button : document.getElementById(`feature-title-${id}`);
    target?.focus({ preventScroll: true });
}
