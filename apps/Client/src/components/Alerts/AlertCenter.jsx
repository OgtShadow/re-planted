import { Link } from 'react-router-dom';
import { useAlerts } from './AlertContext';
import './Alerts.css';

const severityLabel = { Critical: 'Krytyczny', Warning: 'Ostrzeżenie', Info: 'Informacja' };

export function AlertCenter() {
  const { alerts, toast, dismissToast } = useAlerts();
  const activeCount = alerts.filter((alert) => !alert.acknowledgedAtUtc).length;

  return (
    <>
      <Link className="alert-link" to="/alerts" title="Centrum alertów" aria-label="Centrum alertów">
        <span aria-hidden="true">!</span>
        {activeCount > 0 && <strong>{activeCount > 99 ? '99+' : activeCount}</strong>}
      </Link>
      {toast && (
        <aside className={`alert-toast alert-${toast.severity?.toLowerCase() || 'warning'}`} role="status">
          <button type="button" className="alert-toast-close" onClick={dismissToast} aria-label="Zamknij">×</button>
          <span className="alert-toast-severity">{severityLabel[toast.severity] || 'Alert'}</span>
          <b>{toast.title}</b>
          <p>{toast.message}</p>
          <Link to="/alerts" onClick={dismissToast}>Zobacz centrum alertów</Link>
        </aside>
      )}
    </>
  );
}

export function AlertHistory() {
  const { alerts, acknowledge } = useAlerts();
  const active = alerts.filter((alert) => !alert.acknowledgedAtUtc);
  const acknowledged = alerts.filter((alert) => alert.acknowledgedAtUtc);

  return (
    <main className="alerts-page">
      <header className="alerts-page-header">
        <div><span className="eyebrow">Monitoring</span><h1>Centrum alertów</h1></div>
        <span className="alerts-count">{active.length} aktywnych</span>
      </header>
      <section className="alerts-section">
        <h2>Wymagają uwagi</h2>
        {active.length === 0 ? <p className="alerts-empty">Brak aktywnych alertów.</p> : active.map((alert) => <AlertRow key={alert.id} alert={alert} onAcknowledge={acknowledge} />)}
      </section>
      <section className="alerts-section alerts-history">
        <h2>Historia</h2>
        {acknowledged.length === 0 ? <p className="alerts-empty">Historia jest pusta.</p> : acknowledged.map((alert) => <AlertRow key={alert.id} alert={alert} />)}
      </section>
    </main>
  );
}

function AlertRow({ alert, onAcknowledge }) {
  return (
    <article className={`alert-row alert-${alert.severity?.toLowerCase() || 'warning'}`}>
      <div className="alert-row-mark" aria-hidden="true">!</div>
      <div className="alert-row-body"><div className="alert-row-meta"><span>{severityLabel[alert.severity] || 'Alert'}</span><time>{new Date(alert.createdAtUtc).toLocaleString('pl-PL')}</time></div><h3>{alert.title}</h3><p>{alert.message}</p></div>
      {onAcknowledge && <button type="button" className="alert-acknowledge" onClick={() => onAcknowledge(alert.id)}>Potwierdź</button>}
    </article>
  );
}
