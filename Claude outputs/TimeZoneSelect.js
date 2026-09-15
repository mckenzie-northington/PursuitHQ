'use client';

import { useMemo } from "react";
import { zoneGroups, zoneLabel, isKnownZone } from "@/lib/timezones";

/**
 * The time zone picker.
 *
 * A plain select rather than a search box: four hundred options sounds like a
 * lot, but they are grouped by region and every browser types ahead within a
 * select, so "chic" still lands on Chicago. A custom combobox would be more to
 * build, more to get wrong for keyboard and screen reader users, and no faster
 * to use.
 */
export default function TimeZoneSelect({ value, onChange, className = "", id, disabled }) {
  const groups = useMemo(() => zoneGroups(), []);

  // A saved zone the browser does not recognise - an old id, or one this
  // machine's tz database has not heard of - would otherwise render as a blank
  // select, and the first edit would silently overwrite it. Showing it as its
  // own option keeps it visible and keeps it intact until it is changed on
  // purpose.
  const unlisted =
    value && !groups.some((group) => group.zones.some((zone) => zone.id === value));

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

      {groups.map((group) => (
        <optgroup key={group.region} label={group.region}>
          {group.zones.map((zone) => (
            <option key={zone.id} value={zone.id}>
              {zone.label}
            </option>
          ))}
        </optgroup>
      ))}
    </select>
  );
}
