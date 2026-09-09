'use client';

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "./AuthProvider";

const LINKS = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/calendar", label: "Calendar" },
  { href: "/courses", label: "Courses" },
  { href: "/assignments", label: "Assignments" },
];

export default function NavBar() {
  const { user, signOut } = useAuth();
  const pathname = usePathname();

  if (!user) return null;

  return (
    <header className="border-b border-slate-200 bg-white">
      <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <div className="flex items-center gap-8">
          <Link href="/dashboard" className="text-lg font-semibold text-slate-900">
            Pursuit<span className="text-indigo-600">HQ</span>
          </Link>

          <nav className="flex gap-1">
            {LINKS.map((link) => {
              const active = pathname === link.href;
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

        <div className="flex items-center gap-4">
          <span className="text-sm text-slate-600">{user.firstName} {user.lastName}</span>
          <button
            onClick={signOut}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
          >
            Sign out
          </button>
        </div>
      </div>
    </header>
  );
}
