import { formatTimestamp, formatValue } from './telemetryUtils';

function TelemetryPointsList({ points, selectedSeriesDefinition }) {
  return (
    <article className="telemetry-series-item telemetry-side-card">
      <h3>Dokładne wartości punktów</h3>
      <div className="telemetry-points-list">
        {points.map((point, index) => (
          <div key={`${point.bucketStartUtc}-${index}`} className="telemetry-point-row">
            <span>{formatTimestamp(point.bucketStartUtc)}</span>
            <strong>{formatValue(Number(point[selectedSeriesDefinition.key] ?? 0), selectedSeriesDefinition.unit)}</strong>
          </div>
        ))}
      </div>
    </article>
  );
}

export default TelemetryPointsList;
