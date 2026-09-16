'use client';

import { useEffect } from "react";
import Link from "next/link";
import Avatar from "@/components/Avatar";
import { useAuth } from "@/components/AuthProvider";
import { educationLabel } from "@/lib/education";

/**
 * Your own profile, shown the way another student sees it.
 *
 * The point of the panel is the reassurance: discoverability and what a
 * stranger can read are easy to get wrong and hard to check, and "go and look
 * at your own settings" is not the same as seeing the card itself.
 */
export default function ProfilePanel({ open, onClose }) {
  const { user } = useAuth();

  // Escape closes it, and the page behind stops scrolling while it is open.
  useEffect(() => {
    if (!open) return;

    function onKey(e) {
      if (e.key === "Escape") onClose();
    }

    document.addEventListener("keydown", onKey);
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = previous;
    };
  }, [open, onClose]);

  if (!open || !user) return null;

  const details = [
    ["School", user.school],
    ["Education level", educationLabel(user.educationLevel)],
    ["Major", user.major],
    ["Graduating", user.graduationYear],
  ].filter(([, value]) => Boolean(value));

  return (
    <div className="fixed inset-0 z-50">
      <button
        aria-label="Close"
        onClick={onClose}
        className="absolute inset-0 h-full w-full bg-slate-900/20"
      />

      <aside
        role="dialog"
        aria-label="Your profile"
        className="absolute right-0 top-0 flex h-full w-full max-w-sm flex-col border-l border-slate-200 bg-white shadow-xl"
      >
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <p className="text-sm font-medium text-slate-900">Your profile</p>
          <button
            onClick={onClose}
            className="rounded-md px-2 py-1 text-sm text-slate-500 hover:bg-slate-100"
          >
            Close
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-6">
          <div className="flex items-center gap-4">
            <Avatar student={{ ...user, hasPhoto: user.hasPhoto }} size={64} />
            <div className="min-w-0">
              <p className="truncate text-lg font-semibold text-slate-900">
                {user.firstName} {user.lastName}
              </p>
              <p className="truncate text-sm text-slate-600">{user.email}</p>
            </div>
          </div>

          <div className="mt-6 rounded-lg border border-slate-200 p-4">
            <p className="text-xs font-medium uppercase tracking-wide text-slate-400">
              How others see you
            </p>

            {user.isDiscoverable ? (
              <p className="mt-2 text-sm text-slate-700">
                Students can find you by name. They see your photo, name, school and
                education level — never your email, until you connect.
              </p>
            ) : (
              <p className="mt-2 text-sm text-slate-700">
                You are not listed. Only someone who already knows your email address can
                find you.
              </p>
            )}

            {details.length > 0 ? (
              <dl className="mt-4 space-y-2">
                {details.map(([term, value]) => (
                  <div key={term} className="flex justify-between gap-4 text-sm">
                    <dt className="text-slate-500">{term}</dt>
                    <dd className="text-right font-medium text-slate-900">{value}</dd>
                  </div>
                ))}
              </dl>
            ) : (
              <p className="mt-4 text-sm text-slate-500">
                Nothing filled in yet — your card is just your name.
              </p>
            )}
          </div>
        </div>

        <div className="border-t border-slate-200 px-5 py-4">
          <Link
            href="/settings"
            onClick={onClose}
            className="block w-full rounded-md bg-indigo-600 px-4 py-2 text-center text-sm font-medium text-white transition hover:bg-indigo-700"
          >
            Settings
          </Link>
          <p className="mt-2 text-center text-xs text-slate-500">
            Edit your profile, photo, notifications and security.
          </p>
        </div>
      </aside>
    </div>
  );
}
