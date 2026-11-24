// Browser helpers for the web playtest build (SCRUM-35). Only used in WebGL builds.
mergeInto(LibraryManager.library, {

  // In a browser, Application.persistentDataPath lives in memory until it's synced to IndexedDB.
  // Called after every save, so progress survives a reload or a closed tab.
  DeepshaftSyncFiles: function () {
    if (typeof FS === "undefined" || !FS.syncfs) return;
    FS.syncfs(false, function (err) {
      if (err) console.warn("Deepshaft: could not store the save in the browser:", err);
    });
  },

  // Tells the game when the tab is hidden (switched away, phone locked, closing), so it can save
  // and send the playtest log. Calls OnWebPageHidden on the given GameObject.
  DeepshaftListenForPageHidden: function (objectNamePtr) {
    var objectName = UTF8ToString(objectNamePtr);
    document.addEventListener("visibilitychange", function () {
      if (document.visibilityState === "hidden") SendMessage(objectName, "OnWebPageHidden");
    });
    window.addEventListener("pagehide", function () { SendMessage(objectName, "OnWebPageHidden"); });
  },
});
