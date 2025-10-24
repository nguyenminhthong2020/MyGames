console.log("[MyGames.Extension] popup.js loaded ✅ (UCI mode)");

// helper gửi message tới background để background gọi HTTP POST tới desktop
function sendToBackground(path, payload) {
  console.log(
    "[MyGames.Extension] Sending message to background:",
    path,
    payload
  );
  return new Promise((resolve, reject) => {
    try {
      chrome.runtime.sendMessage(
        { type: "postMove", path, payload },
        (resp) => {
          if (!resp) return reject(new Error("no response from background"));
          if (resp.ok) resolve(resp);
          else reject(new Error(resp.error || "failed"));
        }
      );
    } catch (err) {
      reject(err);
    }
  });
}

// 🧩 Validate UCI move format (simple: 4 chars like a2a4)
function isValidUciMove(str) {
  return /^[a-h][1-8][a-h][1-8]$/.test(str);
}

document.getElementById("start").addEventListener("click", async () => {
  console.log(
    "[MyGames.Extension] Start button clicked, preparing UCI payload..."
  );

  const side = document.querySelector("input[name=side]:checked").value;

  // reuse or create gameId
  let { mygames_extension_gameId: gameId } = await chrome.storage.local.get(
    "mygames_extension_gameId"
  );
  if (!gameId) {
    gameId = crypto.randomUUID();
    await chrome.storage.local.set({ mygames_extension_gameId: gameId });
  }

  console.info("[MyGames.Extension] Using gameId from localStorage:", gameId);

  // get optional moves from textarea (now UCI)
  const moveText = document.getElementById("moves")?.value.trim() ?? "";
  let currentMoves = [];

  if (moveText.length > 0) {
    const rawMoves = moveText
      .split(",")
      .map((m) => m.trim())
      .filter((m) => m.length > 0);
    currentMoves = rawMoves
      .map((m) => {
        if (isValidUciMove(m)) return m;
        console.warn(
          `[MyGames.Extension] ⚠️ Invalid UCI move "${m}" — skipped`
        );
        return null;
      })
      .filter(Boolean);
  }

  console.info("[MyGames.Extension] UCI moves parsed:", currentMoves);

  const payload = { gameId, side, currentMoves };

  console.info(
    "[MyGames.Extension] Requesting background to POST /game_started",
    payload
  );

  try {
    const resp = await sendToBackground("/game_started", payload);
    console.info(
      "[MyGames.Extension] Background responded OK for /game_started"
    );
    console.log("✅ Notified desktop app!");

    // after successfully POST /game_started to desktop:
    chrome.storage.local.set(
      {
        mygames_extension_gameId: payload.gameId,
        mygames_extension_game_active: true,
      },
      () => {
        console.log(
          "[MyGames.Extension] Stored active gameId and active flag."
        );
        // notify content scripts to start sending live moves (if any)
        chrome.runtime.sendMessage({ type: "game_started", payload });
      }
    );
  } catch (err) {
    console.error(
      "[MyGames.Extension] Failed to notify desktop via background:",
      err
    );
    console.log("❌ Failed: " + err.message);
  }
});
