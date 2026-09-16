'use client';

import { useState } from "react";
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

export default function NavBar() {
  const { user, signOut } = useAuth();
  const pathname = usePathname();
  const [panelOpen, setPanelOpen] = useState(false);

  // Called unconditionally - hooks cannot sit behind the early return below,
  // and the hook itself does nothing until there is a signed-in user.
  const unread = useUnread();

  if (!user) return null;

  const waiting = unread.total + unread.connectionRequests;

  return (
    <>
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-8">
            <Link href="/dashboard" className="text-lg font-semibold text-slate-900">
              Pursuit<span className="text-indigo-600">HQ</span>
            </Link>

            <nav className="hidden gap-1 lg:flex">
              {LINKS.map((link) => {
                const active =
                  pathname === link.href || pathname.startsWith(`${link.href}/`);

                return (
                  <Link
                    key={link.href}
                    href={link.href}
                    className={`rounded-md px-3 py-1.5 text-sm font-medium transition ${
                      active
                        ? "bg-indigo-50 text-indigo-700"
                        : "text-slate-600 hover:bg-slate-100 hover:text-slate-900"
                    }`}
                  >
                    {link.label}
                  </Link>
                );
              })}
            </nav>
          </div>

          <div className="flex items-center gap-2">
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
              className="flex items-center gap-2 rounded-md py-1 pl-1 pr-2.5 text-sm font-medium text-slate-700 transition hover:bg-slate-100"
            >
              <Avatar student={user} size={28} />
              <span className="hidden sm:inline">{user.firstName}</span>
            </button>

            <button
              onClick={signOut}
              className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              Sign out
            </button>
          </div>
        </div>
      </header>

      <ProfilePanel open={panelOpen} onClose={() => setPanelOpen(false)} />
    </>
  );
}
