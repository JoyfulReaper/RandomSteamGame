const LIGHT_THEME = {
    text: "#0f172a",
    heading: "#020617",
    muted: "#1e293b",
    accent: "#0369a1",
    panelBg: "rgba(255, 255, 255, 0.66)",
    panelBorder: "rgba(15, 23, 42, 0.12)",
    panelShadow: "0 18px 40px rgba(0, 0, 0, 0.18)"
};

const DARK_THEME = {
    text: "#e5e7eb",
    heading: "#ffffff",
    muted: "#cbd5e1",
    accent: "#38bdf8",
    panelBg: "rgba(17, 24, 39, 0.42)",
    panelBorder: "rgba(255, 255, 255, 0.08)",
    panelShadow: "0 18px 40px rgba(0, 0, 0, 0.28)"
};

const DEFAULT_THEME = DARK_THEME;

export async function updateTheme(rootSelector, imageUrl) {
    const root = document.querySelector(rootSelector);

    if (!root) {
        return;
    }

    let theme = DEFAULT_THEME;

    if (imageUrl) {
        try {
            const luminance = await measureAverageLuminance(imageUrl);
            theme = luminance > 150 ? LIGHT_THEME : DARK_THEME;
        } catch {
            theme = DEFAULT_THEME;
        }
    }

    applyTheme(root, theme);
}

async function measureAverageLuminance(imageUrl) {
    const response = await fetch(imageUrl, { mode: "cors", cache: "force-cache" });

    if (!response.ok) {
        throw new Error(`Image fetch failed with status ${response.status}`);
    }

    const blob = await response.blob();
    const bitmap = await createImageBitmap(blob);
    const sampleWidth = 64;
    const sampleHeight = Math.max(1, Math.round((bitmap.height / bitmap.width) * sampleWidth));
    const canvas = document.createElement("canvas");
    canvas.width = sampleWidth;
    canvas.height = sampleHeight;

    const context = canvas.getContext("2d", { willReadFrequently: true });

    if (!context) {
        throw new Error("Canvas context unavailable");
    }

    context.drawImage(bitmap, 0, 0, sampleWidth, sampleHeight);

    const { data } = context.getImageData(0, 0, sampleWidth, sampleHeight);
    let total = 0;
    let samples = 0;

    for (let i = 0; i < data.length; i += 16) {
        const r = data[i];
        const g = data[i + 1];
        const b = data[i + 2];
        total += 0.2126 * r + 0.7152 * g + 0.0722 * b;
        samples++;
    }

    return samples > 0 ? total / samples : 128;
}

function applyTheme(root, theme) {
    root.style.setProperty("--game-text-color", theme.text);
    root.style.setProperty("--game-heading-color", theme.heading);
    root.style.setProperty("--game-muted-color", theme.muted);
    root.style.setProperty("--game-accent-color", theme.accent);
    root.style.setProperty("--game-panel-bg", theme.panelBg);
    root.style.setProperty("--game-panel-border", theme.panelBorder);
    root.style.setProperty("--game-panel-shadow", theme.panelShadow);
}
