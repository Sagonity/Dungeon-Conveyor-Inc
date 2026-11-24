# Game website (SCRUM-31)

A one-page site for the game: what it is, screenshots, features and events.
Plain HTML, CSS and a little JavaScript: no build step and no libraries.

```
Website/
  index.html          the page
  styles.css          look and layout (colours from the game's UI)
  main.js             fills the page from the two content files
  content/site.js     game name, texts, store links, screenshots   ← edit this
  content/events.js   events                                       ← and this
  images/             placeholder screenshots and key art (SVG)
  privacy.html        placeholder privacy policy (SCRUM-25)
  terms.html          placeholder terms (SCRUM-25)
```

## Preview

Open `Website/index.html` in a browser. It works straight from the folder, with no server needed.

## Common changes

| To… | Edit |
|---|---|
| Rename the game | `name` in `content/site.js`. The name appears nowhere else |
| Change the pitch, the loop steps or features | `content/site.js` (`{name}` in a text is replaced with the game name) |
| Add store links at launch | `stores.googlePlay` / `stores.appStore` in `content/site.js`. While empty, the buttons say "Coming soon" |
| Add contact or social links | `contactEmail` / `social` in `content/site.js` |
| Swap a screenshot | Put the picture in `images/` and change its `src` in `screenshots`. Phone-format pictures (about 9:19.5, e.g. 1170 × 2532) fit best |
| Change the key art | Replace `images/placeholder-hero.svg` (or change the `src` of `.hero-art` in `index.html`) |
| Add an event | Copy the example at the top of `content/events.js` into the list. Dates are `YYYY-MM-DD`; the page sorts events into live, upcoming and ended by itself |

## Placeholders to replace

- **Screenshots and key art** (`images/placeholder-*.svg`) are drawn mock-ups marked "PLACEHOLDER". Replace them with real screenshots of the game, or with real art after SCRUM-13.
- **Privacy policy and terms** are placeholder pages. Write them before release (SCRUM-25). They're also needed for the store listings.
- **The name** is the working title (SCRUM-25).

## Hosting (to decide)

Any static host works: upload the `Website/` folder as it is.

- **GitHub Pages:** GitHub Pages only serves the repository root or a `/docs` folder from a branch, so a site in
  `Website/` needs a small GitHub Actions workflow to publish it. On a private repository, Pages needs a paid
  GitHub plan.
- **Netlify or Cloudflare Pages:** free; connect the repository and set the publish folder to `Website`, with no build command.

Use the host's free address until the game has its final name, then add a real domain.
