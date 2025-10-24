(() => {
  console.info("[MyGames.Extension] content script loaded");

  // small helper to POST via background.js (reliable)
  function sendToDesktop(path, payload) {
    return new Promise((resolve, reject) => {
      chrome.runtime.sendMessage(
        { type: "postMove", path, payload },
        (resp) => {
          if (!resp) return reject(new Error("no response from background"));
          if (resp.ok) resolve(resp);
          else reject(new Error(resp.error || "failed"));
        }
      );
    });
  }

  // debounce to avoid duplicates
  let lastSentMove = null;

  // ---- Chess.com: try multiple strategies ----

  function findChessComMoveList() {
    // 2025 Chess.com uses wc-simple-move-list (Web Component)
    const candidates = [
      "wc-simple-move-list",
      ".vertical-move-list",
      ".move-list-component",
      ".moves",
    ];
    for (const sel of candidates) {
      const el = document.querySelector(sel);
      if (el) return el;
    }
    return null;
  }

  // parse SAN move text from a node or container
  function parseLastSanFromChessCom(container) {
    if (!container) return null;

    // If it's <wc-simple-move-list>, get its last move-item shadow DOM
    if (container.tagName?.toLowerCase() === "wc-simple-move-list") {
      try {
        const shadowRoot = container.shadowRoot;
        if (shadowRoot) {
          const allMoves = shadowRoot.querySelectorAll(".move");
          if (allMoves.length > 0) {
            const last = allMoves[allMoves.length - 1];
            const san = last.textContent.trim();
            return san || null;
          }
        }
      } catch (e) {
        console.warn("[MyGames.Extension] shadow parse failed", e);
      }
    }

    // fallback: normal DOM parsing
    const nodes = Array.from(
      container.querySelectorAll("li, .move, .vertical-move-list__move")
    );
    for (let i = nodes.length - 1; i >= 0; i--) {
      const txt = nodes[i].innerText.trim();
      if (txt) {
        const toks = txt.split(/\s+/).filter(Boolean);
        return toks[toks.length - 1];
      }
    }

    const txtAll = container.innerText.trim();
    const toks = txtAll.split(/\s+/);
    return toks.length ? toks[toks.length - 1] : null;
  }

  // ---- Generic observer helper ----
  function observeContainer(container, site) {
    if (!container) return;
    console.info(`[MyGames.Extension] observing move list for ${site}`);

    const observer = new MutationObserver(async () => {
      try {
        // small random delay to let DOM settle
        await new Promise((r) => setTimeout(r, 150 + Math.random() * 200));

        const san = parseLastSanFromChessCom(container);
        if (!san) return;
        if (san === lastSentMove) return;

        lastSentMove = san;
        console.info(`[MyGames.Extension] detected SAN: ${san}`);

        // send to desktop
        await sendToDesktop("/move_san", { san });
        console.info("[MyGames.Extension] sent /move_san", san);
      } catch (err) {
        console.warn("[MyGames.Extension] observer error", err);
      }
    });

    observer.observe(container, {
      childList: true,
      subtree: true,
      characterData: true,
    });
  }

  // ---- Try detect move list container ----
  const chessComContainer = findChessComMoveList();
  if (chessComContainer) {
    observeContainer(chessComContainer, "chesscom");
  } else {
    // fallback observer: wait until it appears
    const fallback = new MutationObserver(() => {
      const c1 = findChessComMoveList();
      if (c1) {
        fallback.disconnect();
        observeContainer(c1, "chesscom");
      }
    });
    fallback.observe(document, { childList: true, subtree: true });
  }

  // ---- Optional: detect game start (future use) ----
  function detectAndSendGameStart() {
    // You can add logic later if needed
  }

  setTimeout(detectAndSendGameStart, 1500);
})();
