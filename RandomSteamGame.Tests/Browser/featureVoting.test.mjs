import assert from "node:assert/strict";
import { test } from "node:test";
import { load, setSupported, focusVote } from "../../RandomSteamGame.Client/wwwroot/js/featureVoting.js";

const response = (status, payload = null, retry = null) => ({
    status, ok: status >= 200 && status < 300,
    headers: new Headers(retry ? { "Retry-After": retry } : {}),
    json: async () => payload
});

test("load uses browser cookies and no-store, returning selections or an empty list", async t => {
    for (const proposals of [[], [{ id: 1, title: "Favorites", supported: true, voteCount: 3 }]]) {
        t.mock.method(globalThis, "fetch", async (url, options) => {
            assert.equal(url, "/api/features");
            assert.deepEqual(options, { mode: "same-origin", credentials: "same-origin", cache: "no-store" });
            return response(200, proposals);
        });
        assert.deepEqual((await load()).proposals, proposals);
    }
});

for (const supported of [true, false]) {
    test(`a ${supported ? "vote" : "withdrawal"} bootstraps antiforgery and uses its returned header`, async t => {
        const calls = [];
        t.mock.method(globalThis, "fetch", async (url, options) => {
            calls.push({ url, options });
            return calls.length === 1
                ? response(200, { headerName: "X-Dynamic-Verification", requestToken: "secret-token" })
                : response(204);
        });
        assert.equal((await setSupported(12, supported)).status, 204);
        assert.equal(calls.length, 2);
        assert.equal(calls[0].url, "/api/antiforgery/token");
        assert.equal(calls[1].url, "/api/features/12/vote");
        assert.equal(calls[1].options.method, "POST");
        assert.deepEqual(calls[1].options.headers, { "Content-Type": "application/json", "X-Dynamic-Verification": "secret-token" });
        assert.deepEqual(JSON.parse(calls[1].options.body), { supported });
        for (const { options } of calls) {
            assert.equal(options.credentials, "same-origin");
            assert.equal(options.mode, "same-origin");
            assert.equal(options.cache, "no-store");
        }
    });
}

for (const status of [400, 404, 409, 429, 503, 500]) {
    test(`HTTP ${status} is returned without exposing internal error text`, async t => {
        let calls = 0;
        t.mock.method(globalThis, "fetch", async () => ++calls === 1
            ? response(200, { headerName: "X-Token", requestToken: "token" })
            : response(status, { detail: "internal-secret" }, "17"));
        const result = await setSupported(1, true);
        assert.equal(result.status, status);
        assert.equal(result.retryAfterSeconds, 17);
        assert.equal(JSON.stringify(result).includes("internal-secret"), false);
        assert.equal(calls, 2); // Never automatically retry a mutation.
    });
}

test("AltNet/unavailable GET is a 404 without reading its body", async t => {
    t.mock.method(globalThis, "fetch", async () => response(404, { detail: "internal-secret" }));
    assert.deepEqual(await load(), { status: 404, proposals: null, retryAfterSeconds: null });
});

test("failed token bootstrap and malformed tokens never send a mutation", async t => {
    for (const tokenResponse of [response(400), response(200, {}), response(200, { headerName: "", requestToken: "token" })]) {
        let calls = 0;
        t.mock.method(globalThis, "fetch", async () => { calls++; return tokenResponse; });
        assert.notEqual((await setSupported(1, true)).status, 204);
        assert.equal(calls, 1);
    }
});

test("network failures and malformed proposal responses produce controlled results", async t => {
    t.mock.method(globalThis, "fetch", async () => { throw Error("internal-secret"); });
    assert.deepEqual(await load(), { status: 0 });
    assert.deepEqual(await setSupported(1, true), { status: 0 });
    t.mock.method(globalThis, "fetch", async () => response(200, { wrong: "shape" }));
    assert.deepEqual(await load(), { status: 0 });
    t.mock.method(globalThis, "fetch", async () => ({ ok: true, json: async () => { throw Error("bad JSON"); } }));
    assert.deepEqual(await load(), { status: 0 });
});

test("a lost POST response is not assumed to mean the vote failed and isn't retried", async t => {
    let calls = 0;
    t.mock.method(globalThis, "fetch", async () => {
        if (++calls === 1) return response(200, { headerName: "X-Token", requestToken: "token" });
        throw Error("disconnected after write");
    });
    assert.deepEqual(await setSupported(1, true), { status: 0 });
    assert.equal(calls, 2);
});

test("invalid mutation arguments cannot become routes or request bodies", async t => {
    t.mock.method(globalThis, "fetch", () => { throw Error("should not fetch"); });
    for (const id of [-1, 0, "1/other", Number.MAX_SAFE_INTEGER + 1])
        assert.equal((await setSupported(id, true)).status, 400);
    assert.equal((await setSupported(1, "false")).status, 400);
});

test("focus returns to the current vote control, or its heading when no longer votable", t => {
    let target;
    const button = { disabled: false, focus: options => { assert.equal(options.preventScroll, true); target = "button"; } };
    const heading = { focus: () => target = "heading" };
    globalThis.document = { getElementById: id => id === "feature-vote-1" ? button : heading };
    t.after(() => delete globalThis.document);
    focusVote(1);
    assert.equal(target, "button");
    button.disabled = true;
    focusVote(1);
    assert.equal(target, "heading");
});
