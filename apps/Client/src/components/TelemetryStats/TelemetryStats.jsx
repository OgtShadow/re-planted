import { useCallback, useEffect, useMemo, useState } from 'react';
import connectionManager, { userDevicesEndpoint, userPlantsEndpoint, userTelemetryEndpoint } from '../../connectionManager';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { useNavigate } from 'react-router-dom';
import { API_BASE_URL } from '../../connectionManager';
import './TelemetryStats.css';

const NUMERIC_SERIES = [
  { key: 'temperatureAvg', label: 'Temperatura', unit: 'raw', color: '#1f77b4' },
  { key: 'humidityAvg', label: 'Wilgotność powietrza', unit: 'raw', color: '#2ca02c' },
  { key: 'soilMoistureAvg', label: 'Wilgotność gleby', unit: 'raw', color: '#8c564b' },
  { key: 'waterLevelAvg', label: 'Poziom wody (cm)', unit: 'cm', color: '#17becf' },
];

const LIGHT_SERIES = { key: 'lightOnPercent', label: 'Światło ON (%)', unit: '%', color: '#f39c12' };

function buildPath(points, selectedKey, minY, maxY) {
  if (!points.length) {
    return '';
  }

  const width = 1000;
  const height = 280;
  const safeRange = Math.max(1, maxY - minY);

  return points
    .map((point, index) => {
      const x = points.length === 1 ? 0 : (index / (points.length - 1)) * width;
      const y = height - (((point[selectedKey] ?? 0) - minY) / safeRange) * height;
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

  return value.toFixed(1);
}

function formatMinutes(totalMinutes) {
  const rounded = Math.max(0, Math.round(totalMinutes));
  const hours = Math.floor(rounded / 60);
  const minutes = rounded % 60;
  return `${hours}h ${minutes}m`;
}

function TelemetryStats() {
  const navigate = useNavigate();
  const [hours, setHours] = useState(6);
  const [plants, setPlants] = useState([]);
  const [sensorFields, setSensorFields] = useState(['soilMoistureAnalog', 'lightIsDark', 'temperature', 'humidity', 'waterLevelCm']);
  const [selectedPlantId, setSelectedPlantId] = useState('');
  const [selectedSensorField, setSelectedSensorField] = useState('soilMoistureAnalog');
  const [response, setResponse] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isSeeding, setIsSeeding] = useState(false);
  const [error, setError] = useState('');
  const [info, setInfo] = useState('');
  const [liveSnapshots, setLiveSnapshots] = useState([]);

  useEffect(() => {
    const loadFilters = async () => {
      try {
        const [plantsResult, catalogResult] = await Promise.all([
          connectionManager.get(userPlantsEndpoint()),
          connectionManager.get(userDevicesEndpoint('/catalog')),
        ]);

        if (Array.isArray(plantsResult)) {
          setPlants(plantsResult);
        }

        if (Array.isArray(catalogResult?.sensorFields) && catalogResult.sensorFields.length > 0) {
          setSensorFields(catalogResult.sensorFields);
          setSelectedSensorField(catalogResult.sensorFields[0]);
        }
      } catch {
      }
    };

    loadFilters();
  }, []);

  const loadTelemetry = useCallback(async () => {
    setIsLoading(true);
    setError('');

    try {
      const params = new URLSearchParams();
      params.set('hours', String(hours));
      params.set('maxPoints', '240');
      if (selectedPlantId) {
        params.set('plantId', selectedPlantId);
        params.set('sensorField', selectedSensorField);
      }

      const query = `?${params.toString()}`;
      const data = await connectionManager.get(userTelemetryEndpoint(`/trends${query}`));
      setResponse(data);
    } catch (err) {
      setError(err?.message || 'Nie udało się pobrać danych telemetrycznych.');
    } finally {
      setIsLoading(false);
    }
  }, [hours, selectedPlantId, selectedSensorField]);

  //====================================================================
  //Dane testowe telemetryczne do testowania wykresów potem usunąć :>
  //====================================================================
  const handleSeedTestData = async () => {
    setIsSeeding(true);
    setError('');
    setInfo('');

    try {
      const result = await connectionManager.post(
        userTelemetryEndpoint('/seed-test-data?hours=72&stepMinutes=5&replaceExisting=true'),
        {}
      );
      setInfo(`Wygenerowano ${result?.insertedBuckets ?? 0} bucketów danych testowych z 72h.`);
      setHours(72);
      await loadTelemetry();
    } catch (err) {
      setError(err?.message || 'Nie udało się wygenerować danych testowych.');
    } finally {
      setIsSeeding(false);
    }
  };
  //====================================================================
  //--------------------------------------------------------------------
  //====================================================================
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

  const liveRows = useMemo(() => {
    if (!Array.isArray(liveSnapshots) || liveSnapshots.length === 0) {
      return [];
    }

    const resolvedDeviceId = response?.deviceId || '';
    const normalized = resolvedDeviceId.trim().toLowerCase();

    if (!normalized) {
      return liveSnapshots;
    }

    const exact = liveSnapshots.filter((snapshot) => (snapshot?.deviceId || '').trim().toLowerCase() === normalized);
    if (exact.length > 0) {
      return exact;
    }

    return liveSnapshots;
  }, [liveSnapshots, response?.deviceId]);

  const chartData = useMemo(() => {
    const points = response?.points ?? [];
    if (!points.length) {
      return null;
    }

    const numericCards = NUMERIC_SERIES.map((series) => {
      const values = points.map((point) => Number(point[series.key] ?? 0));
      const minY = Math.min(...values);
      const maxY = Math.max(...values);
      const averageValue = values.reduce((sum, value) => sum + value, 0) / Math.max(1, values.length);

      return {
        ...series,
        path: buildPath(points, series.key, minY, maxY),
        minY,
        maxY,
        latest: values[values.length - 1],
        minValue: minY,
        maxValue: maxY,
        averageValue,
      };
    });

    const lightValues = points.map((point) => Number(point.lightOnPercent ?? 0));
    const lightPath = buildPath(points, LIGHT_SERIES.key, 0, 100);
    const lightOnMinutes = points.reduce((sum, point) => sum + Number(point.lightOnMinutes ?? 0), 0);
    const lightOffMinutes = points.reduce((sum, point) => sum + Number(point.lightOffMinutes ?? 0), 0);
    const lightTotal = Math.max(1, lightOnMinutes + lightOffMinutes);
    const lightOnShare = (lightOnMinutes * 100) / lightTotal;
    const lightAveragePercent = lightValues.reduce((sum, value) => sum + value, 0) / Math.max(1, lightValues.length);

    return {
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
  }, [response]);

  return (
    <section className="telemetry-stats">
      <div className="telemetry-stats-header">
        <h2>Statystyki telemetryczne</h2>
        <p>
          Dane z mock ESP są zbierane cyklicznie przez backend i zapisywane minutowo. Widok odświeża się co 30 sekund.
        </p>
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

        <button type="button" onClick={loadTelemetry} disabled={isLoading}>
          {isLoading ? 'Odświeżanie...' : 'Odśwież teraz'}
        </button>

        <button type="button" onClick={handleSeedTestData} disabled={isSeeding}>
          {isSeeding ? 'Generowanie...' : 'Generuj testowe 72h'}
        </button>
      </div>

      {info ? <p>{info}</p> : null}
      {error ? <p className="telemetry-error">{error}</p> : null}

      {liveRows.length > 0 ? (
        <div className="telemetry-live-card">
          <strong>Live stream czujników</strong>
          {liveRows.map((snapshot) => (
            <div key={`${snapshot.deviceId}-${snapshot.timestamp}`} className="telemetry-live-row">
              <span>{snapshot.deviceId}</span>
              <span>gleba: {snapshot.soilMoistureAnalog}</span>
              <span>temp: {snapshot.temperature}</span>
              <span>wilg: {snapshot.humidity}</span>
              <span>woda: {snapshot.waterLevelCm} cm</span>
              <span>{new Date(snapshot.timestamp).toLocaleTimeString()}</span>
            </div>
          ))}
        </div>
      ) : null}

      {!chartData ? (
        <p className="telemetry-empty">Brak danych telemetrycznych dla wybranego zakresu.</p>
      ) : (
        <div className="telemetry-chart-card">
          <div className="telemetry-chart-meta">
            <span>Urządzenie: {response?.deviceId || 'n/a'}</span>
            <span>Próbki: {chartData.points.length}</span>
            <span>Bucket: co {response?.intervalMinutes ?? 1} min</span>
          </div>

          <div className="telemetry-series-grid">
            {chartData.numericCards.map((series) => (
              <article className="telemetry-series-item" key={series.key} onClick={() => navigate(`/telemetry/${response?.deviceId ?? 'unknown'}?series=${series.key}&hours=${hours}&plantId=${selectedPlantId}&sensorField=${selectedSensorField}`)}>
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

            <article className="telemetry-series-item telemetry-light-item">
              <h3>Światło (ON/OFF)</h3>
              <svg viewBox="0 0 1000 320" className="telemetry-chart" role="img" aria-label="Wykres udziału czasu światła ON">
                <line x1="0" y1="280" x2="1000" y2="280" className="axis" />
                <line x1="0" y1="0" x2="0" y2="280" className="axis" />
                <path d={chartData.lightCard.path} stroke={chartData.lightCard.color} strokeWidth="3" fill="none" strokeLinejoin="round" strokeLinecap="round" />
              </svg>
              <div className="telemetry-summary">
                <div>
                  <strong>ON:</strong> {formatMinutes(chartData.lightCard.onMinutes)}
                </div>
                <div>
                  <strong>OFF:</strong> {formatMinutes(chartData.lightCard.offMinutes)}
                </div>
                <div>
                  <strong>Udział ON:</strong> {formatValue(chartData.lightCard.onShare, '%')}
                </div>
                <div>
                  <strong>Średnia ON:</strong> {formatValue(chartData.lightCard.averagePercent, '%')}
                </div>
              </div>
            </article>
          </div>
        </div>
      )}
    </section>
  );
}

export default TelemetryStats;