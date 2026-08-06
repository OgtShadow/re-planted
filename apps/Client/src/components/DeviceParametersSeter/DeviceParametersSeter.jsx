import './DeviceParametersSeter.css';
import { useEffect, useMemo, useState } from 'react';
import connectionManager, { userDevicesEndpoint } from '../../connectionManager';

const DeviceParametersSeter = ({ device, setDevice }) => {
  const [catalog, setCatalog] = useState({
    supportedDeviceKinds: ['sensor', 'actuator'],
    targetParameters: [],
    sensorFields: ['soilMoistureAnalog', 'lightIsDark', 'temperature', 'humidity', 'waterLevelCm'],
    supportedEffectTypes: ['increase', 'decrease', 'set'],
  });
  const [loadError, setLoadError] = useState(null);

  useEffect(() => {
    const loadCatalog = async () => {
      try {
        const result = await connectionManager.get(userDevicesEndpoint('/catalog'));
        if (result) {
          setCatalog({
            supportedDeviceKinds: result.supportedDeviceKinds || ['sensor', 'actuator'],
            targetParameters: result.targetParameters || [],
            sensorFields: result.sensorFields || ['soilMoistureAnalog', 'lightIsDark', 'temperature', 'humidity', 'waterLevelCm'],
            supportedEffectTypes: result.supportedEffectTypes || ['increase', 'decrease', 'set'],
          });
        }
      } catch (error) {
        setLoadError(error.message || 'Unable to load device catalog');
      }
    };

    loadCatalog();
  }, []);

  const targetOptions = useMemo(() => catalog.targetParameters || [], [catalog.targetParameters]);
  const selectedTarget = useMemo(
    () => targetOptions.find((target) => target.key === device?.targetParameter),
    [targetOptions, device?.targetParameter]
  );

  const updateDeviceField = (fieldName, value) => {
    if (!setDevice) {
      return;
    }

    setDevice((prevDevice) => ({
      ...(prevDevice || {}),
      [fieldName]: value,
    }));
  };

  const handleNameChange = (value) => {
    updateDeviceField('name', value);
  };

  const handleTargetParameterChange = (value) => {
    const target = targetOptions.find((item) => item.key === value);
    updateDeviceField('targetParameter', value);
    if (target?.suggestedEffectType) {
      updateDeviceField('effectType', target.suggestedEffectType);
    }
  };

  const handleDeviceKindChange = (value) => {
    updateDeviceField('deviceKind', value);
  };

  const handleExternalDeviceIdChange = (value) => {
    updateDeviceField('externalDeviceId', value);
  };

  const handleToggleSensorField = (sensorField) => {
    const current = Array.isArray(device?.sensorFields) ? device.sensorFields : [];
    const next = current.includes(sensorField)
      ? current.filter((field) => field !== sensorField)
      : [...current, sensorField];

    updateDeviceField('sensorFields', next);
  };

  const handleEffectTypeChange = (value) => {
    updateDeviceField('effectType', value);
  };

  const handleEffectStrengthChange = (value) => {
    updateDeviceField('effectStrength', Number(value));
  };

  return (
    <div className="parameters-seter">
      {loadError && <p className="error">Błąd ładowania katalogu: {loadError}</p>}
      <ul style={{ listStyle: 'none', padding: 0 }}>
        <li>
          <label>
            Nazwa urządzenia
            <input
              type="text"
              value={device?.name || ''}
              onChange={(e) => handleNameChange(e.target.value)}
            />
          </label>
        </li>

        <li>
          <label>
            Typ urządzenia
            <select
              value={device?.deviceKind || 'actuator'}
              onChange={(e) => handleDeviceKindChange(e.target.value)}
            >
              {(catalog.supportedDeviceKinds || ['sensor', 'actuator']).map((kind) => (
                <option key={kind} value={kind}>
                  {kind}
                </option>
              ))}
            </select>
          </label>
        </li>

        <li>
          <label>
            External device id (telemetria)
            <input
              type="text"
              value={device?.externalDeviceId || ''}
              onChange={(e) => handleExternalDeviceIdChange(e.target.value)}
            />
          </label>
        </li>

        <li>
          <label>Czujniki urządzenia</label>
          <div>
            {(catalog.sensorFields || []).map((sensorField) => (
              <label key={sensorField} style={{ display: 'block' }}>
                <input
                  type="checkbox"
                  checked={(device?.sensorFields || []).includes(sensorField)}
                  onChange={() => handleToggleSensorField(sensorField)}
                />
                {sensorField}
              </label>
            ))}
          </div>
        </li>

        <li>
          <label>
            Cel urządzenia
            <select
              value={device?.targetParameter || ''}
              onChange={(e) => handleTargetParameterChange(e.target.value)}
            >
              <option value="" disabled>
                Wybierz parametr
              </option>
              {targetOptions.map((parameter) => (
                <option key={parameter.key} value={parameter.key}>
                  {parameter.key} ({parameter.sensorField})
                </option>
              ))}
            </select>
          </label>
        </li>

        <li>
          <label>
            Domyślny handler
            <input
              type="text"
              value={selectedTarget ? `${selectedTarget.defaultCommand} (${selectedTarget.defaultCommandPath})` : ''}
              readOnly
            />
          </label>
        </li>

        <li>
          <label>
            Pole stanu z sensora
            <select disabled value={selectedTarget?.defaultStateField || 'pumpState'}>
              {(selectedTarget?.defaultStateField ? [selectedTarget.defaultStateField] : ['pumpState']).map((parameter) => (
                <option key={parameter} value={parameter}>
                  {parameter}
                </option>
              ))}
            </select>
          </label>
        </li>

        <li>
          <label>
            Typ działania
            <select value={device?.effectType || ''} onChange={(e) => handleEffectTypeChange(e.target.value)}>
              {catalog.supportedEffectTypes.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </select>
          </label>
        </li>

        <li>
          <label>
            Siła działania
            <input
              type="number"
              min="0"
              step="0.1"
              value={device?.effectStrength ?? 1}
              onChange={(e) => handleEffectStrengthChange(e.target.value)}
            />
          </label>
        </li>
      </ul>
    </div>
  );
};

export default DeviceParametersSeter;
