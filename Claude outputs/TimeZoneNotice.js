'use client';

import { useEffect, useState } from "react";
import { auth as authApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import { browserZone, zoneLabel, isKnownZone } from "@/lib/timezones";

const DISMISS_KEY = "pursuithq.tzNoticeDismissed";

/**
 * Offers to move the saved time zone when the device disagrees with it.
 *
 * Deliberately an offer rather than a switch. A due date of 11:59pm belongs to
 * the course, not to wherever the student is sitting - so a week away from
 * school should not quietly re-time every deadline and every reminder. The
 * device is good evidence about a permanent move and no evidence at all about a
 * trip, and only the student can tell those apart.
 *
 * Renders nothing when the two agree, which is almost always.
 */
export default function TimeZoneNotice() {
  const { user, updateUser } = useAuth();

  const [device, setDevice] = useState(null);
  const [dismissed, setDismissed] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  // Read on the client only. The server has no device to ask, and rendering a
  // different answer there than here would be a hydration mismatch.
  useEffect(() => {
    setDevice(browserZone());
  }, []);

  const saved = user?.timeZone;
  const mismatch =
    device && saved && device !== saved && isKnownZone(device) && isKnownZone(saved);

  useEffect(() => {
    if (!mismatch) return;

    try {
      // Keyed by the pair, so declining this move does not also silence the
      // next one - coming home should ask again.
      setDismissed(window.localStorage.getItem(DISMISS_KEY) === `${saved}|${device}`);
    } catch {
      setDismissed(false);
    }
  }, [mismatch, saved, device]);

  if (!mismatch || dismissed) return null;

  async function update() {
    setBusy(true);
    setError("");

    try {
      // Deliberately not updateProfile: that endpoint writes the whole profile
      // from what it is handed, so sending it only a zone would clear the name
      // and major this component knows nothing about.
      updateUser(await authApi.updateTimeZone(device));
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  function keep() {
    try {
      window.localStorage.setItem(DISMISS_KEY, `${saved}|${device}`);
    } catch {
      // Private browsing, or storage turned off. Hiding it for this render is
      // enough; it can ask again next time.
    }

    setDismissed(true);
  }

  return (
    <div className="mb-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3">
      <p className="text-sm text-amber-800">
        Your device is set to <span className="font-medium">{zoneLabel(device)}</span>, but
        PursuitHQ is using <span className="font-medium">{zoneLabel(saved)}</span>.
      </p>

      <p className="mt-1 text-xs text-amber-700">
        Due dates and reminder times follow the setting, not the device. Only change it
        if you have actually moved.
      </p>

      {error && <p className="mt-2 text-xs text-red-700">{error}</p>}

      <div className="mt-3 flex flex-wrap gap-2">
        <button
          type="button"
          onClick={update}
          disabled={busy}
          className="rounded-md bg-amber-700 px-3 py-1.5 text-xs font-medium text-white transition hover:bg-amber-800 disabled:opacity-50"
        >
          {busy ? "Updating..." : "Use my device's time zone"}
        </button>

        <button
          type="button"
          onClick={keep}
          disabled={busy}
          className="rounded-md border border-amber-300 px-3 py-1.5 text-xs font-medium text-amber-800 transition hover:bg-amber-100 disabled:opacity-50"
        >
          Keep {zoneLabel(saved).replace(/^\([^)]*\)\s*/, "")}
        </button>
      </div>
    </div>
  );
}
