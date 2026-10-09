export async function download(url, fileName) {
    const tokenResponse = await fetch("/api/antiforgery/token", {
        mode: "same-origin",
        credentials: "same-origin",
        cache: "no-store"
    });

    if (!tokenResponse.ok) {
        return "Unable to prepare library export. Please reload the page and try again.";
    }

    const { requestToken, headerName } = await tokenResponse.json();
    const response = await fetch(url, {
        method: "POST",
        mode: "same-origin",
        credentials: "same-origin",
        cache: "no-store",
        headers: { [headerName]: requestToken }
    });

    if (!response.ok) {
        const message = (await response.text()).trim();

        return message ||
            `Library export failed with HTTP ${response.status}.`;
    }

    const blob = await response.blob();
    const objectUrl = URL.createObjectURL(blob);

    const anchor = document.createElement("a");

    anchor.href = objectUrl;
    anchor.download = fileName;
    anchor.style.display = "none";

    document.body.appendChild(anchor);

    anchor.click();
    anchor.remove();

    setTimeout(
        () => URL.revokeObjectURL(objectUrl),
        1000);

    return null;
}
