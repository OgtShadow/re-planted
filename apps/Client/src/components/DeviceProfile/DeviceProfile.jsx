import { useEffect, useState } from 'react';
import StatusDot from '../StatusDot/StatusDot';
import { useNavigate } from 'react-router-dom';
import './DeviceProfile.css';

function DeviceProfile({ device }) {

  const navigate = useNavigate();
  const [selectedDuration, setSelectedDuration] = useState(10);
  const [isActivating, setIsActivating] = useState(false);
  const [feedback, setFeedback] = useState('');
  const [isOn, setIsOn] = useState(false);

  const name = device?.name ?? 'Unnamed device';
  const targetParameter = device?.targetParameter ?? '';
  const isLightDevice = /light|temperature/i.test(targetParameter);
  const commandPath = isLightDevice ? '/command/light' : '/command/pump';
  const mockBaseUrl = import.meta.env.VITE_SENSOR_MOCK_BASE_URL?.replace(/\/$/, '') || 'http://localhost:8085';

  useEffect(() => {
    const syncState = async () => {
      try {
        const response = await fetch(`${mockBaseUrl}/device-state`);
        if (!response.ok) {
          return;
        }

        const state = await response.json();
        const nextState = isLightDevice ? Boolean(state.lightOn) : Boolean(state.pumpOn);
        setIsOn(nextState);
      } catch {
        // ignore and keep current UI state
      }
    };

    syncState();
    const intervalId = window.setInterval(syncState, 1000);

    return () => window.clearInterval(intervalId);
  }, [mockBaseUrl, isLightDevice]);

  const handleActivate = async () => {
    setIsActivating(true);
    setFeedback('');

    try {
      const response = await fetch(`${mockBaseUrl}${commandPath}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ state: true, durationSeconds: selectedDuration }),
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(errorText || 'Nie udało się wysłać polecenia');
      }

      setIsOn(true);
      setFeedback(`Włączono na ${selectedDuration} s`);
    } catch (error) {
      setFeedback(error.message || 'Błąd połączenia');
    } finally {
      setIsActivating(false);
    }
  };

  return (
    <div className="device-profile-card">
      <div className="device-profile-header" onClick={() => navigate(`/device/${device?.id ?? 'default'}`)} style={{ cursor: 'pointer' }}>
        <h3>{name}</h3>
        <StatusDot status={isOn ? 'green' : 'gray'} size="medium" />
      </div>
      <p>Cel: {targetParameter || (isLightDevice ? 'light' : 'pump')}</p>
      <label className="device-profile-slider-label" htmlFor={`duration-${device?.id ?? 'default'}`}>
        Czas pracy: {selectedDuration} s
      </label>
      <input
        id={`duration-${device?.id ?? 'default'}`}
        className="device-profile-slider"
        type="range"
        min="1"
        max="60"
        step="1"
        value={selectedDuration}
        onChange={(event) => setSelectedDuration(Number(event.target.value))}
      />
      <button type="button" onClick={handleActivate} disabled={isActivating}>
        {isActivating ? 'Włączanie...' : `Włącz na ${selectedDuration} s`}
      </button>
      {feedback ? <p className="device-profile-feedback">{feedback}</p> : null}
    </div>
  );
}

export default DeviceProfile;
