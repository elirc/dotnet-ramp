import { useEffect, useState } from 'react';
import { api } from '../api/client.js';
import { money } from '../util/format.js';

export default function ReportsPage() {
  const [summary, setSummary] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    api.get('/api/reports/summary')
      .then(setSummary)
      .catch((err) => setError(err.message));
  }, []);

  if (error) return <div className="error">{error}</div>;
  if (!summary) return <p className="muted">Loading…</p>;

  return (
    <div>
      <h1>Reports</h1>

      <div className="cards">
        <div className="stat">
          <span className="muted">Pending approval</span>
          <strong>{summary.pendingApprovalCount}</strong>
        </div>
        <div className="stat">
          <span className="muted">Submitted</span>
          <strong>{money(summary.totalSubmitted)}</strong>
        </div>
        <div className="stat">
          <span className="muted">Approved</span>
          <strong>{money(summary.totalApproved)}</strong>
        </div>
        <div className="stat">
          <span className="muted">Reimbursed</span>
          <strong>{money(summary.totalReimbursed)}</strong>
        </div>
      </div>

      <div className="grid-2">
        <div className="card">
          <h3>By category</h3>
          <table className="table">
            <thead><tr><th>Category</th><th className="right">Count</th><th className="right">Total</th></tr></thead>
            <tbody>
              {summary.byCategory.map((c) => (
                <tr key={c.category}>
                  <td>{c.category}</td><td className="right">{c.count}</td><td className="right">{money(c.total)}</td>
                </tr>
              ))}
              {summary.byCategory.length === 0 && <tr><td colSpan={3} className="muted">No data.</td></tr>}
            </tbody>
          </table>
        </div>

        <div className="card">
          <h3>By status</h3>
          <table className="table">
            <thead><tr><th>Status</th><th className="right">Count</th><th className="right">Total</th></tr></thead>
            <tbody>
              {summary.byStatus.map((s) => (
                <tr key={s.status}>
                  <td>{s.status}</td><td className="right">{s.count}</td><td className="right">{money(s.total)}</td>
                </tr>
              ))}
              {summary.byStatus.length === 0 && <tr><td colSpan={3} className="muted">No data.</td></tr>}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
