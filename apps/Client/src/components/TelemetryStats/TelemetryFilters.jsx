function TelemetryFilters({ hours, onHoursChange, plants, selectedPlantId, onPlantChange, deviceOptions, selectedDeviceId, onDeviceChange, onRefresh, isLoading }) {
  return (
    <div className="telemetry-filters">
      <label htmlFor="hours-window">Zakres:</label>
      <select id="hours-window" value={hours} onChange={(event) => onHoursChange(Number(event.target.value))}>
        <option value={1}>Ostatnia 1h</option>
        <option value={6}>Ostatnie 6h</option>
        <option value={12}>Ostatnie 12h</option>
        <option value={24}>Ostatnie 24h</option>
        <option value={72}>Ostatnie 72h</option>
      </select>

      <label htmlFor="plant-filter">Roślina:</label>
      <select id="plant-filter" value={selectedPlantId} onChange={(event) => onPlantChange(event.target.value)}>
        <option value="">Wszystkie / bez filtra</option>
        {plants.map((plant) => <option key={plant.id} value={plant.id}>{plant.name}</option>)}
      </select>

      <label htmlFor="device-filter">Urządzenie:</label>
      <select id="device-filter" value={selectedDeviceId} onChange={(event) => onDeviceChange(event.target.value)}>
        <option value="">Wszystkie sensory</option>
        {deviceOptions.map((device) => (
          <option key={device.id} value={device.externalDeviceId}>
            {device.name} ({device.externalDeviceId || 'brak external id'})
          </option>
        ))}
      </select>

      <button className="button-secondary" type="button" onClick={onRefresh} disabled={isLoading}>
        {isLoading ? 'Odświeżanie...' : 'Odśwież teraz'}
      </button>
    </div>
  );
}

export default TelemetryFilters;
