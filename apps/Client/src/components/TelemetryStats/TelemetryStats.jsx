import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import connectionManager, { getSignalRAccessToken, userDevicesEndpoint, userPlantsEndpoint, userTelemetryEndpoint, userTelemetryRefreshEndpoint } from '../../connectionManager';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { useNavigate } from 'react-router-dom';
import { API_BASE_URL } from '../../connectionManager';
import TelemetryFilters from './TelemetryFilters';
import LiveTelemetry from './LiveTelemetry';
import TelemetryDeviceCard from './TelemetryDeviceCard';
import { NUMERIC_SERIES, LIGHT_SERIES, LIVE_SNAPSHOT_TTL_MS, buildPath } from './telemetryStatsUtils';
import './TelemetryStats.css';

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
      .withUrl(`${API_BASE_URL}/telemetryHub`, { accessTokenFactory: getSignalRAccessToken })
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
      .withUrl(`${API_BASE_URL}/userHub`, { accessTokenFactory: getSignalRAccessToken })
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
      <div className="telemetry-card">
        <TelemetryFilters
          hours={hours}
          onHoursChange={setHours}
          plants={plants}
          selectedPlantId={selectedPlantId}
          onPlantChange={setSelectedPlantId}
          deviceOptions={deviceOptions}
          selectedDeviceId={selectedDeviceId}
          onDeviceChange={setSelectedDeviceId}
          onRefresh={loadTelemetry}
          isLoading={isLoading}
        />
        </div>

      {error ? <p className="telemetry-error">{error}</p> : null}
      <LiveTelemetry rows={liveRowsByDevice} />

      {!chartCards.length ? (
        <p className="telemetry-empty">Brak danych telemetrycznych dla wybranego zakresu.</p>
      ) : (
        <div className="telemetry-chart-overview">
          <div className="telemetry-device-grid">
            {chartCards.map((chartCard) => (
              <TelemetryDeviceCard
                key={`${chartCard.response.externalDeviceId}-${chartCard.response.deviceId}`}
                chartCard={chartCard}
                status={getDeviceStatus(chartCard)}
                onSeriesClick={(deviceId, seriesKey) => navigate(`/telemetry/${deviceId}?series=${seriesKey}&hours=${hours}&plantId=${selectedPlantId}&sensorField=${selectedSensorField}`)}
              />
            ))}
          </div>
        </div>
      )}
    </section>
  );
}

export default TelemetryStats;