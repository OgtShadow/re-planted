import { LIGHT_SERIES, formatValue } from './telemetryUtils';

function TelemetrySeriesComparison({ chartData, selectedSeriesDefinition, onSeriesSelect }) {
  return (
    <article className="telemetry-series-item telemetry-side-card">
      <h3>Porównanie serii</h3>
      <div className="telemetry-compare-list">
        {chartData.numericCards.map((series) => (
          <div
            key={series.key}
            className={`telemetry-compare-row ${selectedSeriesDefinition.key === series.key ? 'active' : ''}`}
            onClick={() => onSeriesSelect(series.key)}
            style={{ cursor: 'pointer' }}
          >
            <span style={{ color: series.color }}>■</span>
            <span>{series.label}</span>
            <strong>{formatValue(series.latest, series.unit)}</strong>
          </div>
        ))}
        <div
          className={`telemetry-compare-row ${selectedSeriesDefinition.key === LIGHT_SERIES.key ? 'active' : ''}`}
          onClick={() => onSeriesSelect(LIGHT_SERIES.key)}
          style={{ cursor: 'pointer' }}
        >
          <span style={{ color: LIGHT_SERIES.color }}>■</span>
          <span>{LIGHT_SERIES.label}</span>
          <strong>{formatValue(chartData.lightCard.latest, LIGHT_SERIES.unit)}</strong>
        </div>
      </div>
    </article>
  );
}

export default TelemetrySeriesComparison;
