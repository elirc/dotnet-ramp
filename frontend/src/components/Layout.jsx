import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext.jsx';

export default function Layout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  async function handleLogout() {
    await logout();
    navigate('/login');
  }

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">💳 Expenses</div>
        <nav className="nav">
          <NavLink to="/expenses">Expenses</NavLink>
          <NavLink to="/reports">Reports</NavLink>
        </nav>
        <div className="user">
          <span className="muted">{user.fullName}</span>
          <span className="badge role">{user.role}</span>
          <button className="link" onClick={handleLogout}>Sign out</button>
        </div>
      </header>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
