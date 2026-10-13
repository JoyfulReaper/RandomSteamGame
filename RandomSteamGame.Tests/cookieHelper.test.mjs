import { readFile } from "node:fs/promises";
import test from "node:test";
import assert from "node:assert/strict";

const source = await readFile(new URL("../RandomSteamGame.Client/wwwroot/js/cookieHelper.js", import.meta.url), "utf8");
const cookies = await import(`data:text/javascript;base64,${Buffer.from(source).toString("base64")}`);

for (const [deployment, protocol] of [["Public", "https:"], ["HTTP AltNet", "http:"], ["HTTPS AltNet", "https:"]]) {
    test(`${deployment} browser cookies follow the real browser protocol`, () => {
        globalThis.window = { location: { protocol } };
        globalThis.document = { cookie: "" };
        for (const name of ["SteamId", "UnplayedOnly", "ExcludedGameIds", "OwnedGamesCacheResetAt"]) {
            cookies.setCookie(name, "saved value", 365);
            assert.equal(document.cookie.includes("; Secure"), protocol === "https:");
            assert.ok(document.cookie.includes("path=/; SameSite=Lax"));
            assert.equal(document.cookie.includes("Domain="), false);
            assert.equal(cookies.getCookie(name), "saved value");
            cookies.deleteCookie(name);
            assert.ok(document.cookie.includes("Max-Age=0"));
        }
        delete globalThis.window;
        delete globalThis.document;
    });
}
