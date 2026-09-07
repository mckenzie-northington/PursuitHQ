'use client';

import { useEffect, useState } from "react";
import { materials as materialsApi } from "@/lib/api";

/**
 * Shows a file inline instead of downloading it.
 *
 * Images and PDFs render from a blob URL; text files are read as text.
 * Office formats have no browser-native viewer, so those offer a download.
 *
 * Only the types on the server's upload allow-list can reach this, and that
 * list has no HTML or SVG - which matters, because those would be able to run
 * scripts if rendered from a blob URL on this origin.
 */
export default function FilePreview({ courseId, file, onClose, onDownload }) {
  const [url, setUrl] = useState(null);
  const [text, setText] = useState(null);
  const [meta, setMeta] = useState(null);
  const [status, setStatus] = useState("loading");

  const kind = kindOf(file);

  useEffect(() => {
    let objectUrl = null;
    let cancelled = false;

    // Office formats have no browser viewer, so ask the server for their text.
    if (kind === "office") {
      materialsApi
        .text(courseId, file.id)
        .then((result) => {
          if (cancelled) return;
          setText(result.text);
          setMeta(result);
          setStatus("ready");
        })
        .catch((err) => {
          if (cancelled) return;
          setMeta({ message: err.message });
          setStatus("notext");
        });
      return;
    }

    if (kind === "none") {
      setStatus("unsupported");
      return;
    }

    materialsApi
      .preview(courseId, file.id)
      .then(async ({ blob, url: blobUrl }) => {
        if (cancelled) {
          URL.revokeObjectURL(blobUrl);
          return;
        }

        objectUrl = blobUrl;

        if (kind === "text") {
          setText(await blob.text());
        } else {
          setUrl(blobUrl);
        }

        setStatus("ready");
      })
      .catch(() => {
        if (!cancelled) setStatus("error");
      });

    // Blob URLs stay in memory until revoked, so clean up on close.
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [courseId, file.id, kind]);

  // Escape closes the preview.
  useEffect(() => {
    function onKey(e) {
      if (e.key === "Escape") onClose();
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 p-4"
      onClick={onClose}
    >
      <div
        className="flex max-h-full w-full max-w-4xl flex-col overflow-hidden rounded-xl bg-white shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="flex items-center justify-between gap-4 border-b border-slate-200 px-5 py-3">
          <div className="min-w-0">
            <p className="truncate font-medium text-slate-900">{file.fileName}</p>
            <p className="text-xs text-slate-500">{formatBytes(file.sizeBytes)}</p>
          </div>
          <div className="flex shrink-0 items-center gap-3 text-sm">
            <button onClick={onDownload} className="font-medium text-indigo-600 hover:underline">
              Download
            </button>
            <button
              onClick={onClose}
              className="rounded-md border border-slate-300 px-3 py-1.5 text-slate-700 hover:bg-slate-50"
            >
              Close
            </button>
          </div>
        </header>

        <div className="min-h-[200px] flex-1 overflow-auto bg-slate-50">
          {status === "loading" && (
            <div className="flex h-64 items-center justify-center text-sm text-slate-500">
              Loading preview...
            </div>
          )}

          {status === "error" && (
            <div className="flex h-64 flex-col items-center justify-center gap-2 text-sm">
              <p className="text-slate-700">Could not load this file.</p>
              <button onClick={onDownload} className="font-medium text-indigo-600 hover:underline">
                Download instead
              </button>
            </div>
          )}

          {status === "notext" && (
            <div className="flex h-64 flex-col items-center justify-center gap-2 px-6 text-center">
              <p className="text-3xl">{iconFor(file.fileName)}</p>
              <p className="max-w-md text-sm text-slate-700">
                {meta?.message || "No readable text could be extracted from this file."}
              </p>
              <button onClick={onDownload} className="mt-1 rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700">
                Download to open
              </button>
            </div>
          )}

          {status === "unsupported" && (
            <div className="flex h-64 flex-col items-center justify-center gap-2 px-6 text-center">
              <p className="text-3xl">{iconFor(file.fileName)}</p>
              <p className="text-sm text-slate-700">
                Word, PowerPoint, and Excel files cannot be previewed in the browser.
              </p>
              <button onClick={onDownload} className="mt-1 rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700">
                Download to open
              </button>
            </div>
          )}

          {status === "ready" && kind === "image" && (
            <div className="flex items-center justify-center p-4">
              <img src={url} alt={file.fileName} className="max-h-[70vh] max-w-full object-contain" />
            </div>
          )}

          {status === "ready" && kind === "pdf" && (
            <iframe src={url} title={file.fileName} className="h-[75vh] w-full border-0 bg-white" />
          )}

          {status === "ready" && (kind === "text" || kind === "office") && (
            <div>
              {kind === "office" && meta && (
                <div className="sticky top-0 border-b border-slate-200 bg-amber-50 px-5 py-2 text-xs text-amber-900">
                  Text extracted from {meta.sectionCount} {meta.sectionLabel}. Images and
                  formatting are not shown
                  {meta.truncated && " — this document was long, so only the first part is shown"}.{" "}
                  <button onClick={onDownload} className="font-medium underline">
                    Download the original
                  </button>{" "}
                  to see it properly.
                </div>
              )}
              <pre className="whitespace-pre-wrap p-5 font-mono text-sm leading-relaxed text-slate-800">
                {text}
              </pre>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function kindOf(file) {
  const ext = file.fileName.split(".").pop()?.toLowerCase();
  const type = (file.contentType || "").toLowerCase();

  if (type.startsWith("image/") || ["png", "jpg", "jpeg", "gif"].includes(ext)) return "image";
  if (type === "application/pdf" || ext === "pdf") return "pdf";
  if (type.startsWith("text/") || ["txt", "md"].includes(ext)) return "text";
  if (["docx", "pptx", "xlsx", "csv"].includes(ext)) return "office";
  return "none";
}

function formatBytes(bytes) {
  if (!bytes) return "0 B";
  const units = ["B", "KB", "MB", "GB"];
  const i = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  return `${(bytes / Math.pow(1024, i)).toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
}

function iconFor(fileName) {
  const ext = fileName.split(".").pop()?.toLowerCase();
  if (["docx", "doc"].includes(ext)) return "📃";
  if (["pptx", "ppt"].includes(ext)) return "📊";
  if (["xlsx", "xls"].includes(ext)) return "📈";
  return "📎";
}
