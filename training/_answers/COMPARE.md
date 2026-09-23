# COMPARE — your review vs. the sealed one

Fill this in after you've written your own findings and only then opened
`training/_answers/review.md`.

## 1. Findings you both caught

| # | Finding | Your severity | Sealed severity | Notes |
|---|---------|----------------|------------------|-------|
|   |         |                |                  |       |

## 2. Findings the sealed review has that you missed

For each, write **why** you missed it — be specific, not "I didn't look
closely enough":

- Was it in a file you didn't open in the 60 minutes?
- Was it in a file you opened but a line you skimmed past?
- Did you read the comment/docstring and take its claim at face value instead
  of checking the code beneath it? (This is the #6 and #4 pattern — comments
  claiming properties the code doesn't have. If you missed either of those,
  that's the specific miss to name.)
- Did you evaluate a function in isolation without tracing its callers/callees
  (e.g. did you look at `ReceiptStorage.SaveAsync` without also checking
  whether anything ever deletes the file it writes — finding #9)?

## 3. Findings you have that the sealed review doesn't

Not automatically wrong — the sealed review isn't exhaustive. Decide for each:
is this a real production-severity finding, or is it a style preference dressed
up as a finding? (The exercise brief specifically asks you to avoid nitpicks —
did you follow that instruction?)

## 4. The pattern behind your misses

After filling in #2, look at the list of *why* you missed things. Is there one
recurring pattern (e.g. "I trust doc comments," "I don't check for concurrency
unless the ticket mentions it," "I don't trace whether resources get cleaned
up")? Name it in one sentence. That sentence is the thing to consciously check
for on your next review, in this repo or any other.

## 5. Calibration

- Total findings: you ___ vs. sealed 18
- Findings you rated a severity *higher* than sealed did: ___
- Findings you rated a severity *lower* than sealed did: ___
- Did your top 3 by severity match the sealed top 3 (#1 approval-bypass design
  risk, #4 concurrency race, #6 timing side-channel / misleading comment)?
