import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext.jsx';

const DEMO_USERS = [
  { label: 'Employee', email: 'employee@demo.com' },
  { label: 'Manager', email: 'manager@demo.com' },
  { label: 'Finance', email: 'finance@demo.com' }
];

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('employee@demo.com');
  const [password, setPassword] = useState('Password123!');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(e) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await login(email, password);
      navigate('/expenses');
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="center">
      <form className="card login" onSubmit={handleSubmit}>
        <h1>Sign in</h1>
        <p className="muted">Expense Management System</p>

        <label>Email</label>
        <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" required />

        <label>Password</label>
        <input value={password} onChange={(e) => setPassword(e.target.value)} type="password" required />

        {error && <div className="error">{error}</div>}

        <button type="submit" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>

        <div className="demo-users">
          <span className="muted">Demo accounts (password: Password123!):</span>
          <div className="chips">
            {DEMO_USERS.map((u) => (
              <button key={u.email} type="button" className="chip" onClick={() => setEmail(u.email)}>
                {u.label}
              </button>
            ))}
          </div>
        </div>
      </form>
    </div>
  );
}
