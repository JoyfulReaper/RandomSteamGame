import assert from "node:assert/strict";
import { test } from "node:test";
import { download } from "../../RandomSteamGame.Client/wwwroot/js/libraryExport.js";

const exportUrl = "/api/steam/76561197960287930/library/export.csv";

test("download bootstraps a request token and sends a same-origin POST before saving the blob", async t => {
    const calls = [];
    const blob = new Blob(["game,id\r\nPortal,400\r\n"]);
    t.mock.method(globalThis, "fetch", async (url, options) => {
        calls.push({ url, options });
        return calls.length === 1
            ? { ok: true, json: async () => ({ requestToken: "framework-token", headerName: "RequestVerificationToken" }) }
            : { ok: true, blob: async () => blob };
    });
    let clicked = false, removed = false, appended = false;
    const anchor = { style: {}, click: () => clicked = true, remove: () => removed = true };
    globalThis.document = {
        createElement: tag => { assert.equal(tag, "a"); return anchor; },
        body: { appendChild: element => { assert.equal(element, anchor); appended = true; } }
    };
    t.after(() => delete globalThis.document);
    t.mock.method(URL, "createObjectURL", value => { assert.equal(value, blob); return "blob:export"; });
    let revoked;
    t.mock.method(URL, "revokeObjectURL", value => revoked = value);
    t.mock.method(globalThis, "setTimeout", (callback, delay) => { assert.equal(delay, 1000); callback(); });

    assert.equal(await download(exportUrl, "steam-library-76561197960287930.csv"), null);
    assert.equal(calls[0].url, "/api/antiforgery/token");
    assert.equal(calls[1].url, exportUrl);
    assert.equal(calls[1].options.method, "POST");
    assert.deepEqual(calls[1].options.headers, { RequestVerificationToken: "framework-token" });
    for (const { options } of calls) {
        assert.equal(options.mode, "same-origin");
        assert.equal(options.credentials, "same-origin");
        assert.equal(options.cache, "no-store");
    }
    assert.equal(anchor.href, "blob:export");
    assert.equal(anchor.download, "steam-library-76561197960287930.csv");
    assert.ok(appended && clicked && removed);
    assert.equal(revoked, "blob:export");
});

test("token bootstrap failure does not attempt an export", async t => {
    let calls = 0;
    t.mock.method(globalThis, "fetch", async () => { calls++; return { ok: false }; });
    assert.match(await download(exportUrl, "export.csv"), /Unable to prepare library export/);
    assert.equal(calls, 1);
});

test("POST errors remain available to the export form", async t => {
    let calls = 0;
    t.mock.method(globalThis, "fetch", async () => ++calls === 1
        ? { ok: true, json: async () => ({ requestToken: "framework-token", headerName: "RequestVerificationToken" }) }
        : { ok: false, status: 400, text: async () => " Library export verification failed. " });
    assert.equal(await download(exportUrl, "export.csv"), "Library export verification failed.");
    assert.equal(calls, 2);
});
