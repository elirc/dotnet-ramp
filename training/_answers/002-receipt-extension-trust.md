# Answer — Incident 002 (receipt extension/content-type mismatch)

## Root cause

`backend/Services/ReceiptStorage.cs`, `SaveAsync`. The original code:

```csharp
if (!AllowedTypes.TryGetValue(file.ContentType, out var extension))
    throw new DomainException("Unsupported file type. Allowed: JPEG, PNG, PDF.");

var originalExt = Path.GetExtension(file.FileName);
if (!string.Equals(NormalizeExt(originalExt), extension, StringComparison.OrdinalIgnoreCase)
    && !(extension == ".jpg" && NormalizeExt(originalExt) == ".jpeg"))
{
    throw new DomainException("File extension does not match its content type.");
}

var storedName = $"{Guid.NewGuid():N}{extension}";   // <- extension from the ALLOWLIST
```

was changed to:

```csharp
if (!AllowedTypes.TryGetValue(file.ContentType, out _))
    throw new DomainException("Unsupported file type. Allowed: JPEG, PNG, PDF.");

var extension = NormalizeExt(Path.GetExtension(file.FileName));  // <- extension from the CLIENT
var storedName = $"{Guid.NewGuid():N}{extension}";
```

Two things changed together, and the second one is the actual bug:

1. The equality check between the client's extension and the content-type's
   expected extension was deleted (this was the fix for the false-reject
   complaint — legitimate).
2. The variable used to build `storedName` was switched from the allowlist's
   canonical extension (`extension` bound by `TryGetValue`) to the client's raw,
   unvalidated `Path.GetExtension(file.FileName)`. The class's own doc comment
   ("allowlist of content types + extensions ... never trust the client
   blindly") describes exactly the property this line now violates.

`Content-Type` is still validated (that's why every file still downloads with a
correct, allowlisted MIME type via `GetForActor`/`OpenFile` — the reviewer's
"not a data-loss issue" note), but the **on-disk filename's extension is now
attacker-controlled**, decoupled from the validated content type.

## What is, and isn't, exploitable here today

- **Not** path traversal: `Path.GetExtension` only returns the substring after
  the last `.` following the last path separator, so it can't contain `/` or
  `\`; `OpenRead`'s `Path.GetFullPath(...).StartsWith(_root)` guard is untouched
  and still holds.
- **Not** arbitrary file read/overwrite: the stored name is still
  `{Guid.NewGuid():N}{extension}`, so an attacker can't choose the base name or
  collide with an existing file.
- **Is** a stored-content/declared-type mismatch: an attacker can set
  `Content-Type: image/jpeg` (passes the allowlist) while naming the upload
  `payload.html`, `receipt.svg`, or similar. The bytes on disk are only checked
  against the `image/jpeg` allowlist by *header*, never by content sniffing, so
  the actual bytes can be anything. Today the file is never served statically
  and is only ever returned through `Download` with `receipt.ContentType` (the
  validated header) as the response `Content-Type`, so there's no in-app XSS
  path *right now*. The real risk is **latent**: the moment anything else in
  this codebase (a future static-file mount, a CDN pull, an antivirus/OCR
  pipeline that branches on file extension, a backup/restore script) trusts the
  on-disk extension instead of the validated content type, this divergence
  becomes directly exploitable. That's why the reviewer flagged it even without
  a confirmed exploit — it's a broken invariant, not yet a proven CVE.

## Fix

Keep the extension/content-type check removed (that part was a legitimate UX
fix), but build `storedName` from the **allowlist's** extension, not the
client's:

```csharp
if (!AllowedTypes.TryGetValue(file.ContentType, out var extension))
    throw new DomainException("Unsupported file type. Allowed: JPEG, PNG, PDF.");

var storedName = $"{Guid.NewGuid():N}{extension}";
```

This still fixes the original mobile-upload complaint (no more equality check
against the client's extension) while guaranteeing the on-disk extension always
matches the validated `Content-Type` — the invariant the class doc comment
promises.

## Regression test

A unit test on `ReceiptStorage.SaveAsync` (once a test project exists — this repo
currently has zero) asserting: for a fixed `Content-Type: image/jpeg` and a
crafted `IFormFile.FileName` of `"exploit.html"`, the resulting `Receipt
.StoredFileName` ends in `.jpg`, never `.html`. That single assertion pins the
invariant regardless of how the equality-check logic around it changes later.

## Model postmortem

- **Diagnostic path:** don't chase the reviewer's script; go straight to the
  function whose doc comment makes an explicit security claim
  ("never trust the client blindly ... no path traversal") and check whether
  every line still satisfies that claim after the most recent diff touching it.
  `git log -p -- backend/Services/ReceiptStorage.cs` would show the single
  commit that both removed the check and swapped the extension source.
- **Old-AI-generated-code smell to name explicitly:** a docstring/comment
  asserting a security property, plus a later "fix a UX complaint" commit that
  silently erodes that property without updating (or even re-reading) the
  comment above it. The comment kept saying "never trust the client blindly"
  the whole time it was doing exactly that.
- **What NOT to over-claim in the postmortem:** this is not an RCE and should
  not be reported as one; report it as a broken invariant with a described
  future blast radius, which is what actually justifies the fix priority.
