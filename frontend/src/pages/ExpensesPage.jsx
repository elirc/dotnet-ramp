import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client.js';
import StatusBadge from '../components/StatusBadge.jsx';
import { money, date, STATUSES, CATEGORIES } from '../util/format.js';

export default function ExpensesPage() {
  const [expenses, setExpenses] = useState([]);
  const [status, setStatus] = useState('');
  const [category, setCategory] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    const params = new URLSearchParams();
    if (status) params.set('status', status);
    if (category) params.set('category', category);

    api.get(`/api/expenses?${params.toString()}`)
      .then((data) => { if (active) setExpenses(data); })
      .catch((err) => { if (active) setError(err.message); })
      .finally(() => { if (active) setLoading(false); });

    return () => { active = false; };
  }, [status, category]);

  return (
    <div>
      <div className="page-head">
        <h1>Expenses</h1>
        <Link className="btn" to="/expenses/new">+ New expense</Link>
      </div>

      <div className="filters">
        <select value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">All statuses</option>
          {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select value={category} onChange={(e) => setCategory(e.target.value)}>
          <option value="">All categories</option>
          {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
      </div>

      {error && <div className="error">{error}</div>}
      {loading ? (
        <p className="muted">Loading…</p>
      ) : expenses.length === 0 ? (
        <p className="muted">No expenses found.</p>
      ) : (
        <table className="table">
          <thead>
            <tr>
              <th>Date</th><th>Merchant</th><th>Employee</th>
              <th>Category</th><th className="right">Amount</th><th>Status</th>
            </tr>
          </thead>
          <tbody>
            {expenses.map((e) => (
              <tr key={e.id}>
                <td>{date(e.expenseDate)}</td>
                <td><Link to={`/expenses/${e.id}`}>{e.merchant}</Link></td>
                <td>{e.employeeName}</td>
                <td>{e.category}</td>
                <td className="right">{money(e.amount, e.currency)}</td>
                <td><StatusBadge status={e.status} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
