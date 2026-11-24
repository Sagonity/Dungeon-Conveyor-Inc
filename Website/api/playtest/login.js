// POST /api/playtest/login  { code }  →  200 { ok: true, code } or 403 { ok: false }
// Checks a tester code against PLAYTEST_CODES on the server (SCRUM-35).
"use strict";
const { normaliseCode, isAllowedCode, readBody, send } = require("./_shared");

module.exports = async function handler(req, res) {
  if (req.method !== "POST") return send(res, 405, { ok: false, error: "Use POST." });
  const code = normaliseCode(readBody(req).code);
  if (!isAllowedCode(code)) return send(res, 403, { ok: false, error: "That code isn't valid." });
  return send(res, 200, { ok: true, code });
};
