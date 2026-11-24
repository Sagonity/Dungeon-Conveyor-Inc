// POST /api/playtest/log  { code, device, version, events: [{ t, type, detail }] }
// Called by the web game (PlaytestUploader) to store new playtest log lines under the tester's code.
"use strict";
const { normaliseCode, isAllowedCode, redis, logKey, CODES_KEY, readBody, send } = require("./_shared");

const MAX_EVENTS = 500;

const clip = (value, max) => String(value == null ? "" : value).slice(0, max);

module.exports = async function handler(req, res) {
  if (req.method !== "POST") return send(res, 405, { ok: false, error: "Use POST." });
  const body = readBody(req);
  const code = normaliseCode(body.code);
  if (!isAllowedCode(code)) return send(res, 403, { ok: false, error: "Unknown tester code." });

  const events = Array.isArray(body.events) ? body.events : [];
  if (events.length === 0) return send(res, 200, { ok: true, stored: 0 });
  if (events.length > MAX_EVENTS) return send(res, 413, { ok: false, error: "Too many events in one upload." });

  const device = clip(body.device, 16).replace(/[^A-Za-z0-9]/g, "");
  const version = clip(body.version, 16);
  const received = Date.now();
  const lines = [];
  for (const e of events) {
    const t = Number(e && e.t);
    if (!Number.isFinite(t) || t <= 0) continue;
    lines.push(JSON.stringify({ t, type: clip(e.type, 64), detail: clip(e.detail, 500), device, version, received }));
  }
  if (lines.length === 0) return send(res, 400, { ok: false, error: "No valid events." });

  try {
    await redis([["RPUSH", logKey(code), ...lines], ["SADD", CODES_KEY, code]]);
  } catch (err) {
    console.error(err);
    return send(res, 503, { ok: false, error: "Storage unavailable." });
  }
  return send(res, 200, { ok: true, stored: lines.length });
};
