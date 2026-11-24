// Shared helpers for the web playtest API (SCRUM-35). Files starting with "_" aren't turned into
// endpoints by Vercel. No npm packages: Redis is reached over Upstash's REST API with fetch.
//
// Vercel settings (Project → Settings → Environment Variables):
//   PLAYTEST_CODES   tester codes, comma-separated, e.g. "DS-7K4Q, DS-P2M9"
//   ADMIN_PASSWORD   password for /playtest/admin.html
//   KV_REST_API_URL + KV_REST_API_TOKEN  (or UPSTASH_REDIS_REST_URL + UPSTASH_REDIS_REST_TOKEN)
//                    added automatically when an Upstash Redis database is connected to the project.
"use strict";
const crypto = require("crypto");

const CODE_PATTERN = /^[A-Z0-9-]{3,32}$/;

function normaliseCode(value) {
  const code = String(value || "").trim().toUpperCase();
  return CODE_PATTERN.test(code) ? code : "";
}

function allowedCodes() {
  return String(process.env.PLAYTEST_CODES || "")
    .split(/[\s,;]+/)
    .map(normaliseCode)
    .filter(Boolean);
}

function isAllowedCode(value) {
  const code = normaliseCode(value);
  return code !== "" && allowedCodes().includes(code);
}

/** Constant-time password check against ADMIN_PASSWORD. False when no password is set. */
function isAdmin(req) {
  const expected = String(process.env.ADMIN_PASSWORD || "");
  const given = String(req.headers["x-admin-password"] || "");
  if (!expected || !given) return false;
  const a = crypto.createHash("sha256").update(expected).digest();
  const b = crypto.createHash("sha256").update(given).digest();
  return crypto.timingSafeEqual(a, b);
}

function redisConfig() {
  const url = process.env.KV_REST_API_URL || process.env.UPSTASH_REDIS_REST_URL || "";
  const token = process.env.KV_REST_API_TOKEN || process.env.UPSTASH_REDIS_REST_TOKEN || "";
  return url && token ? { url: url.replace(/\/+$/, ""), token } : null;
}

/** Runs several Redis commands in one request. Returns the results in order. Throws on failure. */
async function redis(commands) {
  const config = redisConfig();
  if (!config) throw new Error("No Redis database is connected (KV_REST_API_URL / KV_REST_API_TOKEN).");
  const response = await fetch(config.url + "/pipeline", {
    method: "POST",
    headers: { Authorization: "Bearer " + config.token, "Content-Type": "application/json" },
    body: JSON.stringify(commands),
  });
  if (!response.ok) throw new Error("Redis request failed: HTTP " + response.status);
  const results = await response.json();
  return results.map((r) => {
    if (r && r.error) throw new Error("Redis error: " + r.error);
    return r ? r.result : null;
  });
}

const logKey = (code) => "playtest:log:" + code;
const CODES_KEY = "playtest:codes";

function readBody(req) {
  if (req.body && typeof req.body === "object") return req.body;
  try { return JSON.parse(req.body || "{}"); } catch (e) { return {}; }
}

function send(res, status, data) {
  res.setHeader("Cache-Control", "no-store");
  res.status(status).json(data);
}

module.exports = { normaliseCode, isAllowedCode, isAdmin, redis, logKey, CODES_KEY, readBody, send };
