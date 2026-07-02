const DEFAULT_THEME = {
    textColor: "#e5e7eb",
    headingColor: "#ffffff",
    mutedColor: "#cbd5e1",
    accentColor: "#38bdf8",
    panelBackground: "rgba(17, 24, 39, 0.4)",
    panelBorder: "rgba(255, 255, 255, 0.08)",
    panelShadow: "0 18px 40px rgba(0, 0, 0, 0.28)"
};

const LIGHT_THEME = {
    textColor: "#e2e8f0",
    headingColor: "#ffffff",
    mutedColor: "#cbd5e1",
    accentColor: "#7dd3fc",
    panelBackground: "rgba(15, 23, 42, 0.46)",
    panelBorder: "rgba(255, 255, 255, 0.12)",
    panelShadow: "0 18px 44px rgba(0, 0, 0, 0.34)"
};

const DARK_THEME = {
    textColor: "#f8fafc",
    headingColor: "#ffffff",
    mutedColor: "#e2e8f0",
    accentColor: "#38bdf8",
    panelBackground: "rgba(2, 6, 23, 0.58)",
    panelBorder: "rgba(255, 255, 255, 0.08)",
    panelShadow: "0 18px 42px rgba(0, 0, 0, 0.38)"
};

let themeRequestId = 0;

export async function updateTheme(rootSelector, imageUrl) {
    const requestId = ++themeRequestId;
    const root = document.querySelector(rootSelector);

    if (!root) {
        return;
    }

    const background = root.querySelector(".game-background");

    if (!background) {
        console.debug("RandomGamePicker: .game-background not found");
        return;
    }

    if (!imageUrl) {
        clearBackground(background);
        applyTheme(root, DEFAULT_THEME);
        return;
    }

    try {
        await preloadImage(imageUrl);

        if (requestId !== themeRequestId) {
            return;
        }

        background.style.backgroundImage = [
            "linear-gradient(90deg, rgba(0, 0, 0, 0.72), rgba(0, 0, 0, 0.48))",
            "radial-gradient(circle at center, rgba(0, 0, 0, 0.12), rgba(0, 0, 0, 0.58))",
            `url("${cssEscapeUrl(imageUrl)}")`
        ].join(", ");
        background.style.opacity = "1";
        background.style.transform = "scale(1)";

        try {
            const luminance = await measureAverageLuminance(imageUrl);

            if (requestId !== themeRequestId) {
                return;
            }

            applyTheme(root, luminance > 150 ? LIGHT_THEME : DARK_THEME);
        } catch (error) {
            console.debug("RandomGamePicker: luminance measurement failed", error);

            if (requestId !== themeRequestId) {
                return;
            }

            applyTheme(root, DEFAULT_THEME);
        }
    } catch (error) {
        if (requestId !== themeRequestId) {
            return;
        }

        console.debug("RandomGamePicker: background load failed", error);
        clearBackground(background);
        applyTheme(root, DEFAULT_THEME);
    }
}

function clearBackground(background) {
    background.style.backgroundImage = "";
    background.style.opacity = "0";
    background.style.transform = "scale(1.015)";
}

function preloadImage(imageUrl) {
    return new Promise((resolve, reject) => {
        const image = new Image();
        image.decoding = "async";
        image.onload = () => resolve(image);
        image.onerror = () => reject(new Error(`Failed to preload image: ${imageUrl}`));
        image.src = imageUrl;
    });
}

async function measureAverageLuminance(imageUrl) {
    const image = await preloadImage(imageUrl);
    const canvas = document.createElement("canvas");
    const context = canvas.getContext("2d", { willReadFrequently: true });

    if (!context) {
        throw new Error("Canvas 2D context unavailable.");
    }

    const sampleWidth = Math.max(1, Math.min(64, image.naturalWidth || image.width || 1));
    const sampleHeight = Math.max(1, Math.min(64, image.naturalHeight || image.height || 1));

    canvas.width = sampleWidth;
    canvas.height = sampleHeight;
    context.drawImage(image, 0, 0, sampleWidth, sampleHeight);

    const { data } = context.getImageData(0, 0, sampleWidth, sampleHeight);
    let luminanceTotal = 0;
    let pixelCount = 0;

    for (let index = 0; index < data.length; index += 4) {
        const alpha = data[index + 3];

        if (alpha === 0) {
            continue;
        }

        const red = data[index];
        const green = data[index + 1];
        const blue = data[index + 2];
        luminanceTotal += (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
        pixelCount++;
    }

    if (pixelCount === 0) {
        throw new Error("No visible pixels available for luminance calculation.");
    }

    return luminanceTotal / pixelCount;
}

function applyTheme(root, theme) {
    root.style.setProperty("--game-text-color", theme.textColor);
    root.style.setProperty("--game-heading-color", theme.headingColor);
    root.style.setProperty("--game-muted-color", theme.mutedColor);
    root.style.setProperty("--game-accent-color", theme.accentColor);
    root.style.setProperty("--game-panel-bg", theme.panelBackground);
    root.style.setProperty("--game-panel-border", theme.panelBorder);
    root.style.setProperty("--game-panel-shadow", theme.panelShadow);
}

function cssEscapeUrl(url) {
    return String(url).replaceAll("\\", "\\\\").replaceAll('"', '\\"');
}
