import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api, tokenStore } from '../api/client.js';
import StatusBadge from '../components/StatusBadge.jsx';
import { money, date, dateTime } from '../util/format.js';

export default function ExpenseDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [expense, setExpense] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const fileInput = useRef(null);

  const load = useCallback(async () => {
    try {
      setExpense(await api.get(`/api/expenses/${id}`));
    } catch (err) {
      setError(err.message);
    }
  }, [id]);

  useEffect(() => { load(); }, [load]);

  // Receipt scan/OCR and reimbursement payment run in the background; poll while pending.
  useEffect(() => {
    const pending = expense?.receipt?.status === 'Pending' || expense?.status === 'Reimbursing';
    if (pending) {
      const t = setTimeout(load, 1500);
      return () => clearTimeout(t);
    }
  }, [expense, load]);

  async function act(fn) {
    setError(null);
    setBusy(true);
    try {
      await fn();
      await load();
    } catch (err) {
      setError(err.message + (err.details?.length ? ` — ${err.details.join(', ')}` : ''));
    } finally {
      setBusy(false);
    }
  }

  const can = (a) => expense?.availableActions?.includes(a);

  async function handleReject() {
    const reason = window.prompt('Reason for rejection:');
    if (!reason) return;
    await act(() => api.post(`/api/expenses/${id}/reject`, { reason }));
  }

  async function handleUpload(e) {
    const file = e.target.files?.[0];
    if (!file) return;
    const fd = new FormData();
    fd.append('file', file);
    await act(() => api.upload(`/api/expenses/${id}/receipt`, fd));
    if (fileInput.current) fileInput.current.value = '';
  }

  function downloadReceipt() {
    // Token-protected endpoint: fetch as a blob, then trigger a download.
    fetch(`/api/receipts/${expense.receipt.id}/file`, {
      headers: { Authorization: `Bearer ${tokenStore.get()}` }
    })
      .then((r) => r.blob())
      .then((blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = expense.receipt.originalFileName;
        a.click();
        URL.revokeObjectURL(url);
      });
  }

  if (error && !expense) return <div className="error">{error}</div>;
  if (!expense) return <p className="muted">Loading…</p>;

  return (
    <div className="narrow">
      <div className="page-head">
        <h1>{expense.merchant}</h1>
        <StatusBadge status={expense.status} />
      </div>

      {error && <div className="error">{error}</div>}

      <div className="card">
        <dl className="details">
          <dt>Amount</dt><dd>{money(expense.amount, expense.currency)}</dd>
          <dt>Category</dt><dd>{expense.category}</dd>
          <dt>Date</dt><dd>{date(expense.expenseDate)}</dd>
          <dt>Employee</dt><dd>{expense.employeeName}</dd>
          <dt>Description</dt><dd>{expense.description}</dd>
          {expense.rejectionReason && (<><dt>Rejection</dt><dd className="danger-text">{expense.rejectionReason}</dd></>)}
          {expense.paymentReference && (<><dt>Payment ref</dt><dd>{expense.paymentReference}</dd></>)}
        </dl>
      </div>

      {/* Policy flags */}
      {expense.policyFlags.length > 0 && (
        <div className="card">
          <h3>Policy checks</h3>
          <ul className="flags">
            {expense.policyFlags.map((f) => (
              <li key={f.code} className={f.severity === 'Blocking' ? 'flag blocking' : 'flag warning'}>
                <strong>{f.severity}</strong> · {f.message}
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Receipt */}
      <div className="card">
        <h3>Receipt</h3>
        {expense.receipt ? (
          <div className="receipt">
            <span>{expense.receipt.originalFileName}</span>
            <span className="badge">{expense.receipt.status}</span>
            {expense.receipt.extractedAmount != null && (
              <span className="muted small">OCR: {money(expense.receipt.extractedAmount)}</span>
            )}
            <button className="link" onClick={downloadReceipt}>Download</button>
          </div>
        ) : (
          <p className="muted">No receipt attached.</p>
        )}
        {can('uploadReceipt') && (
          <div className="upload">
            <input ref={fileInput} type="file" accept="image/jpeg,image/png,application/pdf" onChange={handleUpload} />
            <span className="muted small">JPEG, PNG or PDF, max 5 MB.</span>
          </div>
        )}
      </div>

      {/* Actions */}
      <div className="action-bar">
        {can('edit') && <Link className="btn ghost" to={`/expenses/${id}/edit`}>Edit</Link>}
        {can('submit') && <button disabled={busy} onClick={() => act(() => api.post(`/api/expenses/${id}/submit`))}>Submit</button>}
        {can('approve') && <button disabled={busy} className="btn success" onClick={() => act(() => api.post(`/api/expenses/${id}/approve`))}>Approve</button>}
        {can('reject') && <button disabled={busy} className="btn danger" onClick={handleReject}>Reject</button>}
        {can('reimburse') && <button disabled={busy} onClick={() => act(() => api.post(`/api/expenses/${id}/reimburse`))}>Reimburse</button>}
        {can('delete') && (
          <button disabled={busy} className="btn danger ghost"
            onClick={() => act(async () => { await api.del(`/api/expenses/${id}`); navigate('/expenses'); })}>
            Delete
          </button>
        )}
      </div>

      {/* History / audit trail */}
      <div className="card">
        <h3>History</h3>
        {expense.history.length === 0 ? (
          <p className="muted">No transitions yet.</p>
        ) : (
          <ul className="timeline">
            {expense.history.map((h, i) => (
              <li key={i}>
                <span className="badge">{h.from} → {h.to}</span>
                <span> by {h.actorName}</span>
                {h.note && <span className="muted"> — {h.note}</span>}
                <div className="muted small">{dateTime(h.at)}</div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
