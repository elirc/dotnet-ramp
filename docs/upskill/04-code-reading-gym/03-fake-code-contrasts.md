# Fake-Code Contrasts

Each shows a tempting-but-worse version next to what this repo actually does. All "bad"
blocks are **`// Illustrative — not from this repo`**. The point is to recognize the smell.

---

### 1. Coupling the response to the entity
```csharp
// Illustrative — not from this repo
[HttpGet("{id}")] public Expense Get(Guid id) => _store.Expenses[id]; // leaks everything
```
**Better (real):** map to a DTO — [`Mappings.ToResponse`](../../../backend/Services/Mappings.cs#L17-L42).
Returning the entity would expose internal fields and tie the wire to the DB shape.

---

### 2. Role check without resource scoping (IDOR)
```csharp
// Illustrative — not from this repo
[Authorize(Roles="Manager")]
public IActionResult Approve(Guid id) { _store.Expenses[id].Status = Approved; return Ok(); }
```
**Better (real):** [`EnsureManagesOwner` L198-202](../../../backend/Services/ExpenseService.cs#L198-L202).
The fake lets any manager approve any team's expense.

---

### 3. Mutating status without the state machine
```csharp
// Illustrative — not from this repo
expense.Status = ExpenseStatus.Reimbursed; // skips legality + history
```
**Better (real):** [`Transition` L169-178](../../../backend/Services/ExpenseService.cs#L169-L178).
The fake allows `Draft → Reimbursed` and writes no audit entry.

---

### 4. Storing the uploaded file under its original name (path traversal)
```csharp
// Illustrative — not from this repo
var path = Path.Combine(_root, file.FileName); // file.FileName could be "../../appsettings.json"
```
**Better (real):** server GUID name — [`SaveAsync` L56](../../../backend/Services/ReceiptStorage.cs#L56).

---

### 5. Swallowing exceptions
```csharp
// Illustrative — not from this repo
try { await work(...); } catch { /* ignore */ }
```
**Better (real):** [`QueuedHostedService` L45-48](../../../backend/Background/QueuedHostedService.cs#L45-L48)
logs the error (and the loop survives). Silent catch hides outages.

---

### 6. Weak password storage
```csharp
// Illustrative — not from this repo
user.PasswordHash = Convert.ToBase64String(MD5.HashData(bytes)); // unsalted, fast, broken
```
**Better (real):** PBKDF2 + salt + constant-time compare — [`PasswordHasher`](../../../backend/Security/PasswordHasher.cs).

---

### 7. Captive dependency (scoped into singleton)
```csharp
// Illustrative — not from this repo
public class QueuedHostedService { public QueuedHostedService(ExpenseService svc) {...} } // scoped in a singleton
```
**Better (real):** inject the root provider, scope per job — [`QueuedHostedService` L42-43](../../../backend/Background/QueuedHostedService.cs#L42-L43).

---

### 8. `ToList()` too early (N+1 / full scan under a real DB)
```csharp
// Illustrative — not from this repo
var all = _db.Expenses.ToList();           // pulls the whole table
return all.Where(e => e.EmployeeId == id); // filters in memory
```
**Better (real):** compose filters, materialize once — [`List` L32-51](../../../backend/Services/ExpenseService.cs#L32-L51).

---

### 9. Treating the UI capability list as security
```jsx
// Illustrative — not from this repo
if (!expense.availableActions.includes('approve')) return; // client-only "gate"
// ...and the server endpoint does NO re-check
```
**Better (real):** the list is UX ([`AvailableActions`](../../../backend/Services/Mappings.cs#L45-L72)); the server **re-checks** on the call ([`Approve`](../../../backend/Services/ExpenseService.cs#L132-L137)).

---

### 10. Effect with an unstable dependency (infinite fetch)
```jsx
// Illustrative — not from this repo
const load = async () => setExpense(await api.get(`/api/expenses/${id}`));
useEffect(() => { load(); }, [load]); // new load() each render -> loops forever
```
**Better (real):** `useCallback(load, [id])` — [`ExpenseDetailPage` L15-23](../../../frontend/src/pages/ExpenseDetailPage.jsx#L15-L23).
