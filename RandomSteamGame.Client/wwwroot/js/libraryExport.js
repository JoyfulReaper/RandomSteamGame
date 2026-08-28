export async function download(url, fileName) {
    const response = await fetch(url, {
        credentials: "same-origin",
        cache: "no-store"
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