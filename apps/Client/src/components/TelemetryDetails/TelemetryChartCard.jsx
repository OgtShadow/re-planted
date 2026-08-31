import { LIGHT_SERIES, formatMinutes, formatValue } from './telemetryUtils';

function TelemetryChartCard({ response, deviceId, chartData, selectedSeriesDefinition, selectedSeriesCard, selectedSeriesPoints }) {
  return (
    <article className="telemetry-detail-chart-card">
      <div className="telemetry-chart-meta">
        <span>Urządzenie: {response?.deviceId || deviceId || 'n/a'}</span>
        <span>Próbki: {chartData.points.length}</span>
        <span>Bucket: co {response?.intervalMinutes ?? 1} min</span>
      </div>

      <div className="telemetry-detail-title-row">
        <h3>{selectedSeriesDefinition.label}</h3>
        <span className="telemetry-detail-pill">{selectedSeriesDefinition.unit === '%' ? 'Procent' : 'Wartość'}</span>
      </div>

      <svg viewBox="0 0 1000 320" className="telemetry-chart telemetry-chart-large" role="img" aria-label={`Szczegółowy wykres ${selectedSeriesDefinition.label}`}>
        <line x1="0" y1="280" x2="1000" y2="280" className="axis" />
        <line x1="0" y1="0" x2="0" y2="280" className="axis" />
        <path d={selectedSeriesCard.path} stroke={selectedSeriesDefinition.color} strokeWidth="3" fill="none" strokeLinejoin="round" strokeLinecap="round" />
        {selectedSeriesPoints.map((point, index) => (
          <circle key={`${point.label}-${index}`} cx={point.x} cy={point.y} r="4" fill={selectedSeriesDefinition.color} stroke="#fff" strokeWidth="1" />
        ))}
      </svg>

      <div className="telemetry-summary telemetry-summary-detailed">
        <div>
          <strong>Aktualnie:</strong> {formatValue(selectedSeriesCard.latest, selectedSeriesDefinition.unit)}
        </div>
        <div>
          <strong>Średnia:</strong> {formatValue(selectedSeriesCard.averageValue, selectedSeriesDefinition.unit)}
        </div>
        <div>
          <strong>Min:</strong> {formatValue(selectedSeriesCard.minValue, selectedSeriesDefinition.unit)}
        </div>
        <div>
          <strong>Max:</strong> {formatValue(selectedSeriesCard.maxValue, selectedSeriesDefinition.unit)}
        </div>
        {selectedSeriesDefinition.key === LIGHT_SERIES.key ? (
          <>
            <div>
              <strong>ON:</strong> {formatMinutes(chartData.lightCard.onMinutes)}
            </div>
            <div>
              <strong>OFF:</strong> {formatMinutes(chartData.lightCard.offMinutes)}
            </div>
          </>
        ) : null}
      </div>
    </article>
  );
}

export default TelemetryChartCard;
