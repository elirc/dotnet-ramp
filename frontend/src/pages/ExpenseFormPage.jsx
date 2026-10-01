import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../api/client.js';
import { CATEGORIES } from '../util/format.js';

const today = () => new Date().toISOString().slice(0, 10);

const EMPTY = {
  merchant: '',
  description: '',
  amount: '',
  currency: 'USD',
  category: 'Meals',
  expenseDate: today()
};

export default function ExpenseFormPage() {
  const { id } = useParams();
  const isEdit = Boolean(id);
  const navigate = useNavigate();

  const [form, setForm] = useState(EMPTY);
  const [error, setError] = useState(null);
  const [details, setDetails] = useState([]);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!isEdit) return;
    api.get(`/api/expenses/${id}`)
      .then((e) => setForm({
        merchant: e.merchant,
        description: e.description,
        amount: e.amount,
        currency: e.currency,
        category: e.category,
        expenseDate: e.expenseDate
      }))
      .catch((err) => setError(err.message));
  }, [id, isEdit]);

  function update(field, value) {
    setForm((f) => ({ ...f, [field]: value }));
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setError(null);
    setDetails([]);
    setBusy(true);
    const payload = { ...form, amount: parseFloat(form.amount) };
    try {
      const saved = isEdit
        ? await api.put(`/api/expenses/${id}`, payload)
        : await api.post('/api/expenses', payload);
      navigate(`/expenses/${saved.id}`);
    } catch (err) {
      setError(err.message);
      setDetails(err.details || []);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="narrow">
      <h1>{isEdit ? 'Edit expense' : 'New expense'}</h1>
      <form className="card" onSubmit={handleSubmit}>
        <label>Merchant</label>
        <input value={form.merchant} onChange={(e) => update('merchant', e.target.value)} required maxLength={200} />

        <label>Description</label>
        <textarea value={form.description} onChange={(e) => update('description', e.target.value)} required maxLength={1000} />

        <div className="row">
          <div className="col">
            <label>Amount</label>
            <input type="number" step="0.01" min="0.01" value={form.amount}
              onChange={(e) => update('amount', e.target.value)} required />
          </div>
          <div className="col">
            <label>Currency</label>
            <input value={form.currency} onChange={(e) => update('currency', e.target.value.toUpperCase())}
              maxLength={3} minLength={3} required />
          </div>
        </div>

        <div className="row">
          <div className="col">
            <label>Category</label>
            <select value={form.category} onChange={(e) => update('category', e.target.value)}>
              {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </div>
          <div className="col">
            <label>Date</label>
            <input type="date" value={form.expenseDate} onChange={(e) => update('expenseDate', e.target.value)} required />
          </div>
        </div>

        {error && (
          <div className="error">
            {error}
            {details.length > 0 && <ul>{details.map((d, i) => <li key={i}>{d}</li>)}</ul>}
          </div>
        )}

        <div className="actions">
          <button type="button" className="btn ghost" onClick={() => navigate(-1)}>Cancel</button>
          <button type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save'}</button>
        </div>
      </form>
      <p className="muted small">Tip: after saving, open the expense to attach a receipt and submit it.</p>
    </div>
  );
}
