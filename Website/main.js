// Fills the page from content/site.js and content/events.js (SCRUM-31).
// No build step and no libraries: open index.html directly or host the folder anywhere.
(function () {
  "use strict";
  var site = window.SITE || {};
  var events = window.EVENTS || [];
  var name = site.name || "";

  function fill(text) { return String(text || "").split("{name}").join(name); }
  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }

  document.title = name ? name + " · " + (site.tagline || "") : document.title;
  document.querySelectorAll("[data-site]").forEach(function (node) {
    node.textContent = fill(site[node.getAttribute("data-site")]);
  });
  document.getElementById("year").textContent = new Date().getFullYear();

  // Store buttons: a link when set, otherwise "Coming soon".
  var stores = document.getElementById("stores");
  [["googlePlay", "Google Play"], ["appStore", "App Store"]].forEach(function (s) {
    var url = site.stores && site.stores[s[0]];
    var b = el(url ? "a" : "span", "store" + (url ? "" : " soon"));
    b.appendChild(el("small", null, url ? "Get it on" : "Coming soon to"));
    b.appendChild(el("strong", null, s[1]));
    if (url) { b.href = url; b.rel = "noopener"; }
    stores.appendChild(b);
  });

  // The core loop.
  var loop = document.getElementById("loop");
  (site.loop || []).forEach(function (step) {
    var li = el("li");
    li.appendChild(el("h3", null, step.title));
    li.appendChild(el("p", null, fill(step.text)));
    loop.appendChild(li);
  });

  // Screenshots.
  var gallery = document.getElementById("gallery");
  (site.screenshots || []).forEach(function (shot) {
    var fig = el("figure", "shot");
    var img = el("img");
    img.src = shot.src;
    img.alt = shot.caption || "Screenshot";
    img.loading = "lazy";
    img.width = 390; img.height = 844;
    fig.appendChild(img);
    fig.appendChild(el("figcaption", null, shot.caption));
    gallery.appendChild(fig);
  });

  // Features.
  var features = document.getElementById("features-list");
  (site.features || []).forEach(function (f) {
    var card = el("article", "feature");
    card.appendChild(el("h3", null, f.title));
    card.appendChild(el("p", null, fill(f.text)));
    features.appendChild(card);
  });

  // Events: running and upcoming first (soonest first), then past (newest first).
  var list = document.getElementById("events-list");
  var today = new Date().toISOString().slice(0, 10);
  function state(e) { return e.end < today ? "past" : e.start > today ? "upcoming" : "live"; }
  function niceDate(d) {
    var p = d.split("-");
    return new Date(Date.UTC(+p[0], +p[1] - 1, +p[2])).toLocaleDateString(undefined, { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" });
  }
  var valid = events.filter(function (e) { return e && e.title && e.start && e.end; });
  var current = valid.filter(function (e) { return state(e) !== "past"; }).sort(function (a, b) { return a.start < b.start ? -1 : 1; });
  var past = valid.filter(function (e) { return state(e) === "past"; }).sort(function (a, b) { return a.end > b.end ? -1 : 1; });

  if (valid.length === 0) {
    var empty = el("div", "event empty");
    empty.appendChild(el("h3", null, "No events yet"));
    empty.appendChild(el("p", null, "Limited-time events with special rewards will be announced here. Check back after launch."));
    list.appendChild(empty);
  }
  current.concat(past).forEach(function (e) {
    var s = state(e);
    var card = el(e.link ? "a" : "article", "event " + s);
    if (e.link) { card.href = e.link; card.rel = "noopener"; }
    if (e.image) { var img = el("img"); img.src = e.image; img.alt = ""; img.loading = "lazy"; card.appendChild(img); }
    var body = el("div", "event-body");
    body.appendChild(el("span", "badge " + s, s === "live" ? "Live now" : s === "upcoming" ? "Upcoming" : "Ended"));
    body.appendChild(el("h3", null, e.title));
    body.appendChild(el("p", "dates", niceDate(e.start) + " – " + niceDate(e.end)));
    if (e.text) body.appendChild(el("p", null, fill(e.text)));
    card.appendChild(body);
    list.appendChild(card);
  });

  // Footer: contact and social links when they're set.
  var links = document.getElementById("footer-links");
  if (site.contactEmail) {
    var mail = el("a", null, "Contact");
    mail.href = "mailto:" + site.contactEmail;
    links.appendChild(mail);
  }
  (site.social || []).forEach(function (s) {
    var a = el("a", null, s.label);
    a.href = s.url; a.rel = "noopener";
    links.appendChild(a);
  });
})();
