import './DeviceParametersSeter.css';
import { useEffect, useMemo, useState } from 'react';
import connectionManager, { userDevicesEndpoint } from '../../connectionManager';

const DeviceParametersSeter = ({ device, setDevice }) => {
  const [catalog, setCatalog] = useState({
    targetParameters: [],
    supportedEffectTypes: ['increase', 'decrease', 'set'],
  });
  const [loadError, setLoadError] = useState(null);

  useEffect(() => {
    const loadCatalog = async () => {
      try {
        const result = await connectionManager.get(userDevicesEndpoint('/catalog'));
        if (result) {
          setCatalog({
            targetParameters: result.targetParameters || [],
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
