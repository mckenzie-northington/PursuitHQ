'use client';

import {
  DAY_NAMES,
  chipTime,
  colorOf,
  isDone,
  iso,
  todayIso,
  isTickable,
} from "@/lib/calendar";
import AssignmentCheckbox from "./AssignmentCheckbox";

const MAX_VISIBLE = 3;

/**
 * The month view.
 *
 * Three different clicks do three different things in a cell, which is what
 * makes a calendar feel right:
 *   - the date number opens that day in Day view
 *   - empty space starts a new event on that day
 *   - an item opens that item
 */
export default function MonthGrid({
  gridDays,
  anchorMonth,
  byDate,
  onDayNumberClick,
  onEmptyClick,
  onItemClick,
  onToggleItem,
}) {
  const today = todayIso();

  return (
    <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
      <div className="grid grid-cols-7 border-b border-slate-200 bg-slate-50">
        {DAY_NAMES.map((name) => (
          <div
            key={name}
            className="px-2 py-2 text-center text-[11px] font-medium uppercase tracking-wide text-slate-500"
          >
            {name}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-7">
        {gridDays.map((day) => {
          const key = iso(day);
          const dayItems = byDate[key] ?? [];
          const inMonth = day.getMonth() === anchorMonth;
          const isToday = key === today;
          const hidden = dayItems.length - MAX_VISIBLE;

          return (
            <div
              key={key}
              onClick={() => onEmptyClick(key)}
              className={`min-h-[7.5rem] cursor-pointer border-b border-r border-slate-200 p-1.5 transition ${
                inMonth ? "bg-white hover:bg-slate-50" : "bg-slate-50/70 hover:bg-slate-100/70"
              }`}
            >
              <div className="mb-1 flex items-center justify-center">
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    onDayNumberClick(key);
                  }}
                  title="Open this day"
                  className={`flex h-6 w-6 items-center justify-center rounded-full text-xs transition ${
                    isToday
                      ? "bg-indigo-600 font-semibold text-white hover:bg-indigo-700"
                      : inMonth
                      ? "text-slate-700 hover:bg-slate-200"
                      : "text-slate-400 hover:bg-slate-200"
                  }`}
                >
                  {day.getDate()}
                </button>
              </div>

              <div className="space-y-0.5">
                {dayItems.slice(0, MAX_VISIBLE).map((item) => (
                  <MonthChip
                    key={item.id}
                    item={item}
                    onClick={() => onItemClick(item)}
                    onToggle={() => onToggleItem(item)}
                  />
                ))}

                {hidden > 0 && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      onDayNumberClick(key);
                    }}
                    className="w-full px-1 text-left text-[10px] font-medium text-slate-500 hover:text-indigo-600"
                  >
                    +{hidden} more
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function MonthChip({ item, onClick, onToggle }) {
  const tickable = isTickable(item);
  const done = isDone(item);

  return (
    <div
      className="flex items-center gap-1 rounded px-1 py-0.5 text-[11px] leading-tight text-white"
      style={{ backgroundColor: colorOf(item), opacity: done ? 0.55 : 1 }}
    >
      {tickable && <AssignmentCheckbox checked={done} onChange={onToggle} onLight={false} />}
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          onClick();
        }}
        title={item.title}
        className={`min-w-0 flex-1 truncate text-left ${done ? "line-through" : ""}`}
      >
        {item.title}
        {chipTime(item) && <span className="opacity-90"> · {chipTime(item)}</span>}
      </button>
    </div>
  );
}
