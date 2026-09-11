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

const PUBLIC_ROUTES = ["/login", "/register", "/forgot-password", "/reset-password"];

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
