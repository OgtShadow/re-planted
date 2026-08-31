export const NUMERIC_SERIES = [
  // raw value is a 3-digit fixed-point reading (e.g. 235 => 23.5°C)
  { key: 'temperatureAvg', label: 'Temperatura', unit: '°C', color: '#1f77b4', min: 0, max: 50, transform: (raw) => raw / 10 },
  // raw value is a 3-digit fixed-point reading (e.g. 580 => 58.0%)
  { key: 'humidityAvg', label: 'Wilgotność powietrza', unit: '%', color: '#2ca02c', min: 0, max: 100, transform: (raw) => raw / 10 },
  // raw ADC reading 0-4000, inverted: 0 = 100% moist, 4000 = 0% moist
  { key: 'soilMoistureAvg', label: 'Wilgotność gleby', unit: '%', color: '#8c564b', min: 0, max: 100, transform: (raw) => 100 - (raw / 4000) * 100 },
  { key: 'waterLevelAvg', label: 'Poziom wody (cm)', unit: 'cm', color: '#17becf', min: 0, max: 20, transform: (raw) => raw },
];

export const LIGHT_SERIES = { key: 'lightOnPercent', label: 'Światło ON (%)', unit: '%', color: '#f39c12', min: 0, max: 100, transform: (raw) => raw };

export function buildPath(points, selectedKey, minY, maxY, transform = (raw) => raw) {
  if (!points.length) {
    return '';
  }

  const width = 1000;
  const height = 280;
  const safeRange = Math.max(1, maxY - minY);

  return points
    .map((point, index) => {
      const x = points.length === 1 ? 0 : (index / (points.length - 1)) * width;
      const rawValue = transform(Number(point[selectedKey] ?? 0));
      const y = height - (((rawValue - minY) / safeRange) * height);
      return `${index === 0 ? 'M' : 'L'} ${x.toFixed(1)} ${y.toFixed(1)}`;
    })
    .join(' ');
}

export function buildPointCoordinates(points, selectedKey, minY, maxY, transform = (raw) => raw) {
  if (!points.length) {
    return [];
  }

  const width = 1000;
  const height = 280;
  const safeRange = Math.max(1, maxY - minY);

  return points.map((point, index) => {
    const x = points.length === 1 ? 0 : (index / (points.length - 1)) * width;
    const rawValue = transform(Number(point[selectedKey] ?? 0));
    const y = height - (((rawValue - minY) / safeRange) * height);
    return { x, y, rawValue, label: point.bucketStartUtc };
  });
}

export function formatValue(value, unit) {
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

export function formatMinutes(totalMinutes) {
  const rounded = Math.max(0, Math.round(totalMinutes));
  const hours = Math.floor(rounded / 60);
  const minutes = rounded % 60;
  return `${hours}h ${minutes}m`;
}

export function formatTimestamp(value) {
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
