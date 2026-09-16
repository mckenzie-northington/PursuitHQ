'use client';

import { useCallback, useEffect, useState } from "react";
import QRCode from "qrcode";
import { auth as authApi } from "@/lib/api";

/**
 * Two-step verification, start to finish.
 *
 * Four states rather than a wizard: off, setting up, showing the recovery codes
 * once, and on. Recovery codes get their own state because they are the one
 * screen in the app that cannot be returned to - Identity stores only hashes of
 * them, so once this unmounts they are gone for good.
 */
export default function TwoFactorSection({ card, label, field }) {
  const [status, setStatus] = useState(null);
  const [setup, setSetup] = useState(null);
  const [qr, setQr] = useState("");
  const [codes, setCodes] = useState(null);

  const [code, setCode] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  const refresh = useCallback(async () => {
    try {
      setStatus(await authApi.twoFactor.status());
    } catch (err) {
      setError(err.message);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  // Drawn in the browser. The URI contains the shared secret, so sending it to
  // an image service to be rendered would be handing out the second factor.
  useEffect(() => {
    if (!setup?.authenticatorUri) {
      setQr("");
      return;
    }

    let live = true;

    QRCode.toDataURL(setup.authenticatorUri, { margin: 1, width: 200 })
      .then((url) => live && setQr(url))
      .catch(() => live && setQr(""));

    return () => {
      live = false;
    };
  }, [setup]);

  function reset() {
    setError("");
    setMessage("");
  }

  async function begin() {
    reset();
    setBusy(true);

    try {
      setSetup(await authApi.twoFactor.setUp());
      setCode("");
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function enable(e) {
    e.preventDefault();
    reset();
    setBusy(true);

    try {
      const result = await authApi.twoFactor.enable(code);
      setCodes(result.codes);
      setSetup(null);
      setCode("");
      await refresh();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function disable(e) {
    e.preventDefault();
    reset();
    setBusy(true);

    try {
      await authApi.twoFactor.disable(password);
      setPassword("");
      setMessage("Two-step verification is off.");
      await refresh();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function newCodes(e) {
    e.preventDefault();
    reset();
    setBusy(true);

    try {
      const result = await authApi.twoFactor.newRecoveryCodes(password);
      setPassword("");
      setCodes(result.codes);
      await refresh();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  const button =
    "rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50";
  const quiet =
    "rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50";

  return (
    <section className={`mt-6 ${card}`}>
      <h2 className="font-medium text-slate-900">Two-step verification</h2>
      <p className="mt-1 text-sm text-slate-600">
        A code from your phone on top of your password, so a stolen password is not
        enough on its own.
      </p>

      {error && (
        <p className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </p>
      )}

      {message && (
        <p className="mt-4 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
          {message}
        </p>
      )}

      {codes ? (
        <RecoveryCodes codes={codes} onDone={() => setCodes(null)} button={button} />
      ) : setup ? (
        <form onSubmit={enable} className="mt-4 space-y-4">
          <p className="text-sm text-slate-700">
            Scan this with Google Authenticator, Authy, 1Password — any authenticator
            app — then enter the code it shows.
          </p>

          {qr ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={qr}
              alt="QR code for setting up two-step verification"
              className="rounded-md border border-slate-200 bg-white p-2"
              width={200}
              height={200}
            />
          ) : (
            <p className="text-sm text-slate-500">Preparing the QR code...</p>
          )}

          <div>
            <p className="text-xs text-slate-500">
              Can&apos;t scan it? Type this key into the app instead:
            </p>
            <code className="mt-1 block rounded-md bg-slate-100 px-3 py-2 font-mono text-sm tracking-wider text-slate-800">
              {setup.sharedKey}
            </code>
          </div>

          <div className="sm:max-w-xs">
            <label className={label}>Code from the app</label>
            <input
              type="text"
              required
              inputMode="numeric"
              autoComplete="one-time-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              className={`${field} text-center tracking-[0.3em]`}
              placeholder="000000"
            />
          </div>

          <div className="flex flex-wrap gap-2">
            <button type="submit" disabled={busy} className={button}>
              {busy ? "Checking..." : "Turn on"}
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={() => {
                setSetup(null);
                reset();
              }}
              className={quiet}
            >
              Cancel
            </button>
          </div>
        </form>
      ) : status?.enabled ? (
        <div className="mt-4 space-y-4">
          <p className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
            On. You will be asked for a code when you sign in.
          </p>

          <p className="text-sm text-slate-600">
            {status.recoveryCodesLeft} recovery{" "}
            {status.recoveryCodesLeft === 1 ? "code" : "codes"} left.
            {status.recoveryCodesLeft <= 3 && " Worth generating a new set."}
          </p>

          <div>
            <label className={label}>Your password</label>
            <input
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className={`${field} sm:max-w-xs`}
              placeholder="Required for either action"
            />
          </div>

          <div className="flex flex-wrap gap-2">
            <button onClick={newCodes} disabled={busy || !password} className={quiet}>
              New recovery codes
            </button>
            <button
              onClick={disable}
              disabled={busy || !password}
              className="rounded-md border border-red-300 px-4 py-2 text-sm font-medium text-red-700 transition hover:bg-red-50 disabled:opacity-50"
            >
              Turn off
            </button>
          </div>
        </div>
      ) : (
        <div className="mt-4">
          <button onClick={begin} disabled={busy || !status} className={button}>
            {busy ? "Starting..." : "Set up"}
          </button>
        </div>
      )}
    </section>
  );
}

/**
 * The codes, shown once.
 *
 * Confirmation is required before this closes, because the alternative is
 * someone clicking past it and finding out months later, with a lost phone,
 * that there was no way back into their own account.
 */
function RecoveryCodes({ codes, onDone, button }) {
  const [saved, setSaved] = useState(false);

  function copy() {
    navigator.clipboard?.writeText(codes.join("\n")).catch(() => {});
  }

  return (
    <div className="mt-4 space-y-4">
      <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2">
        <p className="text-sm font-medium text-amber-800">
          Save these somewhere safe now.
        </p>
        <p className="mt-1 text-xs text-amber-700">
          Only hashes of these are stored, so this is the one and only time they can be
          shown. Each works once, and they are the way back in if you lose your phone.
        </p>
      </div>

      <ul className="grid grid-cols-2 gap-x-6 gap-y-1 rounded-md bg-slate-100 p-4 font-mono text-sm text-slate-800">
        {codes.map((recoveryCode) => (
          <li key={recoveryCode}>{recoveryCode}</li>
        ))}
      </ul>

      <label className="flex items-start gap-2 text-sm text-slate-700">
        <input
          type="checkbox"
          checked={saved}
          onChange={(e) => setSaved(e.target.checked)}
          className="mt-0.5"
        />
        I have saved these somewhere I can find them.
      </label>

      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          onClick={copy}
          className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
        >
          Copy
        </button>
        <button type="button" onClick={onDone} disabled={!saved} className={button}>
          Done
        </button>
      </div>
    </div>
  );
}
