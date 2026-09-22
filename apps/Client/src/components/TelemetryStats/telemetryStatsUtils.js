export const NUMERIC_SERIES = [
  { key: 'temperatureAvg', label: 'Temperatura', unit: '°C', color: '#1f77b4', min: 0, max: 50, transform: (raw) => raw / 10 },
  { key: 'humidityAvg', label: 'Wilgotność powietrza', unit: '%', color: '#2ca02c', min: 0, max: 100, transform: (raw) => raw / 10 },
  { key: 'soilMoistureAvg', label: 'Wilgotność gleby', unit: '%', color: '#8c564b', min: 0, max: 100, transform: (raw) => 100 - (raw / 4000) * 100 },
  { key: 'waterLevelAvg', label: 'Poziom wody (cm)', unit: 'cm', color: '#17becf', min: 0, max: 20, transform: (raw) => raw },
];

export const LIGHT_SERIES = { key: 'lightOnPercent', label: 'Światło ON (%)', unit: '%', color: '#f39c12', min: 0, max: 100, transform: (raw) => raw };
export const LIVE_SNAPSHOT_TTL_MS = 2 * 60 * 1000;

export function buildPath(points, selectedKey, minY, maxY, transform = (raw) => raw) {
  if (!points.length) return '';

  const width = 1000;
  const height = 280;
  const safeRange = Math.max(1, maxY - minY);

  return points.map((point, index) => {
    const x = points.length === 1 ? 0 : (index / (points.length - 1)) * width;
    const y = height - ((transform(Number(point[selectedKey] ?? 0)) - minY) / safeRange) * height;
    return `${index === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`;
  }).join(' ');
}

export function formatValue(value, unit) {
  if (!Number.isFinite(value)) return '-';
  if (unit === 'cm') return `${value.toFixed(1)} cm`;
  if (unit === '%') return `${value.toFixed(1)}%`;
  if (unit === '°C') return `${value.toFixed(1)}°C`;
  return value.toFixed(1);
}

export function formatMinutes(totalMinutes) {
  const rounded = Math.max(0, Math.round(totalMinutes));
  return `${Math.floor(rounded / 60)}h ${rounded % 60}m`;
}
