import { formatMinutes, formatValue } from './telemetryStatsUtils';

function Chart({ path, color, label }) {
  return (
    <svg viewBox="0 0 1000 320" className="telemetry-chart" role="img" aria-label={`Wykres serii ${label}`}>
      <line x1="0" y1="280" x2="1000" y2="280" className="axis" />
      <line x1="0" y1="0" x2="0" y2="280" className="axis" />
      <path d={path} stroke={color} strokeWidth="3" fill="none" strokeLinejoin="round" strokeLinecap="round" />
    </svg>
  );
}

function TelemetrySeriesCard({ series, onClick }) {
  return (
    <article className="telemetry-series-item" style={{ '--series-color': series.color }} onClick={onClick}>
      <h3>{series.label}</h3>
      <Chart path={series.path} color={series.color} label={series.label} />
      <div className="telemetry-summary">
        <div><strong>Aktualnie:</strong> {formatValue(series.latest, series.unit)}</div>
        <div><strong>Średnia:</strong> {formatValue(series.averageValue, series.unit)}</div>
        <div><strong>Min:</strong> {formatValue(series.minValue, series.unit)}</div>
        <div><strong>Max:</strong> {formatValue(series.maxValue, series.unit)}</div>
      </div>
    </article>
  );
}

export function LightSeriesCard({ series, onClick }) {
  return (
    <article className="telemetry-series-item telemetry-light-item" onClick={onClick}>
      <h3>Światło (ON/OFF)</h3>
      <Chart path={series.path} color={series.color} label="udziału czasu światła ON" />
      <div className="telemetry-summary">
        <div><strong>ON:</strong> {formatMinutes(series.onMinutes)}</div>
        <div><strong>OFF:</strong> {formatMinutes(series.offMinutes)}</div>
        <div><strong>Udział ON:</strong> {formatValue(series.onShare, '%')}</div>
        <div><strong>Średnia ON:</strong> {formatValue(series.averagePercent, '%')}</div>
      </div>
    </article>
  );
}

export default TelemetrySeriesCard;
