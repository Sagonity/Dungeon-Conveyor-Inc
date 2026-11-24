// Admin API for /playtest/admin.html (SCRUM-35). Every request needs the header
// "x-admin-password: <ADMIN_PASSWORD>".
//   GET    /api/playtest/admin              → { testers: [{ code, events, first, last }] }
//   GET    /api/playtest/admin?code=DS-7K4Q → that tester's log as CSV (utc,minutes,type,detail,device,version)
//   DELETE /api/playtest/admin?code=DS-7K4Q → deletes that tester's log
//   DELETE /api/playtest/admin?all=yes      → deletes every log (after the playtest)
"use strict";
const { normaliseCode, isAdmin, redis, logKey, CODES_KEY, send } = require("./_shared");

function parse(line) {
  try { return JSON.parse(line); } catch (e) { return null; }
}

function csvField(value) {
  const text = String(value == null ? "" : value);
  return /[",\n]/.test(text) ? '"' + text.replace(/"/g, '""') + '"' : text;
}

function toCsv(events) {
  events.sort((a, b) => a.t - b.t);
  const first = events.length ? events[0].t : 0;
  const rows = ["utc,minutes,type,detail,device,version"];
  for (const e of events) {
    const utc = new Date(e.t).toISOString().replace("T", " ").slice(0, 19);
    const minutes = ((e.t - first) / 60000).toFixed(1);
    rows.push([utc, minutes, e.type, e.detail, e.device, e.version].map(csvField).join(","));
  }
  return rows.join("\n") + "\n";
}

module.exports = async function handler(req, res) {
  if (!isAdmin(req)) return send(res, 401, { ok: false, error: "Wrong or missing admin password." });
  const query = req.query || {};
  const code = normaliseCode(query.code);

  try {
    if (req.method === "GET" && code) {
      const [lines] = await redis([["LRANGE", logKey(code), "0", "-1"]]);
      const events = (lines || []).map(parse).filter(Boolean);
      res.setHeader("Cache-Control", "no-store");
      res.setHeader("Content-Type", "text/csv; charset=utf-8");
      res.setHeader("Content-Disposition", 'attachment; filename="playtest-' + code + '.csv"');
      return res.status(200).send(toCsv(events));
    }

    if (req.method === "GET") {
      const [codes] = await redis([["SMEMBERS", CODES_KEY]]);
      const list = (codes || []).sort();
      const testers = [];
      if (list.length) {
        const commands = [];
        for (const c of list) {
          commands.push(["LLEN", logKey(c)], ["LINDEX", logKey(c), "0"], ["LINDEX", logKey(c), "-1"]);
        }
        const results = await redis(commands);
        list.forEach((c, i) => {
          const first = parse(results[i * 3 + 1]);
          const last = parse(results[i * 3 + 2]);
          testers.push({ code: c, events: results[i * 3] || 0, first: first ? first.t : null, last: last ? last.received : null });
        });
      }
      return send(res, 200, { ok: true, testers });
    }

    if (req.method === "DELETE" && code) {
      await redis([["DEL", logKey(code)], ["SREM", CODES_KEY, code]]);
      return send(res, 200, { ok: true, deleted: code });
    }

    if (req.method === "DELETE" && query.all === "yes") {
      const [codes] = await redis([["SMEMBERS", CODES_KEY]]);
      const commands = (codes || []).map((c) => ["DEL", logKey(c)]);
      commands.push(["DEL", CODES_KEY]);
      await redis(commands);
      return send(res, 200, { ok: true, deleted: (codes || []).length });
    }
  } catch (err) {
    console.error(err);
    return send(res, 503, { ok: false, error: "Storage unavailable: " + err.message });
  }
  return send(res, 400, { ok: false, error: "Unknown request." });
};
