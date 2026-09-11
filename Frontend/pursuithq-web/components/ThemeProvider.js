'use client';

import { createContext, useCallback, useContext, useEffect, useState } from "react";

const ThemeContext = createContext(null);

export const STORAGE_KEY = "pursuithq_theme";

/** The three choices. "system" follows whatever the operating system is set to. */
export const THEMES = ["light", "dark", "system"];

export function ThemeProvider({ children }) {
  const [theme, setThemeState] = useState("system");

  // Read the saved choice once, on the client. The inline script in layout.js
  // has already applied it to <html> by now - this only brings React's copy of
  // the value into line.
  useEffect(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (THEMES.includes(saved)) setThemeState(saved);
    } catch {
      // Private browsing, or storage blocked. The default is fine.
    }
  }, []);

  const apply = useCallback((next) => {
    const dark =
      next === "dark" ||
      (next === "system" && window.matchMedia("(prefers-color-scheme: dark)").matches);

    document.documentElement.classList.toggle("dark", dark);
  }, []);

  useEffect(() => {
    apply(theme);

    if (theme !== "system") return;

    // On "system", keep following the OS if it changes while the page is open.
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = () => apply("system");

    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, [theme, apply]);

  const setTheme = useCallback((next) => {
    if (!THEMES.includes(next)) return;

    setThemeState(next);

    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // The theme still applies for this visit; it just will not be remembered.
    }
  }, []);

  return (
    <ThemeContext.Provider value={{ theme, setTheme }}>{children}</ThemeContext.Provider>
  );
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used inside ThemeProvider");
  return ctx;
}
