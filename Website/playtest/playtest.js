// Web playtest page (SCRUM-35): check the tester code on the server, ask for consent, then load the
// Unity web build from ./game/ with ?code=… so the game can send its playtest log under that code.
(function () {
  "use strict";
  var site = window.SITE || {};
  document.querySelectorAll("[data-site]").forEach(function (n) { n.textContent = site[n.getAttribute("data-site")] || ""; });
  document.title = "Playtest · " + (site.name || "");

  var KEY = "deepshaft.playtest";
  var $ = function (id) { return document.getElementById(id); };
  var saved = {};
  try { saved = JSON.parse(localStorage.getItem(KEY) || "{}"); } catch (e) { saved = {}; }

  function store() { try { localStorage.setItem(KEY, JSON.stringify(saved)); } catch (e) { /* private mode */ } }

  function show(step) {
    $("code-form").hidden = step !== "code";
    $("consent").hidden = step !== "consent";
    $("gate").hidden = step === "game";
    $("game").hidden = step !== "game";
  }

  function check(code) {
    return fetch("/api/playtest/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ code: code }),
    }).then(function (r) { return r.json().catch(function () { return {}; }).then(function (d) { return r.ok && d.ok ? d.code : ""; }); });
  }

  function askConsent(code) {
    $("consent-code").textContent = "(" + code + ")";
    $("agree").checked = false;
    $("start").disabled = true;
    show("consent");
  }

  function startGame() {
    fetch("/playtest/game/index.html", { method: "HEAD" }).then(function (r) {
      if (!r.ok) { $("missing-build").hidden = false; return; }
      $("frame").src = "/playtest/game/index.html?code=" + encodeURIComponent(saved.code);
      show("game");
    }, function () { $("missing-build").hidden = false; });
  }

  $("code-form").addEventListener("submit", function (ev) {
    ev.preventDefault();
    var code = $("code").value.trim().toUpperCase();
    $("code-error").textContent = "";
    check(code).then(function (ok) {
      if (!ok) { $("code-error").textContent = "That code doesn't work. Check it and try again."; return; }
      saved = { code: ok, consent: false };
      store();
      askConsent(ok);
    }, function () { $("code-error").textContent = "Couldn't reach the server. Try again in a moment."; });
  });

  $("agree").addEventListener("change", function () { $("start").disabled = !$("agree").checked; });

  $("start").addEventListener("click", function () {
    saved.consent = true;
    saved.consentAt = new Date().toISOString();
    store();
    startGame();
  });

  function forget(ev) {
    if (ev) ev.preventDefault();
    saved = {};
    store();
    $("frame").src = "about:blank";
    $("code").value = "";
    show("code");
  }
  $("change-code").addEventListener("click", forget);
  $("leave").addEventListener("click", function (ev) {
    ev.preventDefault();
    $("frame").src = "about:blank"; // the game saves when the page is hidden
    askConsent(saved.code);
  });

  // Returning tester: check the remembered code again (it may have been removed), then continue.
  if (saved.code) {
    check(saved.code).then(function (ok) {
      if (!ok) return forget();
      if (saved.consent) startGame(); else askConsent(ok);
    }, function () { show("code"); });
  } else {
    show("code");
  }
})();
