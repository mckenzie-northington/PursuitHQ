'use client';

import { Fragment } from "react";

/**
 * A small markdown renderer for AI-written study guides.
 *
 * Hand-written rather than a library on purpose. It renders into React
 * elements and never touches dangerouslySetInnerHTML, so text coming back from
 * a model cannot inject markup into the page - which matters here, because
 * that text is influenced by whatever was inside the student's uploaded files.
 *
 * It handles what a study guide actually uses: headings, bullet and numbered
 * lists, bold, italics, inline code, fenced code blocks, and paragraphs.
 * Tables, images, and links are shown as plain text.
 */
export default function Markdown({ text }) {
  if (!text) return null;

  return <div className="space-y-2 text-sm leading-relaxed">{renderBlocks(text)}</div>;
}

function renderBlocks(text) {
  const lines = text.replace(/\r\n/g, "\n").split("\n");
  const blocks = [];

  let paragraph = [];
  let list = null;
  let code = null;

  const flushParagraph = () => {
    if (paragraph.length === 0) return;
    blocks.push(
      <p key={`p-${blocks.length}`} className="text-slate-700">
        {inline(paragraph.join(" "))}
      </p>
    );
    paragraph = [];
  };

  const flushList = () => {
    if (!list) return;

    const Tag = list.ordered ? "ol" : "ul";
    blocks.push(
      <Tag
        key={`l-${blocks.length}`}
        className={`ml-5 space-y-1 text-slate-700 ${
          list.ordered ? "list-decimal" : "list-disc"
        }`}
      >
        {list.items.map((item, i) => (
          <li key={i}>{inline(item)}</li>
        ))}
      </Tag>
    );
    list = null;
  };

  const flushAll = () => {
    flushParagraph();
    flushList();
  };

  for (const line of lines) {
    // Fenced code blocks swallow everything until the closing fence, so
    // markdown inside them is left alone.
    if (line.trimStart().startsWith("```")) {
      if (code === null) {
        flushAll();
        code = [];
      } else {
        blocks.push(
          <pre
            key={`c-${blocks.length}`}
            className="overflow-x-auto rounded-md bg-slate-900 px-3 py-2 text-xs text-slate-100"
          >
            <code>{code.join("\n")}</code>
          </pre>
        );
        code = null;
      }
      continue;
    }

    if (code !== null) {
      code.push(line);
      continue;
    }

    if (line.trim() === "") {
      flushAll();
      continue;
    }

    const heading = /^(#{1,6})\s+(.*)$/.exec(line);
    if (heading) {
      flushAll();

      const level = heading[1].length;
      const size =
        level <= 1 ? "text-lg" : level === 2 ? "text-base" : "text-sm";

      blocks.push(
        <p key={`h-${blocks.length}`} className={`${size} mt-1 font-semibold text-slate-900`}>
          {inline(heading[2])}
        </p>
      );
      continue;
    }

    const bullet = /^\s*[-*+]\s+(.*)$/.exec(line);
    const numbered = /^\s*\d+[.)]\s+(.*)$/.exec(line);

    if (bullet || numbered) {
      flushParagraph();

      const ordered = Boolean(numbered);
      const content = (bullet ?? numbered)[1];

      // Switching between bullets and numbers starts a new list rather than
      // mixing the two.
      if (!list || list.ordered !== ordered) {
        flushList();
        list = { ordered, items: [] };
      }

      list.items.push(content);
      continue;
    }

    flushList();
    paragraph.push(line.trim());
  }

  if (code !== null) {
    blocks.push(
      <pre
        key={`c-${blocks.length}`}
        className="overflow-x-auto rounded-md bg-slate-900 px-3 py-2 text-xs text-slate-100"
      >
        <code>{code.join("\n")}</code>
      </pre>
    );
  }

  flushAll();
  return blocks;
}

/**
 * Inline markup: `code`, **bold**, *italic*.
 *
 * Split on all three at once so the pieces cannot overlap, and code is matched
 * first so that `**not bold**` inside backticks stays literal.
 */
function inline(text) {
  const pattern = /(`[^`]+`|\*\*[^*]+\*\*|\*[^*]+\*|__[^_]+__|_[^_]+_)/g;
  const parts = text.split(pattern).filter((p) => p !== "");

  return parts.map((part, i) => {
    if (part.startsWith("`") && part.endsWith("`") && part.length > 2) {
      return (
        <code key={i} className="rounded bg-slate-200 px-1 py-0.5 text-[0.85em] text-slate-800">
          {part.slice(1, -1)}
        </code>
      );
    }

    if ((part.startsWith("**") && part.endsWith("**")) || (part.startsWith("__") && part.endsWith("__"))) {
      return (
        <strong key={i} className="font-semibold text-slate-900">
          {part.slice(2, -2)}
        </strong>
      );
    }

    if ((part.startsWith("*") && part.endsWith("*")) || (part.startsWith("_") && part.endsWith("_"))) {
      if (part.length > 2) return <em key={i}>{part.slice(1, -1)}</em>;
    }

    return <Fragment key={i}>{part}</Fragment>;
  });
}
