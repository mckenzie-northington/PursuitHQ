'use client';

import Link from "next/link";

const TYPE_LABEL = {
  multiple_choice: "Multiple choice",
  true_false: "True / false",
  short_answer: "Written",
};

/**
 * A practice test as it appears in the chat, before it is saved.
 *
 * Shows the questions but never the answers. Reading the key while deciding
 * whether to keep a test would spoil the test you are about to sit.
 */
export default function TestPreview({ content, title, savedQuizId, onSave }) {
  const questions = parseQuestions(content);

  if (questions.length === 0) {
    return (
      <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
        A practice test came back, but it could not be read as questions. Ask for it again.
      </div>
    );
  }

  const counts = questions.reduce((acc, q) => {
    const label = TYPE_LABEL[q.type] ?? TYPE_LABEL.multiple_choice;
    acc[label] = (acc[label] ?? 0) + 1;
    return acc;
  }, {});

  return (
    <div className="rounded-xl border border-slate-200 bg-white">
      <div className="flex items-center justify-between gap-3 border-b border-slate-200 px-4 py-2.5">
        <div className="min-w-0">
          <p className="text-[11px] font-medium uppercase tracking-wide text-slate-400">
            Practice test · {questions.length} questions
          </p>
          <p className="truncate text-sm font-medium text-slate-900">{title}</p>
          <p className="truncate text-xs text-slate-500">
            {Object.entries(counts)
              .map(([label, n]) => `${n} ${label.toLowerCase()}`)
              .join(" · ")}
          </p>
        </div>

        {savedQuizId ? (
          <Link
            href={`/tests/${savedQuizId}`}
            className="shrink-0 rounded-md bg-green-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-green-700"
          >
            Take it
          </Link>
        ) : (
          <button
            onClick={onSave}
            className="shrink-0 rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-indigo-700"
          >
            Save
          </button>
        )}
      </div>

      <ol className="max-h-80 space-y-2 overflow-y-auto px-4 py-3 text-sm">
        {questions.map((q, i) => (
          <li key={i} className="text-slate-700">
            <span className="mr-1.5 text-slate-400">{i + 1}.</span>
            {q.question}
            {q.type === "multiple_choice" && q.options?.length > 0 && (
              <ul className="ml-5 mt-1 list-[lower-alpha] text-slate-500">
                {q.options.map((option, j) => (
                  <li key={j}>{option}</li>
                ))}
              </ul>
            )}
          </li>
        ))}
      </ol>
    </div>
  );
}

/**
 * Reads the JSON the model produced.
 *
 * Locates the array by bracket depth rather than trusting the whole string to
 * be JSON, since models routinely wrap it in a sentence or a code fence. This
 * is a preview only — the server parses it again properly when you save, so a
 * disagreement here costs a preview, not a test.
 */
function parseQuestions(content) {
  const start = content.indexOf("[");
  if (start < 0) return [];

  let depth = 0;
  let inString = false;
  let escaped = false;

  for (let i = start; i < content.length; i++) {
    const c = content[i];

    if (inString) {
      if (escaped) escaped = false;
      else if (c === "\\") escaped = true;
      else if (c === '"') inString = false;
      continue;
    }

    if (c === '"') inString = true;
    else if (c === "[") depth++;
    else if (c === "]") {
      depth--;
      if (depth === 0) {
        try {
          const parsed = JSON.parse(content.slice(start, i + 1));
          return Array.isArray(parsed) ? parsed.filter((q) => q?.question) : [];
        } catch {
          return [];
        }
      }
    }
  }

  return [];
}
