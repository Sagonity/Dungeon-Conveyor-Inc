# Web playtest (SCRUM-35)

Remote testers play the game in a browser at **/playtest** on the website. They need a tester code, and
their playtest log is sent to us automatically under that code. You download each tester's log at
**/playtest/admin.html**.

```
/playtest                 code → consent → the game, full screen
/playtest/game/           the Unity web build (you add it, see step 2)
/playtest/admin.html      admin password → testers, Download CSV, Delete
/api/playtest/login       checks a code            (Website/api/playtest/login.js)
/api/playtest/log         receives log lines       (Website/api/playtest/log.js)
/api/playtest/admin       list / CSV / delete      (Website/api/playtest/admin.js)
```

## 1. Set up Vercel (once)

1. **Database:** in the Vercel project, open **Storage** (or the **Marketplace**), add **Upstash → Redis**,
   choose the free plan, and connect it to this project. Vercel adds `KV_REST_API_URL` and
   `KV_REST_API_TOKEN` to the project's settings by itself.
2. **Settings → Environment Variables**, for Production:
   - `PLAYTEST_CODES`: the tester codes, comma-separated, e.g. `DS-7K4Q, DS-P2M9, DS-H8XW`.
     Use letters, digits and dashes. Make them hard to guess, one per tester.
   - `ADMIN_PASSWORD`: a long password for the admin page. Don't share it with testers.
3. **Redeploy** (Deployments → ⋯ → Redeploy), so the functions see the new settings.

Keep the list of which code belongs to which person **offline**, not in the repo, Jira or Vercel. The
codes are anonymous everywhere else.

## 2. Make the web build (each new version)

1. In **Unity Hub**, add the **Web Build Support** module to the project's Unity version (6000.6), if it isn't installed.
2. **File → Build Profiles → Web → Switch Platform.**
3. **Player Settings → Web:**
   - **Resolution and Presentation → Web Template: `Deepshaft`**. This is the template in
     `Assets/WebGLTemplates/Deepshaft`; it fills the whole page and shows a loading bar.
   - **Publishing Settings → Compression Format: Disabled.** Vercel compresses the files itself, and Unity's
     own compressed files would need extra server settings.
   - Set **Version** to the build's version (it's sent with every log line).
4. **Build** into `Website/playtest/game/`, so that `Website/playtest/game/index.html` and
   `Website/playtest/game/Build/…` exist. Replace the previous build.
5. Commit and push to `main`. Vercel publishes it in a minute or two.

## 3. Check it

1. Open `https://<your site>/playtest`, enter one of your codes, agree, and play for a minute.
2. In the game: **Settings → Debug** should say "Sent to the website automatically as DS-… · last sent …".
   It sends every minute and when the tab is hidden; **Send now** sends straight away.
3. Open `/playtest/admin.html`, enter the admin password: the code is listed. **Download CSV** gives the
   same columns as Copy log (`utc,minutes,type,detail`), plus `device` and `version`.
4. Reload the game page: the save should still be there.

## 4. During and after the playtest

- Send each tester the link `https://<your site>/playtest` and their code. The browser remembers the code
  and the consent; **"Not your code?"** clears them.
- It works best in a desktop browser (Chrome, Edge, Firefox, Safari). Phone browsers work but load slower.
- Testers shouldn't use Settings → Debug (time skips and Reset are logged, but they change the test).
- **When the playtest is over:** on the admin page, **Delete all logs**, and empty `PLAYTEST_CODES`.

## How it works

- The page checks the code on the server (`/api/playtest/login`), not in the browser, then loads
  `game/index.html?code=DS-…` in a full-page frame.
- The game (`PlaytestUploader`) only sends anything when it was opened with a `code`. Phone builds and
  the Unity editor never send. It sends log lines it hasn't sent yet, in batches of up to 500, and
  remembers the last one it sent.
- `/api/playtest/log` refuses unknown codes, so the database only holds data from your testers.
- In a browser the save lives in the browser's storage (IndexedDB). `WebSaveStorage` syncs it after
  every save, so a reload or a closed tab keeps progress. Clearing the browser's site data deletes it.
- **Limit:** the game files in `/playtest/game/` are static, so someone who knows that address can load the
  game without a code. They can't send data (no valid code) or see anyone's logs (admin password).
  Locking the files too is possible later with Vercel Middleware or Vercel's password protection.
