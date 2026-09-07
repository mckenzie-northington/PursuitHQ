'use client';

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { courses as coursesApi, folders as foldersApi, materials as materialsApi, notes as notesApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

export default function MaterialsPage() {
  const { id } = useParams();
  const courseId = Number(id);
  const { user, loading } = useAuth();

  const [course, setCourse] = useState(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  // Breadcrumb trail. The last entry is the folder currently being viewed;
  // null id means the course root.
  const [trail, setTrail] = useState([{ id: null, name: "All materials" }]);
  const currentFolderId = trail[trail.length - 1].id;

  const [subfolders, setSubfolders] = useState([]);
  const [files, setFiles] = useState([]);
  const [noteList, setNoteList] = useState([]);
  const [usage, setUsage] = useState(null);

  const [newFolderName, setNewFolderName] = useState("");
  const [showNewFolder, setShowNewFolder] = useState(false);
  const [uploading, setUploading] = useState(false);
  const fileInput = useRef(null);

  const [openNote, setOpenNote] = useState(null);
  const [noteDraft, setNoteDraft] = useState({ title: "", content: "" });
  const [savingNote, setSavingNote] = useState(false);

  const refresh = useCallback(async () => {
    try {
      const [f, m, n, u] = await Promise.all([
        foldersApi.list(courseId, currentFolderId),
        materialsApi.list(courseId, currentFolderId),
        notesApi.list(courseId, currentFolderId),
        materialsApi.usage(),
      ]);
      setSubfolders(f);
      setFiles(m);
      setNoteList(n);
      setUsage(u);
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }, [courseId, currentFolderId]);

  useEffect(() => {
    if (loading || !user) return;
    coursesApi.get(courseId).then(setCourse).catch((e) => setError(e.message));
  }, [loading, user, courseId]);

  useEffect(() => {
    if (loading || !user) return;
    refresh();
  }, [loading, user, refresh]);

  function openFolder(folder) {
    setTrail((t) => [...t, { id: folder.id, name: folder.name }]);
  }

  function jumpTo(index) {
    setTrail((t) => t.slice(0, index + 1));
  }

  async function createFolder(e) {
    e.preventDefault();
    if (!newFolderName.trim()) return;

    try {
      await foldersApi.create(courseId, {
        name: newFolderName.trim(),
        parentFolderId: currentFolderId,
      });
      setNewFolderName("");
      setShowNewFolder(false);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  async function deleteFolder(folder) {
    if (!confirm(`Delete "${folder.name}" and everything inside it? This cannot be undone.`)) return;
    try {
      await foldersApi.remove(courseId, folder.id);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  async function handleUpload(e) {
    const chosen = Array.from(e.target.files || []);
    if (chosen.length === 0) return;

    setUploading(true);
    setError("");

    // Uploaded one at a time so a single rejected file does not lose the rest.
    for (const file of chosen) {
      try {
        await materialsApi.upload(courseId, file, currentFolderId, null);
      } catch (err) {
        setError(`${file.name}: ${err.message}`);
      }
    }

    setUploading(false);
    if (fileInput.current) fileInput.current.value = "";
    await refresh();
  }

  async function deleteFile(file) {
    if (!confirm(`Delete "${file.fileName}"?`)) return;
    try {
      await materialsApi.remove(courseId, file.id);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  async function saveNote(e) {
    e.preventDefault();
    setSavingNote(true);

    try {
      if (openNote === "new") {
        await notesApi.create(courseId, { ...noteDraft, folderId: currentFolderId });
      } else {
        await notesApi.update(courseId, openNote.id, { ...noteDraft, folderId: openNote.folderId });
      }
      setOpenNote(null);
      await refresh();
    } catch (err) {
      setError(err.message);
    } finally {
      setSavingNote(false);
    }
  }

  async function editNote(note) {
    try {
      // The list view omits note bodies, so fetch the full note to edit it.
      const full = await notesApi.get(courseId, note.id);
      setOpenNote(full);
      setNoteDraft({ title: full.title, content: full.content });
    } catch (err) {
      setError(err.message);
    }
  }

  async function deleteNote(note) {
    if (!confirm(`Delete "${note.title}"?`)) return;
    try {
      await notesApi.remove(courseId, note.id);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-5xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  const isEmpty = subfolders.length === 0 && files.length === 0 && noteList.length === 0;

  return (
    <div className="mx-auto max-w-5xl px-6 py-10">
      <Link href="/courses" className="text-sm font-medium text-indigo-600 hover:underline">
        &larr; Courses
      </Link>

      <div className="mt-2 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">{course?.name ?? "Study materials"}</h1>
          <p className="mt-1 text-sm text-slate-600">Files and notes for this course.</p>
        </div>

        {usage && (
          <div className="text-right text-sm text-slate-500">
            <p>{formatBytes(usage.usedBytes)} of {formatBytes(usage.quotaBytes)} used</p>
            <div className="mt-1 h-1.5 w-40 overflow-hidden rounded-full bg-slate-200">
              <div
                className="h-full bg-indigo-500"
                style={{ width: `${Math.min(100, (usage.usedBytes / usage.quotaBytes) * 100)}%` }}
              />
            </div>
          </div>
        )}
      </div>

      {/* Breadcrumbs */}
      <nav className="mt-6 flex flex-wrap items-center gap-1 text-sm">
        {trail.map((crumb, i) => (
          <span key={`${crumb.id}-${i}`} className="flex items-center gap-1">
            {i > 0 && <span className="text-slate-400">/</span>}
            {i === trail.length - 1 ? (
              <span className="font-medium text-slate-900">{crumb.name}</span>
            ) : (
              <button onClick={() => jumpTo(i)} className="text-indigo-600 hover:underline">
                {crumb.name}
              </button>
            )}
          </span>
        ))}
      </nav>

      {/* Actions */}
      <div className="mt-4 flex flex-wrap gap-2">
        <button
          onClick={() => fileInput.current?.click()}
          disabled={uploading}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {uploading ? "Uploading..." : "Upload files"}
        </button>
        <input ref={fileInput} type="file" multiple onChange={handleUpload} className="hidden" />

        <button
          onClick={() => setShowNewFolder(!showNewFolder)}
          className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          New folder
        </button>

        <button
          onClick={() => {
            setOpenNote("new");
            setNoteDraft({ title: "", content: "" });
          }}
          className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          New note
        </button>
      </div>

      <p className="mt-2 text-xs text-slate-500">
        PDF, Word, PowerPoint, Excel, text, and images. 25 MB per file.
      </p>

      {showNewFolder && (
        <form onSubmit={createFolder} className="mt-3 flex gap-2">
          <input
            autoFocus
            value={newFolderName}
            onChange={(e) => setNewFolderName(e.target.value)}
            placeholder="Folder name"
            className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
          />
          <button type="submit" className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700">
            Create
          </button>
        </form>
      )}

      {error && (
        <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Note editor */}
      {openNote && (
        <form onSubmit={saveNote} className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
          <h2 className="font-medium">{openNote === "new" ? "New note" : "Edit note"}</h2>

          <input
            required
            value={noteDraft.title}
            onChange={(e) => setNoteDraft({ ...noteDraft, title: e.target.value })}
            placeholder="Title"
            className="mt-3 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
          />

          <textarea
            rows={10}
            value={noteDraft.content}
            onChange={(e) => setNoteDraft({ ...noteDraft, content: e.target.value })}
            placeholder="Write your notes here..."
            className="mt-3 w-full rounded-md border border-slate-300 px-3 py-2 font-mono text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
          />

          <div className="mt-4 flex gap-2">
            <button type="submit" disabled={savingNote} className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
              {savingNote ? "Saving..." : "Save note"}
            </button>
            <button type="button" onClick={() => setOpenNote(null)} className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
              Cancel
            </button>
          </div>
        </form>
      )}

      {/* Contents */}
      <div className="mt-6 overflow-hidden rounded-xl border border-slate-200 bg-white">
        {isEmpty ? (
          <div className="px-6 py-12 text-center text-sm text-slate-600">
            This folder is empty. Upload a file, or create a note.
          </div>
        ) : (
          <ul className="divide-y divide-slate-200">
            {subfolders.map((f) => (
              <li key={`folder-${f.id}`} className="flex items-center justify-between gap-4 px-4 py-3 hover:bg-slate-50">
                <button onClick={() => openFolder(f)} className="flex min-w-0 items-center gap-3 text-left">
                  <span className="text-lg">📁</span>
                  <span className="min-w-0">
                    <span className="block truncate font-medium text-slate-900">{f.name}</span>
                    <span className="text-xs text-slate-500">
                      {f.subfolderCount} folders · {f.fileCount} files · {f.noteCount} notes
                    </span>
                  </span>
                </button>
                <button onClick={() => deleteFolder(f)} className="shrink-0 text-sm text-slate-400 hover:text-red-600">
                  Delete
                </button>
              </li>
            ))}

            {files.map((f) => (
              <li key={`file-${f.id}`} className="flex items-center justify-between gap-4 px-4 py-3 hover:bg-slate-50">
                <div className="flex min-w-0 items-center gap-3">
                  <span className="text-lg">{iconFor(f.fileName)}</span>
                  <div className="min-w-0">
                    <p className="truncate font-medium text-slate-900">{f.fileName}</p>
                    <p className="text-xs text-slate-500">
                      {formatBytes(f.sizeBytes)} · {new Date(f.uploadedAt).toLocaleDateString()}
                    </p>
                  </div>
                </div>
                <div className="flex shrink-0 gap-3 text-sm">
                  <button
                    onClick={() => materialsApi.download(courseId, f.id, f.fileName)}
                    className="font-medium text-indigo-600 hover:underline"
                  >
                    Download
                  </button>
                  <button onClick={() => deleteFile(f)} className="text-slate-400 hover:text-red-600">
                    Delete
                  </button>
                </div>
              </li>
            ))}

            {noteList.map((n) => (
              <li key={`note-${n.id}`} className="flex items-center justify-between gap-4 px-4 py-3 hover:bg-slate-50">
                <button onClick={() => editNote(n)} className="flex min-w-0 items-center gap-3 text-left">
                  <span className="text-lg">📝</span>
                  <span className="min-w-0">
                    <span className="block truncate font-medium text-slate-900">{n.title}</span>
                    <span className="text-xs text-slate-500">
                      Note · updated {new Date(n.updatedAt).toLocaleDateString()}
                    </span>
                  </span>
                </button>
                <button onClick={() => deleteNote(n)} className="shrink-0 text-sm text-slate-400 hover:text-red-600">
                  Delete
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function formatBytes(bytes) {
  if (!bytes) return "0 B";
  const units = ["B", "KB", "MB", "GB"];
  const i = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  return `${(bytes / Math.pow(1024, i)).toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
}

function iconFor(fileName) {
  const ext = fileName.split(".").pop()?.toLowerCase();
  if (ext === "pdf") return "📄";
  if (["png", "jpg", "jpeg", "gif"].includes(ext)) return "🖼️";
  if (["docx", "doc"].includes(ext)) return "📃";
  if (["pptx", "ppt"].includes(ext)) return "📊";
  if (["xlsx", "xls", "csv"].includes(ext)) return "📈";
  return "📎";
}
