# Incident 002 — Receipt files on disk don't match their declared content type

**Branch:** `training/incidents/002-receipt-extension-trust` (based on `main` @ `c5ef2a7`; not published to the remote, so see the status note in `training/README.md` to recreate it)
**Severity:** SEV-3 (security regression, no confirmed exploitation)
**Reported by:** Quarterly infra security review of `backend/Storage` mounts

## What the reviewer found

The security reviewer wrote a small script that walks the receipt upload directory
(`Storage:ReceiptUploadPath`, defaults to `uploads/`) and, for every file, looks up
its `Receipt` record and compares the file's on-disk extension against the
extension the application's own `ContentType`-to-extension allowlist would expect
(`image/jpeg` → `.jpg`, `image/png` → `.png`, `application/pdf` → `.pdf`).

Finding, verbatim from their report:

> "Of 40 sampled files, 11 have an on-disk extension that does not correspond to
> their declared `Content-Type`. Examples: a receipt with `Content-Type:
> image/jpeg` stored on disk as `<guid>.heic`; another stored as `<guid>` with no
> extension at all; one stored as `<guid>.jpeg.pdf`. The application's own code
> comments (`ReceiptStorage.cs`) describe an allowlist specifically meant to
> prevent this class of mismatch. Recommend re-verifying the control is still
> enforced as written."

## What engineering already ruled out

- The `AllowedTypes` content-type allowlist itself is still being checked —
  uploads with a `Content-Type` outside `image/jpeg` / `image/png` /
  `application/pdf` are still rejected with `422 Unsupported file type.`
- `OpenRead`'s path-traversal guard (`fullPath.StartsWith(...)`) is untouched and
  still passes its own defensive check.
- This is not a data-loss issue — every flagged file still opens and downloads
  fine through `GET /api/receipts/{id}/file`.

## Your task

1. Find the code path that lets the on-disk extension diverge from what the
   content-type allowlist would produce.
2. Explain, concretely, what an attacker could do with this divergence given the
   rest of the app's current design (what *is* and *isn't* actually exploitable
   here, and why).
3. Write the fix as a minimal diff that keeps the underlying user complaint (some
   legitimate uploads were being rejected) addressed.
4. Note what regression test would have caught this at review time.

No hints beyond this brief. Sealed reference analysis:
`training/_answers/002-receipt-extension-trust.md`.
