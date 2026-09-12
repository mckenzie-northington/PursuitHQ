/**
 * Shared calendar vocabulary and date helpers.
 *
 * The calendar page, the month grid and the hour grid all need the same
 * constants and the same idea of "what day is this", so they live here rather
 * than being copied three times and drifting apart.
 *
 * A note on dates: this app treats a date as a local wall-clock day, never as
 * an instant. The API sends dates as "YYYY-MM-DD" and times as "HH:MM:SS" with
 * no timezone, and we keep it that way end to end. toISOString() is never used
 * for a date, because it converts to UTC and will hand you the previous day for
 * anyone west of Greenwich.
 */

/**
 * Matches CalendarItemType in the API.
 *
 * 2 was study sessions, removed in September 2026. The number is left out
 * rather than reused: old rows and any saved state still mean what they meant.
 */
export const TYPE = { CLASS: 0, ASSIGNMENT: 1, EVENT: 3, REMINDER: 4 };

/**
 * Things with a tick box: they are done or they are not.
 *
 * A class or an event simply happens - there is nothing to complete - so only
 * these two get a checkbox on the calendar.
 */
export function isTickable(item) {
  return item.type === TYPE.ASSIGNMENT || item.type === TYPE.REMINDER;
}

export const TYPE_LABEL = {
  0: "Class",
  1: "Assignment",
  3: "Event",
  4: "Reminder",
};

/** Fallback colors for items with no course color of their own. */
export const TYPE_COLOR = {
  0: "#6366f1",
  1: "#ef4444",
  3: "#0ea5e9",
  4: "#8b5cf6",
};

/** Matches the EventType enum in the API. */
export const EVENT_TYPES = {
  0: "Work",
  1: "Club",
  2: "Appointment",
  3: "Personal",
  4: "Other",
};

export const DAY_NAMES = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

/** iCal BYDAY codes, in the order a week is displayed. */
export const WEEKDAY_CODES = [
  ["SU", "Sun"],
  ["MO", "Mon"],
  ["TU", "Tue"],
  ["WE", "Wed"],
  ["TH", "Thu"],
  ["FR", "Fri"],
  ["SA", "Sat"],
];

/** A palette that reads well as a solid block with white text on it. */
export const EVENT_COLORS = [
  "#0ea5e9",
  "#6366f1",
  "#8b5cf6",
  "#ec4899",
  "#ef4444",
  "#f59e0b",
  "#10b981",
  "#64748b",
];

/** The hour grid runs 6 AM to midnight - earlier rows are dead space. */
export const GRID_START_HOUR = 6;
export const GRID_END_HOUR = 24;
/** Pixels per hour. Everything in the hour grid is positioned off this. */
export const HOUR_HEIGHT = 48;

// --- dates -----------------------------------------------------------------

/** Local YYYY-MM-DD. Not toISOString, which shifts to UTC and can slip a day. */
export function iso(date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

/** "2026-09-08" to a local Date at midnight. */
export function fromIso(value) {
  const [y, m, d] = value.split("-").map(Number);
  return new Date(y, m - 1, d);
}

export function startOfToday() {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  return d;
}

export function addDays(date, count) {
  const d = new Date(date);
  d.setDate(d.getDate() + count);
  return d;
}

export function todayIso() {
  return iso(new Date());
}

// --- times -----------------------------------------------------------------

/** "14:30:00" to 870. Returns null for anything unparseable. */
export function minutesOf(time) {
  if (!time) return null;
  const [h, m] = time.split(":").map(Number);
  if (Number.isNaN(h) || Number.isNaN(m)) return null;
  return h * 60 + m;
}

/** 870 to "14:30", which is what an <input type="time"> wants. */
export function toTimeValue(minutes) {
  const clamped = Math.max(0, Math.min(23 * 60 + 59, minutes));
  const h = String(Math.floor(clamped / 60)).padStart(2, "0");
  const m = String(clamped % 60).padStart(2, "0");
  return `${h}:${m}`;
}

/** "14:30:00" to "2:30 PM". */
export function formatTime(time) {
  const minutes = minutesOf(time);
  if (minutes === null) return "";

  const h24 = Math.floor(minutes / 60);
  const m = minutes % 60;
  const suffix = h24 >= 12 ? "PM" : "AM";
  const h12 = h24 % 12 === 0 ? 12 : h24 % 12;

  return m === 0 ? `${h12} ${suffix}` : `${h12}:${String(m).padStart(2, "0")} ${suffix}`;
}

/** The label for an hour row in the gutter: 13 to "1 PM". */
export function hourLabel(hour) {
  if (hour === 0 || hour === 24) return "12 AM";
  if (hour === 12) return "12 PM";
  return hour > 12 ? `${hour - 12} PM` : `${hour} AM`;
}

export function longDate(value) {
  return fromIso(value).toLocaleDateString(undefined, {
    weekday: "long",
    month: "long",
    day: "numeric",
    year: "numeric",
  });
}

// --- items -----------------------------------------------------------------

export function colorOf(item) {
  return item.colorHex || TYPE_COLOR[item.type];
}

export function isDone(item) {
  return item.status === "Completed";
}

/**
 * The time shown on a chip in the month grid or an hour block: the full range,
 * "10 AM - 11 AM", not just the start. Empty for anything all-day, since those
 * do not happen at a time and the chip has no room to waste.
 */
export function chipTime(item) {
  if (item.isAllDay || !item.startTime) return "";

  const start = formatTime(item.startTime);
  return item.endTime ? `${start} - ${formatTime(item.endTime)}` : start;
}

/**
 * The time range as text: "9:00 AM - 10:15 AM", or "due 11:59 PM" for an
 * assignment, or "All day" for something with no time at all.
 */
export function timeRangeOf(item) {
  if (item.type === TYPE.ASSIGNMENT) {
    return item.startTime ? `due ${formatTime(item.startTime)}` : "due today";
  }
  if (item.isAllDay || !item.startTime) return "All day";

  const start = formatTime(item.startTime);
  return item.endTime ? `${start} - ${formatTime(item.endTime)}` : start;
}

/**
 * Lays overlapping events out side by side, the way a real calendar does.
 *
 * Items are swept in start order and collected into clusters that overlap each
 * other. Within a cluster each item takes the first column whose last item has
 * already finished, so two events at the same time sit next to each other
 * instead of one hiding the other. Returns each item with the column it landed
 * in and how many columns the cluster ended up needing.
 */
export function layoutOverlaps(items) {
  const sorted = [...items].sort(
    (a, b) => (minutesOf(a.startTime) ?? 0) - (minutesOf(b.startTime) ?? 0)
  );

  const placed = [];
  let cluster = [];
  let columnEnds = [];

  const flush = () => {
    for (const entry of cluster) entry.columns = columnEnds.length;
    placed.push(...cluster);
    cluster = [];
    columnEnds = [];
  };

  for (const item of sorted) {
    const start = minutesOf(item.startTime) ?? 0;
    // A zero-length or missing end still needs to be clickable.
    const end = Math.max(minutesOf(item.endTime) ?? start + 30, start + 20);

    // Nothing in the cluster is still running, so this starts a fresh one.
    if (cluster.length > 0 && columnEnds.every((columnEnd) => columnEnd <= start)) {
      flush();
    }

    let column = columnEnds.findIndex((columnEnd) => columnEnd <= start);
    if (column === -1) {
      column = columnEnds.length;
      columnEnds.push(end);
    } else {
      columnEnds[column] = end;
    }

    cluster.push({ item, start, end, column, columns: 1 });
  }

  flush();
  return placed;
}
