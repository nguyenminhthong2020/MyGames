// chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
//   if (msg && msg.type === "postMove") {
//     postMoveToDesktop(msg.path, msg.payload)
//       .then((r) => sendResponse({ ok: true }))
//       .catch((err) => sendResponse({ ok: false, error: err.message }));
//     return true; // keep channel open for async response
//   }
// });

// async function postMoveToDesktop(path, payload) {
//   const url = `http://127.0.0.1:54546${path}`;
//   const res = await fetch(url, {
//     method: "POST",
//     headers: { "Content-Type": "application/json" },
//     body: JSON.stringify(payload),
//     // no-cors not allowed; desktop CORS should allow
//   });
//   if (!res.ok) throw new Error(`HTTP ${res.status}`);
//   return res.text();
// }

// background.js (service worker)
// lắng nghe 2 loại message:
// - { type: "postMove", path, payload }  => post HTTP tới desktop
// - { type: "start_game", payload } => post /game_started và gửi message tới content-script

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  try {
    if (!msg || !msg.type) {
      sendResponse({ ok: false, error: "invalid message" });
      return true;
    }

    if (msg.type === "postMove") {
      // forward arbitrary move -> desktop HTTP
      postMoveToDesktop(msg.path, msg.payload)
        .then((r) => sendResponse({ ok: true, data: r }))
        .catch((err) => sendResponse({ ok: false, error: err.message }));
      return true; // async response
    }

    if (msg.type === "start_game") {
      // payload should contain { gameId, side, currentMoves }
      // 1) POST to desktop /game_started
      const payload = msg.payload || {};
      postMoveToDesktop("/game_started", payload)
        .then(async (r) => {
          // 2) tell the active tab (content-script) that game started so it can resume sending moves
          // find active tab in current window
          chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
            if (tabs && tabs.length > 0) {
              chrome.tabs.sendMessage(
                tabs[0].id,
                { type: "game_started", payload },
                (resp) => {
                  // ignore resp; content-script will handle
                }
              );
            }
          });
          sendResponse({ ok: true, data: r });
        })
        .catch((err) => {
          sendResponse({ ok: false, error: err.message });
        });
      return true;
    }

    // unknown message type
    sendResponse({ ok: false, error: "unknown message type" });
  } catch (ex) {
    sendResponse({ ok: false, error: ex.message });
  }
});

// helper to post to desktop internal server
async function postMoveToDesktop(path, payload) {
  const url = `http://127.0.0.1:54546${path}`;
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    const txt = await res.text().catch(() => "");
    throw new Error(`HTTP ${res.status} ${txt}`);
  }
  return res.text();
}
