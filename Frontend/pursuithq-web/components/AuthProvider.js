'use client';

// ---------------------------------------------------------------------------
// Holds the signed-in student in React state so any page can read it, and
// redirects to /login when there is no valid session.
// ---------------------------------------------------------------------------

import { createContext, useContext, useEffect, useState } from "react";
import { useRouter, usePathname } from "next/navigation";
import { auth, getStoredUser, getToken, setSession, clearSession } from "@/lib/api";
import { setStoredUser } from "@/lib/api";

const AuthContext = createContext(null);

/**
 * Pages that render without a session.
 *
 * The two policy pages belong here for a reason beyond convenience: they are
 * linked from the sign-up form, and somebody deciding whether to hand over
 * their coursework and messages is exactly the person who needs to read them -
 * before they have an account. Bouncing them to /login would mean the only
 * people who can read the privacy policy are the ones who already agreed to it.
 */
const PUBLIC_ROUTES = [
  "/login",
  "/register",
  "/forgot-password",
  "/reset-password",
  "/privacy",
  "/terms",
];

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    const token = getToken();

    if (!token) {
      setUser(null);
      setLoading(false);
      if (!PUBLIC_ROUTES.includes(pathname)) router.replace("/login");
      return;
    }

    // Show the stored user immediately, then confirm with the API that the
    // token is still valid (it expires after 60 minutes).
    setUser(getStoredUser());

    auth
      .me()
      .then((profile) => {
        setUser(profile);
        setLoading(false);
      })
      .catch(() => {
        clearSession();
        setUser(null);
        setLoading(false);
        if (!PUBLIC_ROUTES.includes(pathname)) router.replace("/login");
      });
  }, [pathname, router]);

  function signIn(token, profile) {
    setSession(token, profile);
    setUser(profile);
    router.push("/dashboard");
  }

  /**
   * Replaces the cached profile after it is edited, so the name in the nav bar
   * changes without a reload and a refresh does not show the old one.
   */
  function updateUser(profile) {
    setUser(profile);
    setStoredUser(profile);
  }

  function signOut() {
    clearSession();
    setUser(null);
    router.replace("/login");
  }

  return (
    <AuthContext.Provider value={{ user, loading, signIn, signOut, updateUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
