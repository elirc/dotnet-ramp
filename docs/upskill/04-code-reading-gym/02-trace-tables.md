# Trace Tables

Fill in the blanks (cover the right column first). Each trains following data and ownership
across layers.

---

## Trace 1 — Submit with a blocking policy flag (error trace)

Scenario: employee submits a $30 expense with no receipt.

| Step | File/line | Value shape | Owner | Transformation | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | [`Submit` L117](../../../backend/Services/ExpenseService.cs#L117) | `Expense{Amount:30,ReceiptId:null}` | service | RefreshPolicy | — |
| 2 | [`PolicyEngine` L50-52](../../../backend/Services/PolicyEngine.cs#L50-L52) | flag `RECEIPT_REQUIRED/Blocking` | policy | rule fires | — |
| 3 | [`Submit` L119-125](../../../backend/Services/ExpenseService.cs#L119-L125) | `DomainException(details=[…])` | service | throw | — |
| 4 | [middleware L41,57-58](../../../backend/Middleware/ExceptionHandlingMiddleware.cs#L41) | `422 ProblemDetails{detail,details}` | middleware | serialize | — |
| 5 | [`client.js` L40-42](../../../frontend/src/api/client.js#L40-L42) | `ApiError{status:422,details}` | client | throw | — |
| 6 | [`ExpenseFormPage`](../../../frontend/src/pages/ExpenseFormPage.jsx) | rendered error list | UI | setState | — |

---

## Trace 2 — Login → authorized request (auth/permission trace)

| Step | File/line | Value | Owner | Transformation |
| --- | --- | --- | --- | --- |
| 1 | [`AuthService.Login` L32-34](../../../backend/Security/AuthService.cs#L32-L34) | token + Session | service | issue |
| 2 | [`client.js` L22-23](../../../frontend/src/api/client.js#L22-L23) | `Authorization: Bearer …` | client | attach |
| 3 | [handler L39-40](../../../backend/Security/TokenAuthenticationHandler.cs#L39-L40) | `User` | handler | validate |
| 4 | [handler L44-55](../../../backend/Security/TokenAuthenticationHandler.cs#L44-L55) | `ClaimsPrincipal{role}` | handler | build |
| 5 | [`UserContext` L30-31](../../../backend/Security/UserContext.cs#L30-L31) | `Guid?` | context | parse claim |
| 6 | [`UserContext` L38-39](../../../backend/Security/UserContext.cs#L38-L39) | `User` | context | resolve entity |

---

## Trace 3 — Receipt upload → processed (UI → API → worker → UI)

| Step | File/line | Value | Owner |
| --- | --- | --- | --- |
| 1 | [`handleUpload` L58-60](../../../frontend/src/pages/ExpenseDetailPage.jsx#L58-L60) | FormData | UI |
| 2 | [`SaveAsync` L56-62](../../../backend/Services/ReceiptStorage.cs#L56-L62) | file on disk + Receipt(Pending) | storage |
| 3 | [`UploadAsync` L52-58](../../../backend/Services/ReceiptService.cs#L52-L58) | enqueued delegate | service |
| 4 | [`ProcessReceipt` L29-30](../../../backend/Background/BackgroundJobs.cs#L29-L30) | Receipt(Processed, ExtractedAmount) | worker |
| 5 | [poll L26-32](../../../frontend/src/pages/ExpenseDetailPage.jsx#L26-L32) | refreshed expense | UI |

---

## Trace 4 — Reimbursement (system transition)

| Step | File/line | Status before → after | Owner |
| --- | --- | --- | --- |
| 1 | [`StartReimbursement` L153](../../../backend/Services/ExpenseService.cs#L153) | Approved → Reimbursing | service (Finance) |
| 2 | [`StartReimbursement` L154](../../../backend/Services/ExpenseService.cs#L154) | (enqueue job) | service |
| 3 | [`ProcessReimbursement` L51](../../../backend/Background/BackgroundJobs.cs#L51) | guard: CanSystemTransition | worker |
| 4 | [`ProcessReimbursement` L58-66](../../../backend/Background/BackgroundJobs.cs#L58-L66) | Reimbursing → Reimbursed + ref | worker (system) |

---

## Trace 5 — List visibility for a Manager (persistence read)

| Step | File/line | Effect |
| --- | --- | --- |
| 1 | [`List` L34-44](../../../backend/Services/ExpenseService.cs#L34-L44) | scope to own + direct reports |
| 2 | [`IsDirectReport` L204-205](../../../backend/Services/ExpenseService.cs#L204-L205) | `owner.ManagerId == actor.Id` |
| 3 | [`List` L46-49](../../../backend/Services/ExpenseService.cs#L46-L49) | apply optional filters |
| 4 | [`List` L51](../../../backend/Services/ExpenseService.cs#L51) | order + materialize |

---

## Trace 6 — A receipt download authorization (resource auth)

| Step | File/line | Check |
| --- | --- | --- |
| 1 | [`Download` L37-42](../../../backend/Controllers/ReceiptsController.cs#L37-L42) | `[Authorize]` + resolve actor |
| 2 | [`ReceiptService.GetForActor` L63-70](../../../backend/Services/ReceiptService.cs#L63-L70) | reuse expense visibility via `GetForActor` |
| 3 | [`ReceiptStorage.OpenRead` L75-82](../../../backend/Services/ReceiptStorage.cs#L75-L82) | path-traversal guard, open file |

**Drill:** Trace 6 reuses expense visibility to authorize a *file* download. Why is that
better than a separate permission check on receipts? (One source of truth; can't drift.)
