'use client';

import { useMemo } from "react";
import { zoneOptions, zoneLabel, isKnownZone } from "@/lib/timezones";

/**
 * The time zone picker: the US zones, east to west.
 */
export default function TimeZoneSelect({ value, onChange, className = "", id, disabled }) {
  const options = useMemo(() => zoneOptions(), []);

  // A saved zone outside the list - someone who set it before the list was
  // trimmed, or whose account was created abroad - would otherwise render as a
  // blank select, and the next save would overwrite it with whatever happened
  // to be first. Showing it keeps it visible and keeps it intact until it is
  // changed on purpose.
  const unlisted = value && !options.some((zone) => zone.id === value);

  return (
    <select
      id={id}
      value={value ?? ""}
      disabled={disabled}
      onChange={(e) => onChange(e.target.value)}
      className={className}
    >
      {unlisted && (
        <option value={value}>
          {isKnownZone(value) ? zoneLabel(value) : `${value} (not recognised)`}
        </option>
      )}

      {options.map((zone) => (
        <option key={zone.id} value={zone.id}>
          {zone.label}
        </option>
      ))}
    </select>
  );
}
