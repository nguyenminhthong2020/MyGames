(() => {
  console.info("[MyGames.Extension.Test] content-script loaded ✅ (UCI mode)");

  // state
  let gameId = null;
  let opponentIsWhite = true; // test config; real ext can detect later
  const moveQueue = [];
  let isProcessing = false;

  // helper to send HTTP via background
  function sendToDesktopViaBackground(path, payload) {
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

  function enqueueMove(moveUci, moveIndex) {
    if (!moveUci) return;
    moveQueue.push({ moveUci, moveIndex });
    processQueue();
  }

  async function processQueue() {
    if (isProcessing || moveQueue.length === 0 || !gameId) return;
    isProcessing = true;
    while (moveQueue.length > 0) {
      const { moveUci, moveIndex } = moveQueue.shift();
      const payload = { gameId, move: moveUci, moveIndex };
      console.info("[MyGames.Extension.Test] send /move_uci ->", payload);
      try {
        await sendToDesktopViaBackground("/move_uci", payload);
        console.info("[MyGames.Extension.Test] sent move", moveUci);
      } catch (err) {
        console.error("[MyGames.Extension.Test] failed send move", err);
      }
      await new Promise((r) => setTimeout(r, 120)); // avoid burst
    }
    isProcessing = false;
  }

  // 🧩 fake moves for testing (all UCI)
  // 5 nước đầu: 3 trắng (đối thủ), 2 đen (tôi) — chỉ là tốt đi 1 bước
  const fakeMoves1 = [
    "a2a3", // white
    "a7a6", // black
    "b2b3", // white
    "b7b6", // black
    "c2c3", // white
  ];

  // Tiếp theo: tốt cột h tiến 1 bước + ngựa cột g tiến đến f
  const fakeMoves2 = [
    "h2h3", // white
    "h7h6", // black
    "g1f3", // white knight
    "g8f6", // black knight
  ];

  // chọn mảng test
  let fakeMoves = fakeMoves2;

  let idx = 0;
  let lastSent = null;

  function pickNextOpponentMoveIndex() {
    const len = fakeMoves.length;
    if (len === 0) return -1;
    if (idx >= len) idx = 0;
    for (let attempts = 0; attempts < len; attempts++) {
      const cur = idx % len;
      const isWhiteMove = cur % 2 === 0;
      const belongsToOpponent = opponentIsWhite ? isWhiteMove : !isWhiteMove;
      idx = (idx + 1) % len;
      if (belongsToOpponent) return cur;
    }
    return -1;
  }

  async function autoSendFakeMove() {
    if (!gameId) {
      console.warn("[MyGames.Extension.Test] no gameId, skip");
      return;
    }

    if (moveQueue.length > 0) {
      console.info(
        "[MyGames.Extension.Test] replay queue not empty, delaying periodic fake move"
      );
      return;
    }

    const mi = pickNextOpponentMoveIndex();
    if (mi < 0) {
      console.info(
        "[MyGames.Extension.Test] all fake moves sent — stopping test game"
      );
      if (timerId) {
        clearInterval(timerId);
        timerId = null;
      }
      chrome.storage.local.set({ mygames_extension_game_active: false }, () => {
        console.info(
          "[MyGames.Extension.Test] set game_active = false in storage"
        );
      });
      return;
    }

    const uci = fakeMoves[mi];
    if (!uci || uci === lastSent) return;
    lastSent = uci;
    enqueueMove(uci, mi);
  }

  // periodic sender (for testing)
  let timerId = null;
  function startPeriodicSender() {
    if (timerId) return;
    console.info(
      "[MyGames.Extension.Test] starting periodic fake move sender ⏱️ (UCI mode)"
    );
    autoSendFakeMove(); // send first move immediately
    timerId = setInterval(autoSendFakeMove, 10000);
  }

  // listen background -> content-script (game started)
  chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    if (!msg || !msg.type) return;
    if (msg.type === "game_started" && msg.payload) {
      gameId = msg.payload.gameId;
      console.info(
        "[MyGames.Extension.Test] content-script got game_started (UCI)",
        msg.payload
      );

      chrome.storage.local.set({
        mygames_extension_gameId: gameId,
        mygames_extension_game_active: true,
      });

      const cm = Array.isArray(msg.payload.currentMoves)
        ? msg.payload.currentMoves
        : [];
      if (cm.length > 0) {
        cm.forEach((m, i) => enqueueMove(m, i));
        fakeMoves = fakeMoves2;
        idx = 0;
        lastSent = null;
        startPeriodicSender();
      } else {
        fakeMoves = fakeMoves2;
        idx = 0;
        lastSent = null;
        startPeriodicSender();
      }

      sendResponse && sendResponse({ ok: true });
      return true;
    }
  });

  // auto-initialize
  chrome.storage.local.get(
    ["mygames_extension_gameId", "mygames_extension_game_active"],
    (obj) => {
      if (obj?.mygames_extension_gameId && obj?.mygames_extension_game_active) {
        gameId = obj.mygames_extension_gameId;
        console.info(
          "[MyGames.Extension.Test] found active game in storage, resuming",
          gameId
        );
        startPeriodicSender();
      } else {
        console.info("[MyGames.Extension.Test] no active game found — idle");
      }
    }
  );
})();
