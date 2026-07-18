# Expense Management System

A learning-oriented clone of Ramp / Expensify / Brex expense workflows, built with
**ASP.NET Core (C#)** and **React**. It demonstrates file uploads, validation,
background processing, a state machine, security, and reporting — with a deliberately
small dependency footprint.

> **Status:** the source is complete but has not been compiled/run in this environment
> (the .NET SDK isn't installed here). Install the .NET 8 SDK to build and run.

---

## What it does

- **Employees** create expenses, attach receipts, and submit them for approval.
- **Managers** approve or reject their direct reports' expenses.
- **Finance** reimburses approved expenses (payment completes in the background).
- **Policy checks** flag or block expenses (receipt required over $25, category limits,
  future dates, high-value spend).
- **Reports** summarise spend by status and category, scoped to your role.

### Expense state machine

```
Draft ──submit──▶ Submitted ──approve──▶ Approved ──reimburse──▶ Reimbursing ──(system)──▶ Reimbursed
  ▲                   │
  │                   └──reject──▶ Rejected ──submit──▶ Submitted
  └──────── edit (while Draft or Rejected)
```

All transitions go through a single `ExpenseStateMachine` that enforces both the legal
edges and which **role** may trigger each one. Every change is recorded in an audit trail.

---

## Architecture

```
backend/                 ASP.NET Core Web API (.NET 8) — ZERO NuGet packages
  Domain/                Entities, enums, the state machine, domain exceptions
  Storage/               In-memory DataStore (singleton) + demo seeding
  Security/              PBKDF2 hashing, token sessions, custom auth handler
  Services/              PolicyEngine, ExpenseService, ReceiptService, Reporting, mappings
  Background/            Channel-based task queue + hosted worker + jobs
  Middleware/            Exception → ProblemDetails mapping
  Controllers/           Auth, Expenses, Receipts, Reports
  Program.cs             Composition root

frontend/                React + Vite
  src/api/               fetch wrapper with bearer token
  src/auth/              auth context (login/logout/me)
  src/pages/             Login, Expenses, Expense form, Expense detail, Reports
  src/components/        Layout, status badge
```

### Notable design choices

- **No NuGet packages** on the backend — everything (auth handler, PBKDF2 hashing,
  background `Channel`, JSON) ships with the .NET SDK. Swapping the in-memory
  `DataStore` for EF Core later only touches the storage layer.
- **Authorization in the service layer**, not controllers: employees see their own
  expenses, managers see their team, finance sees everything — enforced in one place.
- **State changes funnel through the state machine**, so illegal transitions are
  impossible regardless of entry point.
- **Background processing** for receipt scan/OCR and reimbursement payment, using a
  bounded producer/consumer queue drained by a `BackgroundService`.
- **Upload safety**: content-type/extension allowlist, size cap, server-generated file
  names, files stored outside web root and served only through an authorized endpoint.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org) (already present in this environment)

## Running

**1. Backend** (from `backend/`):

```bash
dotnet run
```

API listens on `http://localhost:5085`.

**2. Frontend** (from `frontend/`):

```bash
npm install
npm run dev
```

App runs on `http://localhost:5173` and proxies `/api` to the backend.

## Demo accounts

All use the password **`Password123!`**:

| Role     | Email               | Can do                                   |
|----------|---------------------|------------------------------------------|
| Employee | employee@demo.com   | Create, submit, attach receipts          |
| Employee | dana@demo.com       | (second report of the same manager)      |
| Manager  | manager@demo.com    | Approve / reject the two employees       |
| Finance  | finance@demo.com    | Reimburse approved expenses              |

### Suggested walkthrough

1. Sign in as **employee@demo.com**, open the draft expense, attach a receipt
   (watch its status flip from *Pending* to *Processed*), then **Submit**.
2. Sign in as **manager@demo.com**, open the submitted expense, **Approve** it.
3. Sign in as **finance@demo.com**, **Reimburse** it, then refresh after a few
   seconds to see it become *Reimbursed* with a payment reference.
4. Check **Reports** as each role to see how visibility is scoped.

---

## API reference

| Method | Route                              | Role        | Purpose                       |
|--------|------------------------------------|-------------|-------------------------------|
| POST   | `/api/auth/login`                  | anon        | Get a bearer token            |
| POST   | `/api/auth/logout`                 | any         | Invalidate the session        |
| GET    | `/api/auth/me`                     | any         | Current user                  |
| GET    | `/api/expenses`                    | any         | List (role-scoped, filterable)|
| POST   | `/api/expenses`                    | any         | Create draft                  |
| GET    | `/api/expenses/{id}`               | any         | Detail                        |
| PUT    | `/api/expenses/{id}`               | owner       | Edit (Draft/Rejected only)    |
| DELETE | `/api/expenses/{id}`               | owner       | Delete (Draft only)           |
| POST   | `/api/expenses/{id}/submit`        | owner       | Submit for approval           |
| POST   | `/api/expenses/{id}/approve`       | Manager     | Approve                       |
| POST   | `/api/expenses/{id}/reject`        | Manager     | Reject (with reason)          |
| POST   | `/api/expenses/{id}/reimburse`     | Finance     | Queue reimbursement           |
| POST   | `/api/expenses/{id}/receipt`       | owner       | Upload receipt (multipart)    |
| GET    | `/api/receipts/{id}/file`          | authorized  | Download receipt              |
| GET    | `/api/reports/summary`             | any         | Spend summary (role-scoped)   |

---

## Notes & possible next steps

- Data is **in-memory** and resets on restart (by design, for learning).
- Natural extensions: EF Core + a real DB, JWT instead of in-memory sessions,
  unit tests for `ExpenseStateMachine` / `PolicyEngine`, multi-level approvals,
  currency conversion, and real OCR for receipts.
