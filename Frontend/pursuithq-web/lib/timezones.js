/**
 * The time zone list behind every zone picker in the app.
 *
 * US zones only. PursuitHQ is built for students at a US university, and four
 * hundred options is four hundred chances to scroll past the right one.
 *
 * Labels are built at runtime rather than written out, because a written label
 * is wrong for half the year: Eastern is UTC-05:00 in January and UTC-04:00 in
 * July, and a list typed up in winter quietly lies all summer. Asking Intl each
 * time hands daylight saving to the browser's own tz database instead of to us
 * remembering to update a file twice a year.
 */

/**
 * East to west, the way US zones are normally listed.
 *
 * `place` is set only where the city behind the id would be misleading:
 * "Phoenix" is really the whole of Arizona, and nobody thinks of their zone as
 * "Honolulu". Where it is absent the city is used, which reads fine for
 * New York, Chicago, Denver and Los Angeles.
 */
export const US_ZONES = [
  { id: "America/New_York" },
  { id: "America/Chicago" },
  { id: "America/Denver" },
  { id: "America/Phoenix", place: "Arizona" },
  { id: "America/Los_Angeles" },
  { id: "America/Anchorage", place: "Alaska" },
  { id: "Pacific/Honolulu", place: "Hawaii" },
  { id: "America/Puerto_Rico", place: "Puerto Rico" },
];

export const DEFAULT_ZONE = "America/New_York";

/** What the device thinks, or the default if it will not say. */
export function browserZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || DEFAULT_ZONE;
  } catch {
    return DEFAULT_ZONE;
  }
}

/** True if this browser recognises the id at all. */
export function isKnownZone(zoneId) {
  if (!zoneId) return false;

  try {
    new Intl.DateTimeFormat("en-US", { timeZone: zoneId });
    return true;
  } catch {
    return false;
  }
}

/**
 * Minutes east of UTC for a zone at a given instant.
 *
 * Worked out by formatting the same instant twice - once read in the zone, once
 * read as UTC - and subtracting. There is no API that just hands over an
 * offset, but Intl already carries the whole tz database, so this gets at it
 * without shipping a date library.
 */
function offsetMinutes(zoneId, at) {
  const parts = {};

  for (const part of new Intl.DateTimeFormat("en-US", {
    timeZone: zoneId,
    hour12: false,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).formatToParts(at)) {
    parts[part.type] = part.value;
  }

  // Some engines render midnight as hour 24 rather than 0.
  const hour = parts.hour === "24" ? 0 : Number(parts.hour);

  const wall = Date.UTC(
    Number(parts.year),
    Number(parts.month) - 1,
    Number(parts.day),
    hour,
    Number(parts.minute),
    Number(parts.second)
  );

  // formatToParts has no milliseconds, so both sides compare whole seconds.
  return Math.round((wall - Math.floor(at.getTime() / 1000) * 1000) / 60_000);
}

/** "+05:30", or "-04:00" with a real minus sign so it lines up in a list. */
function formatOffset(minutes) {
  const sign = minutes < 0 ? "−" : "+";
  const abs = Math.abs(minutes);
  const hours = String(Math.floor(abs / 60)).padStart(2, "0");

  return `${sign}${hours}:${String(abs % 60).padStart(2, "0")}`;
}

/**
 * "Eastern Time" rather than "Eastern Daylight Time".
 *
 * The generic name is the one people recognise and the one that does not go
 * stale twice a year. Older browsers do not support longGeneric, so the
 * seasonal name is the fallback. Arizona and Hawaii come back as "Standard
 * Time" either way, which is correct - neither observes daylight saving.
 */
function zoneName(zoneId, at) {
  for (const style of ["longGeneric", "long"]) {
    try {
      const parts = new Intl.DateTimeFormat("en-US", {
        timeZone: zoneId,
        timeZoneName: style,
      }).formatToParts(at);

      const name = parts.find((part) => part.type === "timeZoneName")?.value;

      // Browsers that do not know a name hand back the raw offset instead,
      // which is already in the label - not worth repeating.
      if (name && !/^GMT[+\-−]?\d/i.test(name)) return name;
    } catch {
      // Try the next style.
    }
  }

  return "";
}

/** "America/Los_Angeles" -> "Los Angeles" */
function cityOf(zoneId) {
  return (zoneId.split("/").pop() ?? zoneId).replace(/_/g, " ");
}

/**
 * The label shown in the picker, e.g.
 * "(UTC-05:00) Central Time - Chicago".
 */
export function zoneLabel(zoneId, at = new Date(), place) {
  if (!isKnownZone(zoneId)) return zoneId;

  const offset = `(UTC${formatOffset(offsetMinutes(zoneId, at))})`;
  const where = place ?? cityOf(zoneId);
  const name = zoneName(zoneId, at);

  // "Alaska Time - Alaska" and "Hawaii-Aleutian Standard Time - Hawaii" say the
  // same thing twice, so the place is dropped once the name already carries it.
  if (!name || name.includes(where)) return `${offset} ${name || where}`;

  return `${offset} ${name} — ${where}`;
}

/**
 * The picker's options, in list order.
 *
 * Not cached: eight zones cost almost nothing to label, and computing them on
 * demand means a page left open across a daylight saving change shows the new
 * offsets rather than yesterday's.
 */
export function zoneOptions(at = new Date()) {
  return US_ZONES.filter((zone) => isKnownZone(zone.id)).map((zone) => ({
    id: zone.id,
    label: zoneLabel(zone.id, at, zone.place),
  }));
}

/**
 * Now, as a Date whose ordinary local fields read as the wall clock in
 * `zoneId`.
 *
 * Due dates arrive from the API without an offset, so `new Date(dueDate)` reads
 * them as browser-local wall clock. Comparing one of those against a plain
 * `new Date()` silently asks "has this passed where this laptop is", which is a
 * different question from "has this passed where the student says they are".
 * Shifting now by the difference between the two zones puts both sides of the
 * comparison on the same footing.
 */
export function wallClockNow(zoneId, at = new Date()) {
  if (!isKnownZone(zoneId)) return at;

  // getTimezoneOffset counts minutes *behind* UTC, so the sign is flipped.
  const browser = -at.getTimezoneOffset();

  return new Date(at.getTime() + (offsetMinutes(zoneId, at) - browser) * 60_000);
}
