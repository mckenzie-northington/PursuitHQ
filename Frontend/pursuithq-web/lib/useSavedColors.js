'use client';

import { useCallback, useEffect, useState } from "react";
import { preferences } from "@/lib/api";

/**
 * The student's saved colors, loaded once and kept in sync with the API.
 *
 * Adding and removing update the screen immediately and save in the
 * background. If the save fails the list snaps back to what it was, so what
 * you see is never a color the server did not actually keep.
 */
export function useSavedColors() {
  const [colors, setColors] = useState([]);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    preferences
      .colors()
      .then((saved) => {
        if (!cancelled) setColors(saved ?? []);
      })
      .catch(() => {
        // A palette that will not load is not worth interrupting the page for -
        // the built-in colors still work.
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const persist = useCallback(async (next, previous) => {
    setColors(next);
    setError("");

    try {
      const saved = await preferences.saveColors(next);
      setColors(saved ?? next);
    } catch (err) {
      setColors(previous);
      setError(err.message);
    }
  }, []);

  const add = useCallback(
    (hex) => {
      const value = (hex || "").toLowerCase();
      if (!value || colors.some((c) => c.toLowerCase() === value)) return;
      persist([...colors, value], colors);
    },
    [colors, persist]
  );

  const remove = useCallback(
    (hex) => {
      const value = (hex || "").toLowerCase();
      persist(
        colors.filter((c) => c.toLowerCase() !== value),
        colors
      );
    },
    [colors, persist]
  );

  const has = useCallback(
    (hex) => colors.some((c) => c.toLowerCase() === (hex || "").toLowerCase()),
    [colors]
  );

  return { colors, add, remove, has, error };
}
