function LiveTelemetry({ rows }) {
  if (!rows.length) return null;

  return (
    <div className="telemetry-card">
      <strong>Live stream czujników</strong>
      {rows.map((entry) => (
        <div key={`${entry.snapshotDeviceId}-${entry.snapshot.timestamp || entry.snapshot.Timestamp}`} className="telemetry-live-row">
          <span>{entry.snapshotDeviceId}</span>
          <span>gleba: {entry.snapshot.soilMoistureAnalog ?? entry.snapshot.SoilMoistureAnalog}</span>
          <span>temp: {entry.snapshot.temperature ?? entry.snapshot.Temperature}</span>
          <span>wilg: {entry.snapshot.humidity ?? entry.snapshot.Humidity}</span>
          <span>woda: {entry.snapshot.waterLevelCm ?? entry.snapshot.WaterLevelCm} cm</span>
          <span>{new Date(entry.snapshot.timestamp || entry.snapshot.Timestamp || Date.now()).toLocaleTimeString()}</span>
        </div>
      ))}
    </div>
  );
}

export default LiveTelemetry;
