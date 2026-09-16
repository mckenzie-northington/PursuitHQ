'use client';

import { useRef, useState } from "react";
import { profile as profileApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import Avatar, { forgetPhoto } from "@/components/Avatar";

/**
 * Your profile photo.
 *
 * The server re-encodes whatever is uploaded, which strips the EXIF that phone
 * photos carry - including GPS. Worth saying on screen, because "my photo knows
 * where I live" is not something anyone thinks about when picking one.
 */
export default function PhotoPicker({ label }) {
  const { user, updateUser } = useAuth();
  const input = useRef(null);

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function choose(e) {
    const file = e.target.files?.[0];
    if (!file) return;

    setBusy(true);
    setError("");

    try {
      await profileApi.uploadPhoto(file);

      // The cached blob is keyed by user id, so without this the old photo
      // stays on screen until a full reload.
      forgetPhoto(user.id);
      updateUser(await profileApi.me());
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
      if (input.current) input.current.value = "";
    }
  }

  async function remove() {
    setBusy(true);
    setError("");

    try {
      await profileApi.removePhoto();
      forgetPhoto(user.id);
      updateUser(await profileApi.me());
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  if (!user) return null;

  return (
    <div className="sm:col-span-2">
      <label className={label}>Photo</label>

      <div className="mt-2 flex items-center gap-4">
        <Avatar student={user} size={64} />

        <div>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              disabled={busy}
              onClick={() => input.current?.click()}
              className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
            >
              {busy ? "Working..." : user.hasPhoto ? "Change" : "Upload"}
            </button>

            {user.hasPhoto && (
              <button
                type="button"
                disabled={busy}
                onClick={remove}
                className="rounded-md px-3 py-1.5 text-sm font-medium text-slate-500 transition hover:text-red-600 disabled:opacity-50"
              >
                Remove
              </button>
            )}
          </div>

          <p className="mt-1.5 text-xs text-slate-500">
            JPEG, PNG or WebP, up to 8MB. It is resized and re-saved, which removes
            any location data your camera stored in it.
          </p>
        </div>
      </div>

      <input
        ref={input}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        onChange={choose}
        className="hidden"
      />

      {error && <p className="mt-2 text-sm text-red-700">{error}</p>}
    </div>
  );
}
