import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import connectionManager, { userTelemetryEndpoint } from '../../connectionManager';
import TelemetryChartCard from './TelemetryChartCard';
import TelemetrySeriesComparison from './TelemetrySeriesComparison';
import TelemetryPointsList from './TelemetryPointsList';
import { NUMERIC_SERIES, LIGHT_SERIES, buildPath, buildPointCoordinates } from './telemetryUtils';
import './TelemetryDetails.css';

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
  const selectedPlantId = searchParams.get('plantId') || '';
  const selectedSensorField = searchParams.get('sensorField') || 'soilMoistureAnalog';
  const selectedSeriesDefinition = NUMERIC_SERIES.find((series) => series.key === selectedSeriesParam)
    || (selectedSeriesParam === LIGHT_SERIES.key ? LIGHT_SERIES : NUMERIC_SERIES[0]);

  const loadTelemetry = useCallback(async () => {
    setIsLoading(true);
    setError('');

    try {
      const params = new URLSearchParams();
      params.set('hours', String(hours));
      params.set('maxPoints', '400');
      if (deviceId) {
        params.set('deviceId', deviceId);
      }
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
  }, [deviceId, hours, selectedPlantId, selectedSensorField]);

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

    return buildPointCoordinates(chartData.points, selectedSeriesDefinition.key, selectedSeriesDefinition.min, selectedSeriesDefinition.max, selectedSeriesDefinition.transform);
  }, [chartData, selectedSeriesCard, selectedSeriesDefinition]);

  const handleSeriesSelect = (nextSeries) => {
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
          <TelemetryChartCard
            response={response}
            deviceId={deviceId}
            chartData={chartData}
            selectedSeriesDefinition={selectedSeriesDefinition}
            selectedSeriesCard={selectedSeriesCard}
            selectedSeriesPoints={selectedSeriesPoints}
          />

          <div className="telemetry-detail-side">
            <TelemetrySeriesComparison
              chartData={chartData}
              selectedSeriesDefinition={selectedSeriesDefinition}
              onSeriesSelect={handleSeriesSelect}
            />

            <TelemetryPointsList points={chartData.points} selectedSeriesDefinition={selectedSeriesDefinition} />
          </div>
        </div>
      )}
    </section>
  );
}

export default TelemetryDetails;
