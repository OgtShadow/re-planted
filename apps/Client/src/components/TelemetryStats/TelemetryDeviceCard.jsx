import TelemetrySeriesCard, { LightSeriesCard } from './TelemetrySeriesCard';

function TelemetryDeviceCard({ chartCard, status, onSeriesClick }) {
  const device = chartCard.response;
  const deviceId = device.deviceId || device.externalDeviceId || 'unknown';

  return (
    <section className="telemetry-card">
      <div className="telemetry-device-meta">
        <div className="telemetry-device-title-row">
          <h3>{device.deviceName || device.externalDeviceId || device.deviceId || 'Urządzenie'}</h3>
          <span className={`telemetry-status-badge telemetry-status-${status.key}`}>{status.label}</span>
        </div>
        <div>
          <span>Telemetry id: {device.deviceId || 'brak'}</span>
          <span>External id: {device.externalDeviceId || 'brak'}</span>
          <span>Rośliny: {device.plantNames?.length ? device.plantNames.join(', ') : 'brak przypisania'}</span>
          <span>Próbki: {chartCard.points.length}</span>
          <span>Bucket: co {device.intervalMinutes ?? 1} min</span>
        </div>
      </div>

      {!chartCard.points.length ? (
        <p className="telemetry-empty">Brak próbek telemetrycznych dla tego urządzenia w wybranym zakresie.</p>
      ) : (
        <div className="telemetry-series-grid">
          {chartCard.numericCards.map((series) => (
            <TelemetrySeriesCard
              key={`${device.deviceId}-${series.key}`}
              series={series}
              onClick={() => onSeriesClick(deviceId, series.key)}
            />
          ))}
          {chartCard.lightCard ? (
            <LightSeriesCard
              series={chartCard.lightCard}
              onClick={() => onSeriesClick(deviceId, chartCard.lightCard.key)}
            />
          ) : null}
        </div>
      )}
    </section>
  );
}

export default TelemetryDeviceCard;
