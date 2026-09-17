'use client';

import { useEffect, useRef, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { auth as authApi } from "@/lib/api";
import { useAuth } from "./AuthProvider";

/**
 * The walkthrough.
 *
 * Deliberately not a spotlight tour. Those measure the position of a particular
 * button and cut a hole in an overlay around it, which looks wonderful until
 * somebody moves the button, or the layout reflows, or the screen is narrow
 * enough that the button in question is inside a drawer that is not open. This
 * takes the student to each real page instead and explains what they are
 * looking at, so it survives the app changing underneath it.
 */
const STEPS = [
  {
    path: "/dashboard",
    title: "This is your dashboard",
    body:
      "Everything due soon, in one place - assignments, events and reminders. " +
      "It fills up as you add things, so it is quiet right now.",
  },
  {
    path: "/courses",
    title: "Start with your courses",
    body:
      "Add a course for each class you are taking. Give it the days and times " +
      "it meets and it shows up on your calendar automatically. Almost " +
      "everything else in PursuitHQ hangs off a course, so this is the first " +
      "thing worth doing.",
  },
  {
    path: "/assignments",
    title: "Then your assignments",
    body:
      "Every assignment belongs to a course and has a due date. PursuitHQ can " +
      "email you before each one - you choose how far ahead in settings, and " +
      "you can pick more than one warning.",
  },
  {
    path: "/calendar",
    title: "Your calendar",
    body:
      "Classes, due dates, events and reminders together. Switch between " +
      "month, week and day at the top right. On a phone it opens on the day " +
      "view, because a month of seven columns is unreadable there.",
  },
  {
    path: "/study",
    title: "Study tools",
    body:
      "Upload your notes and slides, then turn them into flashcards, practice " +
      "quizzes and study guides. This is the part that saves the most time " +
      "once you have material in it.",
  },
  {
    path: "/students",
    title: "Find other students",
    body:
      "Search for people at your school and connect with them. You are not " +
      "listed here unless you turn that on in settings - it is off by default, " +
      "on purpose.",
  },
  {
    path: "/messages",
    title: "And message them",
    body:
      "Direct messages and group chats, with files, replies and reactions. " +
      "Connection requests appear here too.",
  },
  {
    path: "/settings",
    title: "Last stop: settings",
    body:
      "Your profile and photo, dark mode, which emails you get, two-step " +
      "verification, and the button that starts this walkthrough again. That " +
      "is everything - go add a course.",
    last: true,
  },
];

/**
 * Routes the tour must never appear over. Sign-in and the policy pages are
 * reachable without an account, and a walkthrough of the app on top of the
 * privacy policy would be absurd.
 */
const OFF_LIMITS = [
  "/login",
  "/register",
  "/forgot-password",
  "/reset-password",
  "/privacy",
  "/terms",
];

export default function TourGuide() {
  const { user, updateUser } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  const [step, setStep] = useState(0);
  const [running, setRunning] = useState(false);
  const [saving, setSaving] = useState(false);

  // True once the browser has actually arrived at the step being shown. Until
  // then the card and the address bar disagree, and the effect below has to
  // keep out of it - see the comment there.
  const arrived = useRef(false);
  const started = useRef(false);

  // Starts itself for anybody who has not seen it, and stops as soon as the
  // flag comes back true - which is what makes the settings button work: it
  // sets the flag false, and this notices.
  useEffect(() => {
    if (!user || user.hasSeenTour) {
      started.current = false;
      setRunning(false);
      return;
    }

    if (!started.current) {
      started.current = true;
      arrived.current = false;

      // Always from the beginning, wherever they happened to be standing.
      setStep(0);
      if (pathname !== STEPS[0].path) router.push(STEPS[0].path);
    }

    setRunning(true);
  }, [user, pathname, router]);

  // Somebody wandering off mid-tour is answered by following them rather than
  // dragging them back. The card keeps up; the step counter does not lie.
  //
  // The `arrived` guard is what stops this misreading the start. Restarting the
  // walkthrough from the settings page means that, for the moment between
  // asking for it and the browser reaching the dashboard, the current path is
  // /settings - which is itself the last step. Without the guard this would
  // read that as "they are on step eight", show the closing card, and only then
  // jump to the beginning.
  useEffect(() => {
    if (!running) return;

    if (pathname === STEPS[step]?.path) {
      arrived.current = true;
      return;
    }

    if (!arrived.current) return;

    const here = STEPS.findIndex((s) => s.path === pathname);
    if (here !== -1) setStep(here);
  }, [pathname, running, step]);

  if (!running || !user) return null;
  if (OFF_LIMITS.some((p) => pathname === p || pathname.startsWith(`${p}/`))) return null;

  const current = STEPS[step];
  if (!current) return null;

  async function finish() {
    setSaving(true);

    try {
      const profile = await authApi.setTourSeen(true);
      updateUser(profile);
    } catch {
      // Not worth an error message. The worst case is being offered the
      // walkthrough again next time, which is a far smaller annoyance than a
      // red box appearing at the end of a friendly introduction.
    } finally {
      setRunning(false);
      setSaving(false);
    }
  }

  function go(index) {
    const next = STEPS[index];
    if (!next) return;

    setStep(index);
    if (next.path !== pathname) router.push(next.path);
  }

  return (
    // Bottom on a phone, bottom-right on anything larger. Never centred: a
    // walkthrough that covers the thing it is describing is worse than none.
    <div className="pointer-events-none fixed inset-x-0 bottom-0 z-40 flex justify-center p-3 sm:inset-x-auto sm:right-4 sm:bottom-4 sm:p-0">
      <div className="pointer-events-auto w-full max-w-sm rounded-xl border border-slate-200 bg-white p-4 shadow-xl">
        <div className="flex items-start justify-between gap-3">
          <p className="text-sm font-semibold text-slate-900">{current.title}</p>

          <span className="shrink-0 rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600">
            {step + 1} / {STEPS.length}
          </span>
        </div>

        <p className="mt-2 text-sm leading-relaxed text-slate-600">{current.body}</p>

        <div className="mt-4 flex items-center justify-between gap-2">
          <button
            onClick={finish}
            disabled={saving}
            className="text-xs font-medium text-slate-500 underline transition hover:text-slate-700 disabled:opacity-50"
          >
            Skip for now
          </button>

          <div className="flex gap-2">
            {step > 0 && (
              <button
                onClick={() => go(step - 1)}
                disabled={saving}
                className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
              >
                Back
              </button>
            )}

            <button
              onClick={() => (current.last ? finish() : go(step + 1))}
              disabled={saving}
              className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-indigo-500 disabled:opacity-60"
            >
              {current.last ? (saving ? "Finishing..." : "Finish") : "Next"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
