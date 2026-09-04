import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import connectionManager, { userDevicesEndpoint, userPlantsEndpoint, userTelemetryEndpoint, userTelemetryRefreshEndpoint } from '../../connectionManager';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { useNavigate } from 'react-router-dom';
import { API_BASE_URL } from '../../connectionManager';
import './TelemetryStats.css';

const NUMERIC_SERIES = [
  // raw value is a 3-digit fixed-point reading (e.g. 235 => 23.5°C)
  { key: 'temperatureAvg', label: 'Temperatura', unit: '°C', color: '#1f77b4', min: 0, max: 50, transform: (raw) => raw / 10 },
  // raw value is a 3-digit fixed-point reading (e.g. 580 => 58.0%)
  { key: 'humidityAvg', label: 'Wilgotność powietrza', unit: '%', color: '#2ca02c', min: 0, max: 100, transform: (raw) => raw / 10 },
  // raw ADC reading 0-4000, inverted: 0 = 100% moist, 4000 = 0% moist
  { key: 'soilMoistureAvg', label: 'Wilgotność gleby', unit: '%', color: '#8c564b', min: 0, max: 100, transform: (raw) => 100 - (raw / 4000) * 100 },
  { key: 'waterLevelAvg', label: 'Poziom wody (cm)', unit: 'cm', color: '#17becf', min: 0, max: 20, transform: (raw) => raw },
];

const LIGHT_SERIES = { key: 'lightOnPercent', label: 'Światło ON (%)', unit: '%', color: '#f39c12', min: 0, max: 100, transform: (raw) => raw };
const LIVE_SNAPSHOT_TTL_MS = 2 * 60 * 1000;

function buildPath(points, selectedKey, minY, maxY, transform = (raw) => raw) {
  if (!points.length) {
    return '';
  }

  const width = 1000;
  const height = 280;
  const safeRange = Math.max(1, maxY - minY);

  return points
    .map((point, index) => {
      const x = points.length === 1 ? 0 : (index / (points.length - 1)) * width;
      const y = height - ((transform(Number(point[selectedKey] ?? 0)) - minY) / safeRange) * height;
      return `${index === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`;
    })
    .join(' ');
}

function formatValue(value, unit) {
  if (!Number.isFinite(value)) {
    return '-';
  }

  if (unit === 'cm') {
    return `${value.toFixed(1)} cm`;
  }

  if (unit === '%') {
    return `${value.toFixed(1)}%`;
  }

  if (unit === '°C') {
    return `${value.toFixed(1)}°C`;
  }

  return value.toFixed(1);
}

function formatMinutes(totalMinutes) {
  const rounded = Math.max(0, Math.round(totalMinutes));
  const hours = Math.floor(rounded / 60);
  const minutes = rounded % 60;
  return `${hours}h ${minutes}m`;
}

function normalizeIdentifier(value) {
  return (value || '').trim().toLowerCase();
}

function toTimestamp(value) {
  const parsed = new Date(value || 0).getTime();
  return Number.isFinite(parsed) ? parsed : 0;
}

function TelemetryStats() {
  const navigate = useNavigate();
  const [hours, setHours] = useState(6);
  const [plants, setPlants] = useState([]);
  const [devices, setDevices] = useState([]);
  const [sensorFields, setSensorFields] = useState(['soilMoistureAnalog', 'lightIsDark', 'temperature', 'humidity', 'waterLevelCm']);
  const [selectedPlantId, setSelectedPlantId] = useState('');
  const [selectedSensorField, setSelectedSensorField] = useState('soilMoistureAnalog');
  const [selectedDeviceId, setSelectedDeviceId] = useState('');
  const [responses, setResponses] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [liveSnapshots, setLiveSnapshots] = useState([]);
  const hasRequestedInitialRefresh = useRef(false);

  const loadFilters = useCallback(async () => {
    try {
      const [plantsResult, devicesResult, catalogResult] = await Promise.all([
        connectionManager.get(userPlantsEndpoint()),
        connectionManager.get(userDevicesEndpoint()),
        connectionManager.get(userDevicesEndpoint('/catalog')),
      ]);

      if (Array.isArray(plantsResult)) {
        setPlants(plantsResult);
      }

      if (Array.isArray(devicesResult)) {
        const sensorDevices = devicesResult
          .filter((device) => (device.deviceKind || '').toLowerCase() === 'sensor')
          .sort((a, b) => (a.name || '').localeCompare(b.name || ''));
        setDevices(sensorDevices);
      }

      if (Array.isArray(catalogResult?.sensorFields) && catalogResult.sensorFields.length > 0) {
        setSensorFields(catalogResult.sensorFields);
        setSelectedSensorField((current) => (
          catalogResult.sensorFields.includes(current)
            ? current
            : catalogResult.sensorFields[0]
        ));
      }
    } catch (loadError) {
      console.error('Failed to load telemetry filters:', loadError);
    }
  }, []);

  useEffect(() => {
    loadFilters();
  }, [loadFilters]);

  const loadTelemetry = useCallback(async () => {
    setIsLoading(true);
    setError('');

    try {
      if (!hasRequestedInitialRefresh.current) {
        hasRequestedInitialRefresh.current = true;
        const snapshots = await connectionManager.post(userTelemetryRefreshEndpoint());
        if (Array.isArray(snapshots)) {
          setLiveSnapshots(snapshots);
        }
      }
      const params = new URLSearchParams();
      params.set('hours', String(hours));
      params.set('maxPoints', '240');
      if (selectedPlantId) {
        params.set('plantId', selectedPlantId);
      }
      params.set('sensorField', selectedSensorField);

      const query = `?${params.toString()}`;
      const data = await connectionManager.get(userTelemetryEndpoint(`/trends/all${query}`));
      const rows = Array.isArray(data) ? data : [];

      const normalizedSelectedDeviceId = normalizeIdentifier(selectedDeviceId);
      const filteredRows = normalizedSelectedDeviceId
        ? rows.filter((row) => {
            const normalizedDeviceId = normalizeIdentifier(row.deviceId);
            const normalizedExternalId = normalizeIdentifier(row.externalDeviceId);

            return normalizedDeviceId === normalizedSelectedDeviceId
              || normalizedExternalId === normalizedSelectedDeviceId
              || normalizedDeviceId.startsWith(`${normalizedSelectedDeviceId}-`)
              || normalizedSelectedDeviceId.startsWith(`${normalizedExternalId}-`);
          })
        : rows;

      setResponses(filteredRows);
    } catch (err) {
      setError(err?.message || 'Nie udało się pobrać danych telemetrycznych.');
      setResponses([]);
    } finally {
      setIsLoading(false);
    }
  }, [hours, selectedPlantId, selectedSensorField, selectedDeviceId]);

  useEffect(() => {
    loadTelemetry();
  }, [loadTelemetry]);

  useEffect(() => {
    const intervalId = window.setInterval(() => {
      loadTelemetry();
    }, 30000);

    return () => window.clearInterval(intervalId);
  }, [loadTelemetry]);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/telemetryHub`)
      .withAutomaticReconnect()
      .build();

    connection.on('TelemetryUpdated', (snapshots) => {
      if (!Array.isArray(snapshots) || snapshots.length === 0) {
        return;
      }

      setLiveSnapshots(snapshots);
      loadTelemetry();
    });

    connection.start().catch((signalrError) => {
      console.error('Telemetry SignalR connection failed:', signalrError);
    });

    return () => {
      connection.stop();
    };
  }, [loadTelemetry]);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/userHub`)
      .withAutomaticReconnect()
      .build();

    connection.on('DevicesUpdated', () => {
      loadFilters();
      loadTelemetry();
    });

    connection.on('PlantsUpdated', () => {
      loadFilters();
      loadTelemetry();
    });

    connection.start().catch((signalrError) => {
      console.error('Device/plant SignalR connection failed:', signalrError);
    });

    return () => {
      connection.stop();
    };
  }, [loadFilters, loadTelemetry]);

  const selectedDevice = useMemo(() => {
    if (!selectedDeviceId) {
      return null;
    }

    const normalizedSelectedDeviceId = normalizeIdentifier(selectedDeviceId);
    return devices.find((device) => {
      const normalizedExternalDeviceId = normalizeIdentifier(device.externalDeviceId);
      return normalizedExternalDeviceId === normalizedSelectedDeviceId;
    }) || null;
  }, [devices, selectedDeviceId]);

  const liveRowsByDevice = useMemo(() => {
    if (!Array.isArray(liveSnapshots) || liveSnapshots.length === 0) {
      return [];
    }

    const normalizedSelectedDeviceId = normalizeIdentifier(selectedDevice?.externalDeviceId || selectedDeviceId);

    const rows = liveSnapshots
      .map((snapshot) => {
        const snapshotDeviceId = (snapshot?.deviceId || snapshot?.DeviceId || '').trim();
        const normalizedSnapshotDeviceId = normalizeIdentifier(snapshotDeviceId);
        return {
          snapshot,
          snapshotDeviceId,
          normalizedSnapshotDeviceId,
        };
      })
      .filter((entry) => {
        if (!normalizedSelectedDeviceId) {
          return true;
        }

        return entry.normalizedSnapshotDeviceId === normalizedSelectedDeviceId
          || entry.normalizedSnapshotDeviceId.startsWith(`${normalizedSelectedDeviceId}-`);
      });

    return rows;
  }, [liveSnapshots, selectedDevice, selectedDeviceId]);

  const chartCards = useMemo(() => {
    if (!Array.isArray(responses) || responses.length === 0) {
      return [];
    }

    return responses.map((response) => {
      const points = response?.points ?? [];

      if (!points.length) {
        return {
          response,
          points,
          numericCards: [],
          lightCard: null,
        };
      }

      const numericCards = NUMERIC_SERIES.map((series) => {
        const values = points.map((point) => series.transform(Number(point[series.key] ?? 0)));
        const averageValue = values.reduce((sum, value) => sum + value, 0) / Math.max(1, values.length);

        return {
          ...series,
          path: buildPath(points, series.key, series.min, series.max, series.transform),
          latest: values[values.length - 1],
          minValue: Math.min(...values),
          maxValue: Math.max(...values),
          averageValue,
        };
      });

      const lightValues = points.map((point) => Number(point.lightOnPercent ?? 0));
      const lightPath = buildPath(points, LIGHT_SERIES.key, LIGHT_SERIES.min, LIGHT_SERIES.max);
      const lightOnMinutes = points.reduce((sum, point) => sum + Number(point.lightOnMinutes ?? 0), 0);
      const lightOffMinutes = points.reduce((sum, point) => sum + Number(point.lightOffMinutes ?? 0), 0);
      const lightTotal = Math.max(1, lightOnMinutes + lightOffMinutes);
      const lightOnShare = (lightOnMinutes * 100) / lightTotal;
      const lightAveragePercent = lightValues.reduce((sum, value) => sum + value, 0) / Math.max(1, lightValues.length);

      return {
        response,
        points,
        numericCards,
        lightCard: {
          ...LIGHT_SERIES,
          path: lightPath,
          latest: lightValues[lightValues.length - 1],
          minValue: Math.min(...lightValues),
          maxValue: Math.max(...lightValues),
          onMinutes: lightOnMinutes,
          offMinutes: lightOffMinutes,
          onShare: lightOnShare,
          averagePercent: lightAveragePercent,
        },
      };
    });
  }, [responses]);

  const totalPoints = useMemo(() => {
    return chartCards.reduce((sum, card) => sum + (card.points?.length ?? 0), 0);
  }, [chartCards]);

  const deviceOptions = useMemo(() => {
    return devices
      .filter((device) => (device.deviceKind || '').toLowerCase() === 'sensor')
      .map((device) => ({
        id: device.id,
        name: device.name,
        externalDeviceId: device.externalDeviceId || '',
      }));
  }, [devices]);

  const latestLiveSnapshotByDevice = useMemo(() => {
    const snapshots = new Map();

    for (const snapshot of liveSnapshots) {
      const rawDeviceId = snapshot?.deviceId || snapshot?.DeviceId || '';
      const normalizedDeviceId = normalizeIdentifier(rawDeviceId);
      if (!normalizedDeviceId) {
        continue;
      }

      const rawTimestamp = snapshot?.timestamp || snapshot?.Timestamp;
      const timestamp = toTimestamp(rawTimestamp);

      const current = snapshots.get(normalizedDeviceId);
      if (!current || timestamp > current.timestamp) {
        snapshots.set(normalizedDeviceId, { snapshot, timestamp });
      }
    }

    return snapshots;
  }, [liveSnapshots]);

  const getDeviceStatus = useCallback((chartCard) => {
    const telemetryId = normalizeIdentifier(chartCard.response.deviceId);
    const externalId = normalizeIdentifier(chartCard.response.externalDeviceId);
    const now = Date.now();

    const matchingSnapshots = Array.from(latestLiveSnapshotByDevice.entries())
      .filter(([snapshotDeviceId]) => (
        (telemetryId && (snapshotDeviceId === telemetryId || snapshotDeviceId.startsWith(`${telemetryId}-`) || telemetryId.startsWith(`${snapshotDeviceId}-`)))
        || (externalId && (snapshotDeviceId === externalId || snapshotDeviceId.startsWith(`${externalId}-`) || externalId.startsWith(`${snapshotDeviceId}-`)))
      ))
      .map(([, value]) => value);

    const latestSnapshotTimestamp = matchingSnapshots.reduce((max, entry) => Math.max(max, entry.timestamp), 0);
    const isLive = latestSnapshotTimestamp > 0 && (now - latestSnapshotTimestamp) <= LIVE_SNAPSHOT_TTL_MS;

    if (isLive) {
      return { key: 'live', label: 'LIVE' };
    }

    if (!chartCard.points.length) {
      return { key: 'no-data', label: 'BRAK DANYCH' };
    }

    return { key: 'offline', label: 'OFFLINE' };
  }, [latestLiveSnapshotByDevice]);

  return (
    <section className="telemetry-stats">
      <div className="telemetry-stats-header">
        <h2>Statystyki telemetryczne</h2>
        <p>Każde urządzenie sensoryczne ma własne wykresy i listę przypisanych roślin.</p>
      </div>

      <div className="telemetry-controls">
        <label htmlFor="hours-window">Zakres:</label>
        <select id="hours-window" value={hours} onChange={(event) => setHours(Number(event.target.value))}>
          <option value={1}>Ostatnia 1h</option>
          <option value={6}>Ostatnie 6h</option>
          <option value={12}>Ostatnie 12h</option>
          <option value={24}>Ostatnie 24h</option>
          <option value={72}>Ostatnie 72h</option>
        </select>

        <label htmlFor="plant-filter">Roślina:</label>
        <select id="plant-filter" value={selectedPlantId} onChange={(event) => setSelectedPlantId(event.target.value)}>
          <option value="">Wszystkie / bez filtra</option>
          {plants.map((plant) => (
            <option key={plant.id} value={plant.id}>
              {plant.name}
            </option>
          ))}
        </select>

        <label htmlFor="sensor-filter">Czujnik:</label>
        <select id="sensor-filter" value={selectedSensorField} onChange={(event) => setSelectedSensorField(event.target.value)}>
          {sensorFields.map((field) => (
            <option key={field} value={field}>
              {field}
            </option>
          ))}
        </select>

        <label htmlFor="device-filter">Urządzenie:</label>
        <select id="device-filter" value={selectedDeviceId} onChange={(event) => setSelectedDeviceId(event.target.value)}>
          <option value="">Wszystkie sensory</option>
          {deviceOptions.map((device) => (
            <option key={device.id} value={device.externalDeviceId}>
              {device.name} ({device.externalDeviceId || 'brak external id'})
            </option>
          ))}
        </select>

        <button type="button" onClick={loadTelemetry} disabled={isLoading}>
          {isLoading ? 'Odświeżanie...' : 'Odśwież teraz'}
        </button>
      </div>

      {error ? <p className="telemetry-error">{error}</p> : null}

      {liveRowsByDevice.length > 0 ? (
        <div className="telemetry-live-card">
          <strong>Live stream czujników</strong>
          {liveRowsByDevice.map((entry) => (
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
      ) : null}

      {!chartCards.length ? (
        <p className="telemetry-empty">Brak danych telemetrycznych dla wybranego zakresu.</p>
      ) : (
        <div className="telemetry-chart-card telemetry-chart-overview">
          <div className="telemetry-chart-meta">
            <span>Urządzenia na wykresach: {chartCards.length}</span>
            <span>Łączna liczba próbek: {totalPoints}</span>
            <span>Zakres: ostatnie {hours}h</span>
          </div>

          <div className="telemetry-device-grid">
            {chartCards.map((chartCard) => (
              <section className="telemetry-device-card" key={`${chartCard.response.externalDeviceId}-${chartCard.response.deviceId}`}>
                <div className="telemetry-device-meta">
                  <div className="telemetry-device-title-row">
                    <h3>{chartCard.response.deviceName || chartCard.response.externalDeviceId || chartCard.response.deviceId || 'Urządzenie'}</h3>
                    <span className={`telemetry-status-badge telemetry-status-${getDeviceStatus(chartCard).key}`}>
                      {getDeviceStatus(chartCard).label}
                    </span>
                  </div>
                  <div>
                    <span>Telemetry id: {chartCard.response.deviceId || 'brak'}</span>
                    <span>External id: {chartCard.response.externalDeviceId || 'brak'}</span>
                    <span>Rośliny: {chartCard.response.plantNames?.length ? chartCard.response.plantNames.join(', ') : 'brak przypisania'}</span>
                    <span>Próbki: {chartCard.points.length}</span>
                    <span>Bucket: co {chartCard.response.intervalMinutes ?? 1} min</span>
                  </div>
                </div>

                {!chartCard.points.length ? (
                  <p className="telemetry-empty">Brak próbek telemetrycznych dla tego urządzenia w wybranym zakresie.</p>
                ) : (
                  <div className="telemetry-series-grid">
                    {chartCard.numericCards.map((series) => (
                      <article
                        className="telemetry-series-item"
                        key={`${chartCard.response.deviceId}-${series.key}`}
                        onClick={() => navigate(`/telemetry/${chartCard.response.deviceId || chartCard.response.externalDeviceId || 'unknown'}?series=${series.key}&hours=${hours}&plantId=${selectedPlantId}&sensorField=${selectedSensorField}`)}
                      >
                        <h3>{series.label}</h3>
                        <svg viewBox="0 0 1000 320" className="telemetry-chart" role="img" aria-label={`Wykres serii ${series.label}`}>
                          <line x1="0" y1="280" x2="1000" y2="280" className="axis" />
                          <line x1="0" y1="0" x2="0" y2="280" className="axis" />
                          <path d={series.path} stroke={series.color} strokeWidth="3" fill="none" strokeLinejoin="round" strokeLinecap="round" />
                        </svg>
                        <div className="telemetry-summary">
                          <div>
                            <strong>Aktualnie:</strong> {formatValue(series.latest, series.unit)}
                          </div>
                          <div>
                            <strong>Średnia:</strong> {formatValue(series.averageValue, series.unit)}
                          </div>
                          <div>
                            <strong>Min:</strong> {formatValue(series.minValue, series.unit)}
                          </div>
                          <div>
                            <strong>Max:</strong> {formatValue(series.maxValue, series.unit)}
                          </div>
                        </div>
                      </article>
                    ))}

                    {chartCard.lightCard ? (
                      <article className="telemetry-series-item telemetry-light-item">
                        <h3>Światło (ON/OFF)</h3>
                        <svg viewBox="0 0 1000 320" className="telemetry-chart" role="img" aria-label="Wykres udziału czasu światła ON">
                          <line x1="0" y1="280" x2="1000" y2="280" className="axis" />
                          <line x1="0" y1="0" x2="0" y2="280" className="axis" />
                          <path d={chartCard.lightCard.path} stroke={chartCard.lightCard.color} strokeWidth="3" fill="none" strokeLinejoin="round" strokeLinecap="round" />
                        </svg>
                        <div className="telemetry-summary">
                          <div>
                            <strong>ON:</strong> {formatMinutes(chartCard.lightCard.onMinutes)}
                          </div>
                          <div>
                            <strong>OFF:</strong> {formatMinutes(chartCard.lightCard.offMinutes)}
                          </div>
                          <div>
                            <strong>Udział ON:</strong> {formatValue(chartCard.lightCard.onShare, '%')}
                          </div>
                          <div>
                            <strong>Średnia ON:</strong> {formatValue(chartCard.lightCard.averagePercent, '%')}
                          </div>
                        </div>
                      </article>
                    ) : null}
                  </div>
                )}
              </section>
            ))}
          </div>
        </div>
      )}
    </section>
  );
}

export default TelemetryStats;