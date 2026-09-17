'use client';

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "./AuthProvider";
import Avatar from "./Avatar";
import ProfilePanel from "./ProfilePanel";
import { useUnread } from "@/lib/useUnread";

const LINKS = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/calendar", label: "Calendar" },
  { href: "/courses", label: "Courses" },
  { href: "/study", label: "Study" },
  { href: "/assignments", label: "Assignments" },
  { href: "/resume", label: "Resume" },
  { href: "/students", label: "Students" },
];

/**
 * Links that live in the drawer only.
 *
 * On a wide screen these are reachable from the profile panel, which is a
 * hover-and-click affordance that does not survive the trip to a phone. Rather
 * than have the drawer be a subset of the app, it carries everything.
 */
const DRAWER_EXTRAS = [
  { href: "/settings", label: "Settings" },
  { href: "/support", label: "Report a problem" },
];

export default function NavBar() {
  const { user, signOut } = useAuth();
  const pathname = usePathname();
  const [panelOpen, setPanelOpen] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  // Called unconditionally - hooks cannot sit behind the early return below,
  // and the hook itself does nothing until there is a signed-in user.
  const unread = useUnread();

  // Navigating closes the drawer. Without this, tapping a link slides the panel
  // shut only if the destination happens to unmount it, and the app looks stuck.
  useEffect(() => {
    setMenuOpen(false);
  }, [pathname]);

  // Escape closes it, and the page behind it does not scroll while it is open.
  // Both are things people try without thinking, and both are broken by default.
  useEffect(() => {
    if (!menuOpen) return;

    function onKey(e) {
      if (e.key === "Escape") setMenuOpen(false);
    }

    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    document.addEventListener("keydown", onKey);

    return () => {
      document.body.style.overflow = previous;
      document.removeEventListener("keydown", onKey);
    };
  }, [menuOpen]);

  if (!user) return null;

  const waiting = unread.total + unread.connectionRequests;

  function isActive(href) {
    return pathname === href || pathname.startsWith(`${href}/`);
  }

  return (
    <>
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-3 px-4 py-3 sm:px-6 sm:py-4">
          <div className="flex min-w-0 items-center gap-8">
            <Link
              href="/dashboard"
              className="shrink-0 text-lg font-semibold text-slate-900"
            >
              Pursuit<span className="text-indigo-600">HQ</span>
            </Link>

            <nav className="hidden gap-1 lg:flex">
              {LINKS.map((link) => (
                <Link
                  key={link.href}
                  href={link.href}
                  className={`rounded-md px-3 py-1.5 text-sm font-medium transition ${
                    isActive(link.href)
                      ? "bg-indigo-50 text-indigo-700"
                      : "text-slate-600 hover:bg-slate-100 hover:text-slate-900"
                  }`}
                >
                  {link.label}
                </Link>
              ))}
            </nav>
          </div>

          <div className="flex shrink-0 items-center gap-1 sm:gap-2">
            <Link
              href="/messages"
              title={waiting > 0 ? `${waiting} waiting` : "Messages"}
              aria-label={waiting > 0 ? `Messages, ${waiting} waiting` : "Messages"}
              className={`relative rounded-md p-2 transition ${
                pathname.startsWith("/messages")
                  ? "bg-indigo-50 text-indigo-700"
                  : "text-slate-600 hover:bg-slate-100 hover:text-slate-900"
              }`}
            >
              <MessageIcon />

              {waiting > 0 && (
                <span
                  className="absolute right-1 top-1 block h-2.5 w-2.5 rounded-full bg-red-500 ring-2 ring-white"
                  aria-hidden="true"
                />
              )}
            </Link>

            <button
              onClick={() => setPanelOpen(true)}
              title="Your profile"
              className="flex items-center gap-2 rounded-md py-1 pl-1 pr-1 text-sm font-medium text-slate-700 transition hover:bg-slate-100 sm:pr-2.5"
            >
              <Avatar student={user} size={28} />
              <span className="hidden sm:inline">{user.firstName}</span>
            </button>

            {/* Wide screens keep sign-out where it has always been. */}
            <button
              onClick={signOut}
              className="hidden rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 lg:block"
            >
              Sign out
            </button>

            {/* And narrow screens get everything behind this instead. */}
            <button
              onClick={() => setMenuOpen(true)}
              aria-label="Open menu"
              aria-expanded={menuOpen}
              className="rounded-md p-2 text-slate-600 transition hover:bg-slate-100 hover:text-slate-900 lg:hidden"
            >
              <MenuIcon />
            </button>
          </div>
        </div>
      </header>

      {/* ------------------------------------------------------------------
          The drawer.
          Kept mounted and slid off-screen rather than conditionally rendered,
          so opening and closing animate instead of snapping. pointer-events-none
          while closed, because an invisible panel that still swallows taps on
          the right edge of the screen is a maddening bug to track down.
      ------------------------------------------------------------------ */}
      <div
        className={`fixed inset-0 z-50 lg:hidden ${
          menuOpen ? "" : "pointer-events-none"
        }`}
        aria-hidden={!menuOpen}
      >
        <div
          onClick={() => setMenuOpen(false)}
          className={`absolute inset-0 bg-slate-900/40 transition-opacity duration-200 ${
            menuOpen ? "opacity-100" : "opacity-0"
          }`}
        />

        <nav
          className={`absolute right-0 top-0 flex h-full w-72 max-w-[85%] flex-col border-l border-slate-200 bg-white shadow-xl transition-transform duration-200 ease-out ${
            menuOpen ? "translate-x-0" : "translate-x-full"
          }`}
        >
          <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3">
            <span className="text-sm font-semibold text-slate-900">Menu</span>

            <button
              onClick={() => setMenuOpen(false)}
              aria-label="Close menu"
              className="rounded-md p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900"
            >
              <CloseIcon />
            </button>
          </div>

          {/* Scrolls on its own, so a short phone in landscape can still reach
              the bottom of the list. */}
          <div className="flex-1 overflow-y-auto px-2 py-3">
            {LINKS.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className={`block rounded-md px-3 py-2.5 text-sm font-medium transition ${
                  isActive(link.href)
                    ? "bg-indigo-50 text-indigo-700"
                    : "text-slate-700 hover:bg-slate-100"
                }`}
              >
                {link.label}
              </Link>
            ))}

            <Link
              href="/messages"
              className={`flex items-center justify-between rounded-md px-3 py-2.5 text-sm font-medium transition ${
                isActive("/messages")
                  ? "bg-indigo-50 text-indigo-700"
                  : "text-slate-700 hover:bg-slate-100"
              }`}
            >
              Messages
              {waiting > 0 && (
                <span className="ml-2 rounded-full bg-red-500 px-2 py-0.5 text-xs font-semibold text-white">
                  {waiting > 99 ? "99+" : waiting}
                </span>
              )}
            </Link>

            <div className="my-2 border-t border-slate-200" />

            {DRAWER_EXTRAS.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className={`block rounded-md px-3 py-2.5 text-sm font-medium transition ${
                  isActive(link.href)
                    ? "bg-indigo-50 text-indigo-700"
                    : "text-slate-700 hover:bg-slate-100"
                }`}
              >
                {link.label}
              </Link>
            ))}
          </div>

          <div className="border-t border-slate-200 p-3">
            <button
              onClick={signOut}
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              Sign out
            </button>
          </div>
        </nav>
      </div>

      <ProfilePanel open={panelOpen} onClose={() => setPanelOpen(false)} />
    </>
  );
}

function MessageIcon() {
  return (
    <svg
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M21 11.5a8.4 8.4 0 0 1-9 8.4 8.9 8.9 0 0 1-4-.9L3 21l1.9-4.6A8.4 8.4 0 0 1 4 11.5a8.4 8.4 0 0 1 9-8.4 8.4 8.4 0 0 1 8 8.4Z" />
    </svg>
  );
}

function MenuIcon() {
  return (
    <svg
      width="22"
      height="22"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      aria-hidden="true"
    >
      <path d="M4 7h16M4 12h16M4 17h16" />
    </svg>
  );
}

function CloseIcon() {
  return (
    <svg
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      aria-hidden="true"
    >
      <path d="M6 6l12 12M18 6L6 18" />
    </svg>
  );
}
