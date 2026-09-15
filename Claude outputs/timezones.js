/**
 * The time zone list behind every zone picker in the app.
 *
 * Labels are built at runtime rather than hardcoded, because a hardcoded label
 * is wrong for half the year: "Eastern Standard Time, UTC-05:00" becomes
 * Eastern Daylight Time at UTC-04:00 every March, and a list written in winter
 * quietly lies all summer. Asking Intl each time means daylight saving is
 * handled by the browser's own tz database instead of by us remembering.
 */

/**
 * For browsers without Intl.supportedValuesOf (anything before ~2022).
 * Not meant to be complete - just enough that the picker is never empty.
 */
const FALLBACK_ZONES = [
  "America/New_York",
  "America/Chicago",
  "America/Denver",
  "America/Phoenix",
  "America/Los_Angeles",
  "America/Anchorage",
  "Pacific/Honolulu",
  "Europe/London",
  "UTC",
];

/** Anything we cannot read a zone from falls back to this. */
export const DEFAULT_ZONE = "America/New_York";

const REGION_NAMES = {
  Africa: "Africa",
  America: "Americas",
  Antarctica: "Antarctica",
  Arctic: "Arctic",
  Asia: "Asia",
  Atlantic: "Atlantic",
  Australia: "Australia",
  Europe: "Europe",
  Indian: "Indian Ocean",
  Pacific: "Pacific",
};

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
 * seasonal name is the fallback, and the city alone is the last resort.
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
export function zoneLabel(zoneId, at = new Date()) {
  if (!isKnownZone(zoneId)) return zoneId;

  const offset = `(UTC${formatOffset(offsetMinutes(zoneId, at))})`;
  const city = cityOf(zoneId);
  const name = zoneName(zoneId, at);

  if (!name || name === city) return `${offset} ${city}`;

  // UTC and GMT have no city worth naming.
  if (zoneId === "UTC" || zoneId === "GMT") return `${offset} ${name}`;

  return `${offset} ${name} — ${city}`;
}

/**
 * Every zone the browser knows, grouped by region and sorted west to east.
 *
 * Built once and kept: this is a few hundred entries and each one costs two
 * Intl formatters to label, which is fine once and wasteful on every render.
 * Offsets only move at a daylight saving boundary, and a page that has been
 * open across one is already showing a stale clock.
 */
let groupsCache = null;

export function zoneGroups() {
  if (groupsCache) return groupsCache;

  const at = new Date();

  let ids;
  try {
    ids = Intl.supportedValuesOf("timeZone");
  } catch {
    ids = FALLBACK_ZONES;
  }

  if (!ids?.length) ids = FALLBACK_ZONES;

  const byRegion = new Map();

  for (const id of ids) {
    if (!isKnownZone(id)) continue;

    const region = REGION_NAMES[id.split("/")[0]] ?? "Other";

    if (!byRegion.has(region)) byRegion.set(region, []);

    byRegion.get(region).push({
      id,
      label: zoneLabel(id, at),
      offset: offsetMinutes(id, at),
    });
  }

  groupsCache = [...byRegion.entries()]
    .map(([region, zones]) => ({
      region,
      zones: zones.sort((a, b) => a.offset - b.offset || a.label.localeCompare(b.label)),
    }))
    .sort((a, b) => a.region.localeCompare(b.region));

  return groupsCache;
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
