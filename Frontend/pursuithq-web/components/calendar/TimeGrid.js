'use client';

import { useEffect, useState } from "react";
import {
  DAY_NAMES,
  GRID_END_HOUR,
  GRID_START_HOUR,
  HOUR_HEIGHT,
  TYPE,
  chipTime,
  colorOf,
  hourLabel,
  isDone,
  iso,
  layoutOverlaps,
  timeRangeOf,
  todayIso,
  toTimeValue,
} from "@/lib/calendar";
import AssignmentCheckbox from "./AssignmentCheckbox";

const HOURS = Array.from(
  { length: GRID_END_HOUR - GRID_START_HOUR + 1 },
  (_, i) => GRID_START_HOUR + i
);

const GRID_HEIGHT = (GRID_END_HOUR - GRID_START_HOUR) * HOUR_HEIGHT;

/**
 * The hour-by-hour schedule, used for both Day view (one column) and Week
 * view (seven). One component covers both because the only difference is how
 * many day columns are handed in.
 *
 * All-day things - assignment due dates, all-day events - sit in a strip above
 * the grid rather than being crammed into a time slot, because they do not
 * happen at a time.
 */
export default function TimeGrid({
  days,
  byDate,
  onSlotClick,
  onItemClick,
  onToggleAssignment,
  onDayClick,
}) {
  const today = todayIso();
  const nowMinutes = useNowMinutes();

  return (
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
      {/* Day headers */}
      <div className="flex border-b border-slate-200 bg-slate-50">
        <div className="w-16 shrink-0 border-r border-slate-200" />
        {days.map((day) => {
          const key = iso(day);
          const isToday = key === today;

          return (
            <button
              key={key}
              type="button"
              onClick={() => onDayClick?.(key)}
              className="min-w-0 flex-1 border-r border-slate-200 px-2 py-2 text-center transition last:border-r-0 hover:bg-slate-100"
            >
              <div className="text-[11px] font-medium uppercase tracking-wide text-slate-500">
                {DAY_NAMES[day.getDay()]}
              </div>
              <div
                className={`mx-auto mt-0.5 flex h-7 w-7 items-center justify-center rounded-full text-sm ${
                  isToday ? "bg-indigo-600 font-semibold text-white" : "text-slate-800"
                }`}
              >
                {day.getDate()}
              </div>
            </button>
          );
        })}
      </div>

      {/* All-day strip */}
      <div className="flex border-b border-slate-200 bg-white">
        <div className="flex w-16 shrink-0 items-start justify-end border-r border-slate-200 px-2 py-1.5 text-[10px] uppercase tracking-wide text-slate-400">
          All day
        </div>
        {days.map((day) => {
          const key = iso(day);
          const allDay = (byDate[key] ?? []).filter((i) => i.isAllDay);

          return (
            <div
              key={key}
              className="min-w-0 flex-1 space-y-1 border-r border-slate-200 p-1 last:border-r-0"
              style={{ minHeight: "2.25rem" }}
            >
              {allDay.map((item) => (
                <AllDayChip
                  key={item.id}
                  item={item}
                  onClick={() => onItemClick(item)}
                  onToggle={() => onToggleAssignment(item)}
                />
              ))}
            </div>
          );
        })}
      </div>

      {/* Hour grid */}
      <div className="max-h-[34rem] overflow-y-auto">
        <div className="flex pt-3">
          {/* Time gutter */}
          <div className="relative w-16 shrink-0 border-r border-slate-200" style={{ height: GRID_HEIGHT }}>
            {HOURS.map((hour) => (
              <span
                key={hour}
                className="absolute right-2 -translate-y-1/2 text-[10px] text-slate-400"
                style={{ top: (hour - GRID_START_HOUR) * HOUR_HEIGHT }}
              >
                {hourLabel(hour)}
              </span>
            ))}
          </div>

          {days.map((day) => {
            const key = iso(day);
            const timed = (byDate[key] ?? []).filter((i) => !i.isAllDay && i.startTime);

            return (
              <DayColumn
                key={key}
                dateStr={key}
                items={timed}
                isToday={key === today}
                nowMinutes={nowMinutes}
                onSlotClick={onSlotClick}
                onItemClick={onItemClick}
              />
            );
          })}
        </div>
      </div>
    </div>
  );
}

/** One day's worth of the hour grid. */
function DayColumn({ dateStr, items, isToday, nowMinutes, onSlotClick, onItemClick }) {
  const laid = layoutOverlaps(items);

  /**
   * Turns a click anywhere in the column into the half-hour slot it landed in.
   *
   * Measuring against the column's own box rather than reading offsetY means a
   * click that lands on a child element still reports the right position -
   * offsetY would be relative to whatever was clicked.
   */
  function handleClick(e) {
    const rect = e.currentTarget.getBoundingClientRect();
    const y = e.clientY - rect.top;
    const rawMinutes = GRID_START_HOUR * 60 + (y / HOUR_HEIGHT) * 60;
    const snapped = Math.floor(rawMinutes / 30) * 30;

    onSlotClick(dateStr, toTimeValue(snapped));
  }

  return (
    <div
      onClick={handleClick}
      className="relative min-w-0 flex-1 cursor-pointer border-r border-slate-200 last:border-r-0"
      style={{ height: GRID_HEIGHT }}
    >
      {/* Hour lines, plus a lighter one on the half hour. */}
      {HOURS.map((hour) => (
        <div
          key={hour}
          className="pointer-events-none absolute inset-x-0 border-t border-slate-200"
          style={{ top: (hour - GRID_START_HOUR) * HOUR_HEIGHT }}
        />
      ))}
      {HOURS.slice(0, -1).map((hour) => (
        <div
          key={`half-${hour}`}
          className="pointer-events-none absolute inset-x-0 border-t border-dashed border-slate-100"
          style={{ top: (hour - GRID_START_HOUR) * HOUR_HEIGHT + HOUR_HEIGHT / 2 }}
        />
      ))}

      {/* Where we are right now. */}
      {isToday && nowMinutes >= GRID_START_HOUR * 60 && nowMinutes <= GRID_END_HOUR * 60 && (
        <div
          className="pointer-events-none absolute inset-x-0 z-20 border-t-2 border-red-500"
          style={{ top: ((nowMinutes - GRID_START_HOUR * 60) / 60) * HOUR_HEIGHT }}
        >
          <span className="absolute -left-1 -top-[5px] block h-2 w-2 rounded-full bg-red-500" />
        </div>
      )}

      {laid.map(({ item, start, end, column, columns }) => {
        // An event that starts before the grid does gets clipped to the top
        // rather than drawn off-screen above it.
        const top = Math.max(((start - GRID_START_HOUR * 60) / 60) * HOUR_HEIGHT, 0);
        const bottom = ((end - GRID_START_HOUR * 60) / 60) * HOUR_HEIGHT;
        const height = Math.max(bottom - top, 18);
        const color = colorOf(item);
        const short = height < 34;

        return (
          <button
            key={item.id}
            type="button"
            onClick={(e) => {
              // Otherwise this also reads as a click on empty space and opens
              // the "new event" dialog on top of the one being opened.
              e.stopPropagation();
              onItemClick(item);
            }}
            title={`${item.title} - ${timeRangeOf(item)}`}
            style={{
              top,
              height,
              backgroundColor: color,
              left: `calc(${(column * 100) / columns}% + 2px)`,
              width: `calc(${100 / columns}% - 4px)`,
            }}
            className="absolute z-10 overflow-hidden rounded px-1.5 py-0.5 text-left text-[11px] leading-tight text-white shadow-sm transition hover:brightness-110"
          >
            <span className="block truncate font-medium">{item.title}</span>
            {!short && (
              <span className="block truncate opacity-90">
                {chipTime(item)}
                {item.location ? ` · ${item.location}` : ""}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}

/** An all-day item in the strip: assignments get a checkbox, events do not. */
function AllDayChip({ item, onClick, onToggle }) {
  const color = colorOf(item);
  const assignment = item.type === TYPE.ASSIGNMENT;
  const done = isDone(item);

  return (
    <div
      className="flex items-center gap-1.5 rounded px-1.5 py-0.5 text-[11px] leading-tight text-white"
      style={{ backgroundColor: color, opacity: done ? 0.55 : 1 }}
    >
      {assignment && <AssignmentCheckbox checked={done} onChange={onToggle} onLight={false} />}
      <button
        type="button"
        onClick={onClick}
        className={`min-w-0 flex-1 truncate text-left ${done ? "line-through" : ""}`}
        title={item.title}
      >
        {item.title}
      </button>
    </div>
  );
}

/**
 * Minutes since midnight, refreshed every minute so the "now" line creeps down
 * the day instead of freezing wherever it was when the page loaded.
 */
function useNowMinutes() {
  const [minutes, setMinutes] = useState(() => currentMinutes());

  useEffect(() => {
    const id = setInterval(() => setMinutes(currentMinutes()), 60_000);
    return () => clearInterval(id);
  }, []);

  return minutes;
}

function currentMinutes() {
  const now = new Date();
  return now.getHours() * 60 + now.getMinutes();
}
