'use client';

import { useCallback, useEffect, useState } from "react";
import { conversations as conversationsApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

/** Often enough to feel live, rarely enough not to be rude to the server. */
const INTERVAL_MS = 30_000;

/**
 * How many things are waiting on you: unread messages and unanswered requests.
 *
 * Polled. Real push over SignalR is the next step, and this hook is the seam it
 * will slot into - everything that shows the dot reads it from here, so the
 * change is one file rather than every component that displays a badge.
 *
 * Polling pauses while the tab is hidden. A laptop left open on this tab
 * overnight would otherwise make roughly a thousand pointless requests.
 */
export function useUnread() {
  const { user } = useAuth();
  const [unread, setUnread] = useState({ total: 0, conversations: 0, connectionRequests: 0 });

  const refresh = useCallback(async () => {
    if (!user) return;

    try {
      setUnread(await conversationsApi.unread());
    } catch {
      // A failed poll is not worth surfacing. The next one will do.
    }
  }, [user]);

  useEffect(() => {
    if (!user) return;

    refresh();

    const id = setInterval(() => {
      if (document.visibilityState === "visible") refresh();
    }, INTERVAL_MS);

    // Coming back to the tab should be immediate rather than up to 30s stale.
    function onVisible() {
      if (document.visibilityState === "visible") refresh();
    }

    document.addEventListener("visibilitychange", onVisible);

    return () => {
      clearInterval(id);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [user, refresh]);

  return { ...unread, refresh };
}
