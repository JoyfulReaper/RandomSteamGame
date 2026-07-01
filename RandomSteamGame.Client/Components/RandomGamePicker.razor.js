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
    const sampleWidth = 96;
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
    const regionSamples = [];
    const regions = [
        { x: 0.16, y: 0.16, weight: 0.75 },
        { x: 0.5, y: 0.16, weight: 1 },
        { x: 0.84, y: 0.16, weight: 1.35 },
        { x: 0.16, y: 0.5, weight: 0.85 },
        { x: 0.5, y: 0.5, weight: 1.15 },
        { x: 0.84, y: 0.5, weight: 1.7 },
        { x: 0.16, y: 0.84, weight: 0.75 },
        { x: 0.5, y: 0.84, weight: 1 },
        { x: 0.84, y: 0.84, weight: 1.35 }
    ];

    const patchWidth = Math.max(4, Math.round(sampleWidth * 0.18));
    const patchHeight = Math.max(4, Math.round(sampleHeight * 0.18));

    for (const region of regions) {
        const startX = clamp(Math.round(region.x * sampleWidth - patchWidth / 2), 0, sampleWidth - 1);
        const startY = clamp(Math.round(region.y * sampleHeight - patchHeight / 2), 0, sampleHeight - 1);
        const endX = Math.min(sampleWidth, startX + patchWidth);
        const endY = Math.min(sampleHeight, startY + patchHeight);
        regionSamples.push({
            luminance: measureRegionLuminance(data, sampleWidth, startX, startY, endX, endY),
            weight: region.weight
        });
    }

    const weightedSamples = regionSamples
        .sort((a, b) => a.luminance - b.luminance)
        .filter((sample) => Number.isFinite(sample.luminance) && Number.isFinite(sample.weight));

    if (weightedSamples.length === 0) {
        return 128;
    }

    if (weightedSamples.length <= 3) {
        return weightedAverage(weightedSamples);
    }

    const trimmed = weightedSamples.slice(1, -1);
    return trimmed.length > 0 ? weightedAverage(trimmed) : weightedAverage(weightedSamples);
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

function measureRegionLuminance(data, width, startX, startY, endX, endY) {
    let total = 0;
    let samples = 0;

    for (let y = startY; y < endY; y += 2) {
        for (let x = startX; x < endX; x += 2) {
            const index = (y * width + x) * 4;
            const alpha = data[index + 3];

            if (alpha === 0) {
                continue;
            }

            const r = data[index];
            const g = data[index + 1];
            const b = data[index + 2];
            total += 0.2126 * r + 0.7152 * g + 0.0722 * b;
            samples++;
        }
    }

    return samples > 0 ? total / samples : 128;
}

function average(values) {
    if (values.length === 0) {
        return 128;
    }

    return values.reduce((total, value) => total + value, 0) / values.length;
}

function weightedAverage(values) {
    if (values.length === 0) {
        return 128;
    }

    let total = 0;
    let weightTotal = 0;

    for (const sample of values) {
        total += sample.luminance * sample.weight;
        weightTotal += sample.weight;
    }

    return weightTotal > 0 ? total / weightTotal : 128;
}

function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
}
