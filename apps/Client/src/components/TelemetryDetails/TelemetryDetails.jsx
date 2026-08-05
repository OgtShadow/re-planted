import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import connectionManager, { userTelemetryEndpoint } from '../../connectionManager';
import './TelemetryDetails.css';

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
      const rawValue = Number(point[selectedKey] ?? 0);
      const y = height - (((rawValue - minY) / safeRange) * height);
      return `${index === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`;
    })
    .join(' ');
}

function buildPointCoordinates(points, selectedKey, minY, maxY) {
  if (!points.length) {
    return [];
  }

  const width = 1000;
  const height = 280;
  const safeRange = Math.max(1, maxY - minY);

  return points.map((point, index) => {
    const x = points.length === 1 ? 0 : (index / (points.length - 1)) * width;
    const rawValue = Number(point[selectedKey] ?? 0);
    const y = height - (((rawValue - minY) / safeRange) * height);
    return { x, y, rawValue, label: point.bucketStartUtc };
  });
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

function formatTimestamp(value) {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString('pl-PL', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function TelemetryDetails() {
  const navigate = useNavigate();
  const { deviceId } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const [hours, setHours] = useState(() => {
    const paramValue = Number(searchParams.get('hours'));
    return Number.isFinite(paramValue) ? paramValue : 24;
  });
  const [response, setResponse] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  const selectedSeriesParam = searchParams.get('series') || NUMERIC_SERIES[0].key;
  const selectedSeriesDefinition = NUMERIC_SERIES.find((series) => series.key === selectedSeriesParam)
    || (selectedSeriesParam === LIGHT_SERIES.key ? LIGHT_SERIES : NUMERIC_SERIES[0]);

  const loadTelemetry = useCallback(async () => {
    setIsLoading(true);
    setError('');

    try {
      const query = `?hours=${hours}&maxPoints=400${deviceId ? `&deviceId=${encodeURIComponent(deviceId)}` : ''}`;
      const data = await connectionManager.get(userTelemetryEndpoint(`/trends${query}`));
      setResponse(data);
    } catch (err) {
      setError(err?.message || 'Nie udało się pobrać danych telemetrycznych.');
    } finally {
      setIsLoading(false);
    }
  }, [deviceId, hours]);

  useEffect(() => {
    loadTelemetry();
  }, [loadTelemetry]);

  useEffect(() => {
    const intervalId = window.setInterval(() => {
      loadTelemetry();
    }, 30000);

    return () => window.clearInterval(intervalId);
  }, [loadTelemetry]);

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

  const selectedSeriesCard = useMemo(() => {
    if (!chartData) {
      return null;
    }

    if (selectedSeriesDefinition.key === LIGHT_SERIES.key) {
      return chartData.lightCard;
    }

    return chartData.numericCards.find((series) => series.key === selectedSeriesDefinition.key) ?? chartData.numericCards[0];
  }, [chartData, selectedSeriesDefinition]);

  const selectedSeriesPoints = useMemo(() => {
    if (!chartData || !selectedSeriesCard) {
      return [];
    }

    const minY = selectedSeriesDefinition.key === LIGHT_SERIES.key ? 0 : selectedSeriesCard.minValue;
    const maxY = selectedSeriesDefinition.key === LIGHT_SERIES.key ? 100 : selectedSeriesCard.maxValue;
    return buildPointCoordinates(chartData.points, selectedSeriesDefinition.key, minY, maxY);
  }, [chartData, selectedSeriesCard, selectedSeriesDefinition]);

  const handleSeriesChange = (event) => {
    const nextSeries = event.target.value;
    setSearchParams((previousParams) => {
      const next = new URLSearchParams(previousParams);
      next.set('series', nextSeries);
      return next;
    });
  };

  const handleHoursChange = (event) => {
    const nextHours = Number(event.target.value);
    setHours(nextHours);
    setSearchParams((previousParams) => {
      const next = new URLSearchParams(previousParams);
      next.set('hours', String(nextHours));
      return next;
    });
  };

  return (
    <section className="telemetry-details-page">
      <button type="button" className="back-button" onClick={() => navigate('/stats')}>
        ← Powrót do statystyk
      </button>

      <div className="telemetry-details-header">
        <div>
          <h2>Szczegóły telemetryki</h2>
          <p>Wykres i dokładne punkty dla wybranej serii z pełnym zestawem danych.</p>
        </div>
      </div>

      <div className="telemetry-controls">
        <label htmlFor="hours-window">Zakres:</label>
        <select id="hours-window" value={hours} onChange={handleHoursChange}>
          <option value={1}>Ostatnia 1h</option>
          <option value={6}>Ostatnie 6h</option>
          <option value={12}>Ostatnie 12h</option>
          <option value={24}>Ostatnie 24h</option>
          <option value={72}>Ostatnie 72h</option>
        </select>
      </div>

      {error ? <p className="telemetry-error">{error}</p> : null}

      {!chartData || !selectedSeriesCard ? (
        <p className="telemetry-empty">Brak danych telemetrycznych dla wybranego zakresu.</p>
      ) : (
        <div className="telemetry-details-layout">
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

          <div className="telemetry-detail-side">
            <article className="telemetry-series-item telemetry-side-card">
              <h3>Porównanie serii</h3>
              <div className="telemetry-compare-list">
                {chartData.numericCards.map((series) => (
                  <div key={series.key} className={`telemetry-compare-row ${selectedSeriesDefinition.key === series.key ? 'active' : ''}`} onClick={() => handleSeriesChange({ target: { value: series.key } })} style={{ cursor: 'pointer' }}>
                    <span style={{ color: series.color }}>■</span>
                    <span>{series.label}</span>
                    <strong>{formatValue(series.latest, series.unit)}</strong>
                  </div>
                ))}
                <div className={`telemetry-compare-row ${selectedSeriesDefinition.key === LIGHT_SERIES.key ? 'active' : ''}`} onClick={() => handleSeriesChange({ target: { value: LIGHT_SERIES.key } })} style={{ cursor: 'pointer' }}>
                  <span style={{ color: LIGHT_SERIES.color }}>■</span>
                  <span>{LIGHT_SERIES.label}</span>
                  <strong>{formatValue(chartData.lightCard.latest, LIGHT_SERIES.unit)}</strong>
                </div>
              </div>
            </article>

            <article className="telemetry-series-item telemetry-side-card">
              <h3>Dokładne wartości punktów</h3>
              <div className="telemetry-points-list">
                {chartData.points.map((point, index) => (
                  <div key={`${point.bucketStartUtc}-${index}`} className="telemetry-point-row">
                    <span>{formatTimestamp(point.bucketStartUtc)}</span>
                    <strong>{formatValue(Number(point[selectedSeriesDefinition.key] ?? 0), selectedSeriesDefinition.unit)}</strong>
                  </div>
                ))}
              </div>
            </article>
          </div>
        </div>
      )}
    </section>
  );
}

export default TelemetryDetails;
