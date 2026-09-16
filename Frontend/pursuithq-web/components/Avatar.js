'use client';

import { useEffect, useState } from "react";
import { students as studentsApi } from "@/lib/api";

/**
 * Photos are fetched once per person and kept for the session.
 *
 * They sit behind the bearer token, so an <img src> pointing at the API would
 * come back 401 - the photo has to be fetched with the header and turned into a
 * blob URL. Without this cache that would happen on every render of every list,
 * and avatars would visibly flash on each one.
 *
 * The object URLs are deliberately never revoked. They are small, there is one
 * per person rather than per render, and revoking them would break every avatar
 * still on screen for the sake of a few kilobytes.
 */
const photos = new Map();

function loadPhoto(id) {
  if (!photos.has(id)) {
    photos.set(
      id,
      studentsApi
        .photo(id)
        .then(({ url }) => url)
        .catch(() => null)
    );
  }

  return photos.get(id);
}

/** Forgets a cached photo, so a fresh upload is picked up rather than the old one. */
export function forgetPhoto(id) {
  photos.delete(id);
}

const TONES = [
  "bg-indigo-100 text-indigo-700",
  "bg-emerald-100 text-emerald-700",
  "bg-amber-100 text-amber-800",
  "bg-rose-100 text-rose-700",
  "bg-sky-100 text-sky-700",
  "bg-violet-100 text-violet-700",
];

/** Same person, same colour, every time - without storing anything. */
function toneFor(id = "") {
  let total = 0;
  for (let i = 0; i < id.length; i++) total += id.charCodeAt(i);

  return TONES[total % TONES.length];
}

export default function Avatar({ student, size = 40, className = "" }) {
  const [url, setUrl] = useState(null);

  const id = student?.id;
  const hasPhoto = student?.hasPhoto;

  useEffect(() => {
    if (!id || !hasPhoto) {
      setUrl(null);
      return;
    }

    let live = true;
    loadPhoto(id).then((found) => live && setUrl(found));

    return () => {
      live = false;
    };
  }, [id, hasPhoto]);

  const initials =
    `${student?.firstName?.[0] ?? ""}${student?.lastName?.[0] ?? ""}`.toUpperCase() || "?";

  const style = { width: size, height: size, fontSize: Math.round(size * 0.4) };

  if (url) {
    return (
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={url}
        alt=""
        style={style}
        className={`shrink-0 rounded-full object-cover ${className}`}
      />
    );
  }

  return (
    <span
      aria-hidden="true"
      style={style}
      className={`flex shrink-0 items-center justify-center rounded-full font-semibold ${toneFor(
        id
      )} ${className}`}
    >
      {initials}
    </span>
  );
}
