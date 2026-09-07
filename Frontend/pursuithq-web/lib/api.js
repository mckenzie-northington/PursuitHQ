// ---------------------------------------------------------------------------
// The single place the website talks to the API.
//
// Every page calls these helpers instead of using fetch directly, so the token
// handling and error handling live in one file rather than being repeated.
// ---------------------------------------------------------------------------

const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5051";

const TOKEN_KEY = "pursuithq_token";
const USER_KEY = "pursuithq_user";

// --- token storage ---------------------------------------------------------
// NOTE: localStorage is convenient but readable by any script on the page.
// Before PursuitHQ opens to other people, this should move to an httpOnly
// cookie (see docs/Security.md). Fine for local development.

export function getToken() {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TOKEN_KEY);
}

export function setSession(token, user) {
  localStorage.setItem(TOKEN_KEY, token);
  localStorage.setItem(USER_KEY, JSON.stringify(user));
}

export function getStoredUser() {
  if (typeof window === "undefined") return null;
  const raw = localStorage.getItem(USER_KEY);
  return raw ? JSON.parse(raw) : null;
}

export function clearSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

// --- the request helper ----------------------------------------------------

export class ApiError extends Error {
  constructor(message, status, details) {
    super(message);
    this.status = status;
    this.details = details;
  }
}

async function request(path, { method = "GET", body } = {}) {
  const headers = { "Content-Type": "application/json" };

  const token = getToken();
  if (token) {
    // This is what proves who you are to the API on every request.
    headers.Authorization = `Bearer ${token}`;
  }

  let res;
  try {
    res = await fetch(`${API_URL}${path}`, {
      method,
      headers,
      // body !== undefined, not a truthiness check: a bare 0 is a valid body
      // (it is the Saved status), and `body ? ...` would silently drop it.
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw new ApiError(
      "Could not reach the API. Is it running on " + API_URL + "?",
      0
    );
  }

  if (res.status === 204) return null;

  let data = null;
  const text = await res.text();
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = null;
    }
  }

  if (!res.ok) {
    // The API returns { error, message, details } - see docs/ApiDesign.md
    const message = data?.message || `Request failed (${res.status})`;
    throw new ApiError(message, res.status, data?.details);
  }

  return data;
}

/**
 * Uploads use multipart/form-data, so the browser must set the Content-Type
 * itself (it has to include the boundary marker). Setting it manually breaks
 * the upload, which is why this bypasses `request`.
 */
async function upload(path, formData) {
  const headers = {};
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  let res;
  try {
    res = await fetch(`${API_URL}${path}`, { method: "POST", headers, body: formData });
  } catch {
    throw new ApiError("Could not reach the API.", 0);
  }

  const text = await res.text();
  let data = null;
  if (text) {
    try { data = JSON.parse(text); } catch { data = null; }
  }

  if (!res.ok) {
    throw new ApiError(data?.message || `Upload failed (${res.status})`, res.status, data?.details);
  }

  return data;
}

/** Downloads a file through the authorized endpoint and saves it locally. */
async function download(path, fileName) {
  const headers = {};
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${API_URL}${path}`, { headers });
  if (!res.ok) throw new ApiError(`Download failed (${res.status})`, res.status);

  // The response is the file itself, so it becomes a temporary blob URL that a
  // hidden link "clicks" to trigger the browser's save dialog.
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);

  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

/**
 * Fetches a file with the bearer token and returns a blob plus an object URL,
 * for showing it in the page instead of downloading it.
 *
 * The download endpoint sends Content-Disposition: attachment, but that only
 * applies to direct navigation - fetching the bytes ourselves and wrapping them
 * in a blob URL lets an <img> or <iframe> render it.
 *
 * Always call URL.revokeObjectURL(url) when done, or the blob stays in memory.
 */
async function fetchBlob(path) {
  const headers = {};
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${API_URL}${path}`, { headers });
  if (!res.ok) throw new ApiError(`Could not load file (${res.status})`, res.status);

  const blob = await res.blob();
  return { blob, url: URL.createObjectURL(blob) };
}

export const api = {
  get: (path) => request(path),
  post: (path, body) => request(path, { method: "POST", body }),
  put: (path, body) => request(path, { method: "PUT", body }),
  del: (path) => request(path, { method: "DELETE" }),
};

// --- feature helpers -------------------------------------------------------

export const auth = {
  register: (data) => api.post("/api/auth/register", data),
  login: (data) => api.post("/api/auth/login", data),
  me: () => api.get("/api/auth/me"),
};

export const courses = {
  list: (semester) =>
    api.get(semester ? `/api/courses?semester=${encodeURIComponent(semester)}` : "/api/courses"),
  get: (id) => api.get(`/api/courses/${id}`),
  create: (data) => api.post("/api/courses", data),
  update: (id, data) => api.put(`/api/courses/${id}`, data),
  remove: (id) => api.del(`/api/courses/${id}`),
  schedules: {
    create: (courseId, data) => api.post(`/api/courses/${courseId}/schedules`, data),
    remove: (courseId, id) => api.del(`/api/courses/${courseId}/schedules/${id}`),
  },
};

export const assignments = {
  list: (params = {}) => {
    const q = new URLSearchParams();
    if (params.courseId) q.set("courseId", params.courseId);
    if (params.status !== undefined && params.status !== "") q.set("status", params.status);
    const qs = q.toString();
    return api.get(qs ? `/api/assignments?${qs}` : "/api/assignments");
  },
  create: (data) => api.post("/api/assignments", data),
  update: (id, data) => api.put(`/api/assignments/${id}`, data),
  remove: (id) => api.del(`/api/assignments/${id}`),
};

export const folders = {
  list: (courseId, parentId) =>
    api.get(
      parentId
        ? `/api/courses/${courseId}/folders?parentId=${parentId}`
        : `/api/courses/${courseId}/folders`
    ),
  /** Every folder in the course, for the "move to folder" picker. */
  listAll: (courseId) => api.get(`/api/courses/${courseId}/folders?all=true`),
  create: (courseId, data) => api.post(`/api/courses/${courseId}/folders`, data),
  update: (courseId, id, data) => api.put(`/api/courses/${courseId}/folders/${id}`, data),
  remove: (courseId, id) => api.del(`/api/courses/${courseId}/folders/${id}`),
};

export const materials = {
  list: (courseId, folderId, search) => {
    const q = new URLSearchParams();
    if (folderId) q.set("folderId", folderId);
    if (search) q.set("search", search);
    const qs = q.toString();
    return api.get(`/api/courses/${courseId}/materials${qs ? `?${qs}` : ""}`);
  },
  upload: (courseId, file, folderId, description) => {
    const form = new FormData();
    form.append("file", file);
    // Only append folderId when there is one - sending an empty value would
    // be read as folder 0, which does not exist.
    if (folderId) form.append("folderId", folderId);
    if (description) form.append("description", description);
    return upload(`/api/courses/${courseId}/materials`, form);
  },
  download: (courseId, id, fileName) =>
    download(`/api/courses/${courseId}/materials/${id}/download`, fileName),
  preview: (courseId, id) => fetchBlob(`/api/courses/${courseId}/materials/${id}/download`),
  /** Extracted text, for file types the browser cannot render. */
  text: (courseId, id) => api.get(`/api/courses/${courseId}/materials/${id}/text`),
  move: (courseId, material, folderId) =>
    api.put(`/api/courses/${courseId}/materials/${material.id}`, {
      fileName: material.fileName,
      description: material.description ?? null,
      folderId: folderId ?? null,
    }),
  update: (courseId, id, data) => api.put(`/api/courses/${courseId}/materials/${id}`, data),
  remove: (courseId, id) => api.del(`/api/courses/${courseId}/materials/${id}`),
  usage: () => api.get("/api/storage/usage"),
};

export const notes = {
  list: (courseId, folderId, search) => {
    const q = new URLSearchParams();
    if (folderId) q.set("folderId", folderId);
    if (search) q.set("search", search);
    const qs = q.toString();
    return api.get(`/api/courses/${courseId}/notes${qs ? `?${qs}` : ""}`);
  },
  get: (courseId, id) => api.get(`/api/courses/${courseId}/notes/${id}`),
  create: (courseId, data) => api.post(`/api/courses/${courseId}/notes`, data),
  update: (courseId, id, data) => api.put(`/api/courses/${courseId}/notes/${id}`, data),
  remove: (courseId, id) => api.del(`/api/courses/${courseId}/notes/${id}`),
};

export const applications = {
  list: (params = {}) => {
    const q = new URLSearchParams();
    if (params.status !== undefined && params.status !== "") q.set("status", params.status);
    if (params.type !== undefined && params.type !== "") q.set("type", params.type);
    if (params.search) q.set("search", params.search);
    const qs = q.toString();
    return api.get(`/api/applications${qs ? `?${qs}` : ""}`);
  },
  stats: () => api.get("/api/applications/stats"),
  create: (data) => api.post("/api/applications", data),
  update: (id, data) => api.put(`/api/applications/${id}`, data),
  // Status-only update, used when dragging a card between columns.
  setStatus: (id, status) => api.put(`/api/applications/${id}/status`, status),
  remove: (id) => api.del(`/api/applications/${id}`),
};

export const jobSearch = {
  search: ({ query, location, contractTime, page = 1 }) => {
    const q = new URLSearchParams({ query, page: String(page) });
    if (location) q.set("location", location);
    if (contractTime) q.set("contractTime", contractTime);
    return api.get(`/api/jobs/search?${q.toString()}`);
  },
  saveToTracker: (data) => api.post("/api/jobs/save", data),
};
