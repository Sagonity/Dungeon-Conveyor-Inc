/*
 * Site settings (SCRUM-31). Everything here can be changed without touching the page code.
 * The game name is ONLY here: change `name` and the whole site follows (title, header, text).
 *
 * Text may contain {name}; it is replaced with the game name.
 * Leave a store link empty ("") to show "Coming soon" instead of a button link.
 */
window.SITE = {
  name: "Expedition: Deepshaft",          // working title (SCRUM-25 decides the final name)
  studio: "SagoCorp",
  tagline: "Dig deep. Send your Minions. Get rich while you sleep.",
  pitch:
    "{name} is a mobile mining and auto-battler game. Your Miner Minions dig for Gold and ore " +
    "around the clock, your Adventure Minions fight the boss of each mine tier, and the Expeditions " +
    "you clear keep earning rare materials even while your phone is locked.",

  stores: {
    googlePlay: "",   // e.g. "https://play.google.com/store/apps/details?id=com.sagocorp.deepshaft"
    appStore: "",     // e.g. "https://apps.apple.com/app/id0000000000"
  },

  contactEmail: "",   // e.g. "hello@example.com"; empty hides the contact link
  social: [
    // { label: "Discord", url: "https://discord.gg/..." },
  ],

  // How the game plays, in order (the "core loop").
  loop: [
    { title: "Mine", text: "Miner Minions dig the deepest tier you've opened, and the Gold and ore pile up even while you're away." },
    { title: "Upgrade", text: "Level up your Minions and forge better tools, weapons and armor at the Smith." },
    { title: "Beat the boss", text: "Your Adventure team takes on each mine tier's boss. Win its unique material and open new Expeditions." },
    { title: "Clear & automate", text: "Clear an Expedition once, then let it run on its own forever, farming rare materials." },
    { title: "Break through", text: "Spend rare materials on breakthroughs that raise your Minions' and gear's limits, and dig deeper." },
  ],

  features: [
    { title: "Two kinds of Minions", text: "Miners with fixed traits (Gold, ore or speed) and Adventurers in five classes: Tank, Fighter, Archer, Mage and Support." },
    { title: "Collect them all", text: "Summon Common, Uncommon and Rare Minions from the Miner or Adventure pool. The rates are always shown." },
    { title: "Mine tiers and bosses", text: "Each new tier mines richer ore and is guarded by a boss with its own material." },
    { title: "Progress while you're away", text: "Mining and automated Expeditions keep going with the app closed. Coming back always pays." },
    { title: "Fair to free players", text: "Every tier can be cleared without paying." },
  ],

  // Screenshots in phone format (9:19.5 works well). Placeholders until real art (SCRUM-13).
  screenshots: [
    { src: "images/placeholder-hq.svg", caption: "HQ: everything you earn per hour" },
    { src: "images/placeholder-minions.svg", caption: "Level up and break through" },
    { src: "images/placeholder-smith.svg", caption: "Forge gear at the Smith" },
    { src: "images/placeholder-boss.svg", caption: "Take on the tier boss" },
    { src: "images/placeholder-summon.svg", caption: "Summon new Minions" },
  ],
};
