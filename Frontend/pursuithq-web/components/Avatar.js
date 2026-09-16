'use client';

import { useEffect, useState } from "react";
import { students as studentsApi, conversations as conversationsApi } from "@/lib/api";

/**
 * Photos are fetched once and kept for the session.
 *
 * They sit behind the bearer token, so an <img src> pointing at the API comes
 * back 401 - the photo has to be fetched with the header and turned into a blob
 * URL. Without this cache that would happen on every render of every list, and
 * avatars would visibly flash on each one.
 *
 * The object URLs are deliberately never revoked. They are small, there is one
 * per subject rather than per render, and revoking them would break every
 * avatar still on screen for the sake of a few kilobytes.
 *
 * Keyed by kind as well as id, because a group and a student can share an id
 * and must not share a picture.
 */
const photos = new Map();

function load(kind, id) {
  const key = `${kind}:${id}`;

  if (!photos.has(key)) {
    const fetcher = kind === "group" ? conversationsApi.groupPhoto : studentsApi.photo;

    photos.set(
      key,
      fetcher(id)
        .then(({ url }) => url)
        .catch(() => null)
    );
  }

  return photos.get(key);
}

/** Forgets a cached photo, so a fresh upload shows instead of the old one. */
export function forgetPhoto(id, kind = "student") {
  photos.delete(`${kind}:${id}`);
}

const TONES = [
  "bg-indigo-100 text-indigo-700",
  "bg-emerald-100 text-emerald-700",
  "bg-amber-100 text-amber-800",
  "bg-rose-100 text-rose-700",
  "bg-sky-100 text-sky-700",
  "bg-violet-100 text-violet-700",
];

/** Same subject, same colour, every time - without storing anything. */
function toneFor(key = "") {
  let total = 0;
  for (let i = 0; i < key.length; i++) total += key.charCodeAt(i);

  return TONES[total % TONES.length];
}

/**
 * Pass either a student or a group, never both.
 *
 *   <Avatar student={card} />
 *   <Avatar group={{ id, title, hasPhoto }} />
 */
export default function Avatar({ student, group, size = 40, className = "" }) {
  const [url, setUrl] = useState(null);

  const kind = group ? "group" : "student";
  const subject = group ?? student;
  const id = subject?.id;
  const hasPhoto = subject?.hasPhoto;

  useEffect(() => {
    if (!id || !hasPhoto) {
      setUrl(null);
      return;
    }

    let live = true;
    load(kind, id).then((found) => live && setUrl(found));

    return () => {
      live = false;
    };
  }, [kind, id, hasPhoto]);

  const initials = group
    ? // Two words of a group name read better than two letters of one word:
      // "CS Study Group" is CS, not CS-something.
      (group.title ?? "")
        .split(/\s+/)
        .filter(Boolean)
        .slice(0, 2)
        .map((word) => word[0])
        .join("")
        .toUpperCase() || "#"
    : `${student?.firstName?.[0] ?? ""}${student?.lastName?.[0] ?? ""}`.toUpperCase() || "?";

  const style = { width: size, height: size, fontSize: Math.round(size * 0.38) };

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
        `${kind}:${id}`
      )} ${className}`}
    >
      {initials}
    </span>
  );
}
