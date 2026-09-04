import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import DeviceEditWindow from '../DeviceEditWindow/DeviceEditWindow';
import connectionManager, { userDevicesEndpoint, userPlantsEndpoint } from '../../connectionManager';
import StatusDot from '../StatusDot/StatusDot';
import './DeviceDetails.css';

function DeviceDetails() {
    const { id } = useParams();
    const navigate = useNavigate();
    const [device, setDevice] = useState(null);
    const [plants, setPlants] = useState([]);
    const [selectedPlantId, setSelectedPlantId] = useState('');
    const [assignMessage, setAssignMessage] = useState('');
    const [isEditing, setIsEditing] = useState(false);
    const [loading, setLoading] = useState(true);
    const [manualDurationSeconds, setManualDurationSeconds] = useState(2);
    const [manualStatus, setManualStatus] = useState('');
    const [isCommandPending, setIsCommandPending] = useState(false);
    const executionTimerRef = useRef(null);

    useEffect(() => () => {
        if (executionTimerRef.current) {
            window.clearTimeout(executionTimerRef.current);
        }
    }, []);

    useEffect(() => {
        const fetchData = async () => {
            try {
                const [deviceResult, plantsResult] = await Promise.all([
                    connectionManager.get(userDevicesEndpoint(`/${id}`)),
                    connectionManager.get(userPlantsEndpoint()),
                ]);

                setDevice(deviceResult);
                if (Array.isArray(plantsResult)) {
                    setPlants(plantsResult);
                }
            } catch (error) {
                console.error("Failed to fetch device details:", error);
            } finally {
                setLoading(false);
            }
        };

        fetchData();
    }, [id]);

    const refreshDevice = async () => {
        const result = await connectionManager.get(userDevicesEndpoint(`/${id}`));
        setDevice(result);
    };

    const handleAssignPlant = async () => {
        if (!selectedPlantId) {
            return;
        }

        try {
            const result = await connectionManager.put(userDevicesEndpoint(`/${id}/plants/${selectedPlantId}`), {});
            setAssignMessage(result?.Response || result?.response || 'Przypisano urządzenie do rośliny.');
            await refreshDevice();
        } catch (error) {
            setAssignMessage(error?.message || 'Nie udało się przypisać urządzenia do rośliny.');
        }
    };

    const handleUnassignPlant = async (plantId) => {
        try {
            const result = await connectionManager.delete(userDevicesEndpoint(`/${id}/plants/${plantId}`));
            setAssignMessage(result?.Response || result?.response || 'Odpięto urządzenie od rośliny.');
            await refreshDevice();
        } catch (error) {
            setAssignMessage(error?.message || 'Nie udało się odpiąć urządzenia od rośliny.');
        }
    };

    const handleManualPump = async () => {
        if (isSensorDevice || !device.isEnabled || isCommandPending) return;
        if (!window.confirm(`Uruchomić pompę na ${manualDurationSeconds} s?`)) return;

        setIsCommandPending(true);
        setManualStatus('Wysyłanie komendy do urządzenia...');
        try {
            await connectionManager.post(userDevicesEndpoint(`/${id}/manual/pump`), {
                durationMs: Number(manualDurationSeconds) * 1000,
            });
            setManualStatus('Komenda przyjęta przez kontroler. Oczekiwanie na wykonanie...');
            if (executionTimerRef.current) {
                window.clearTimeout(executionTimerRef.current);
            }
            executionTimerRef.current = window.setTimeout(() => {
                setManualStatus('Wykonanie komendy zakończone.');
                executionTimerRef.current = null;
            }, Number(manualDurationSeconds) * 1000 + 1500);
        } catch (error) {
            setManualStatus(error?.message || 'Nie udało się wysłać komendy.');
        } finally {
            setIsCommandPending(false);
        }
    };

    const handleEmergencyStop = async () => {
        setIsCommandPending(true);
        setManualStatus('Wysyłanie zatrzymania awaryjnego...');
        try {
            await connectionManager.post(userDevicesEndpoint(`/${id}/manual/stop`));
            if (executionTimerRef.current) {
                window.clearTimeout(executionTimerRef.current);
                executionTimerRef.current = null;
            }
            setManualStatus('Zatrzymanie awaryjne przyjęte przez kontroler.');
        } catch (error) {
            setManualStatus(error?.message || 'Nie udało się zatrzymać urządzenia.');
        } finally {
            setIsCommandPending(false);
        }
    };

    const handleEditClose = (response) => {
        setIsEditing(false);
        if (response) {
            try {
                 const parsed = JSON.parse(response);
                 const responseText = parsed.Response || parsed.response;
                 if (parsed && typeof parsed === 'object' && responseText && responseText.includes("Usunięto")) {
                     navigate("/");
                     return;
                 }
            } catch (error) { 
                console.error("Failed to parse response:", error);
            }
            refreshDevice();
        }
    };

    if (loading) return <div className="device-details-container">Loading...</div>;
    if (!device) return <div className="device-details-container">Device not found</div>;

    const assignedPlantIds = new Set((device.plants || []).map((plant) => plant.id));
    const assignablePlants = plants.filter((plant) => !assignedPlantIds.has(plant.id));
    const isSensorDevice = (device.deviceKind || '').toLowerCase() === 'sensor';

    return (
        <div className="device-details-container">
            <button className="back-button" onClick={() => navigate("/devices")}>&larr; Back to List</button>
            
            <div className="device-details-header">
                <h1>{device.name}</h1>
                <StatusDot status={device.isEnabled ? "green" : "gray"} size="large" />
            </div>

            <div className="device-info-grid">
                <div className="info-item">
                    <h3>Typ:</h3>
                    <p className="value">{device.deviceKind || 'actuator'}</p>
                </div>
                {!isSensorDevice ? (
                    <div className="info-item">
                        <h3>Parameter:</h3>
                        <p className="value">{device.targetParameter}</p>
                    </div>
                ) : null}
                <div className="info-item">
                    <h3>Czujniki:</h3>
                    <p className="value">{Array.isArray(device.sensorFields) && device.sensorFields.length > 0 ? device.sensorFields.join(', ') : 'brak'}</p>
                </div>
                <div className="info-item">
                    <h3>Telemetry ID:</h3>
                    <p className="value">{device.externalDeviceId || 'brak'}</p>
                </div>
                {!isSensorDevice ? (
                    <div className="info-item">
                        <h3>Effect Strength:</h3>
                        <p className="value">{device.effectStrength}</p>
                    </div>
                ) : null}
            </div>

            <div className="info-item">
                <h3>Przypisane rośliny</h3>
                {Array.isArray(device.plants) && device.plants.length > 0 ? (
                    <ul className="relation-list">
                        {device.plants.map((plant) => (
                            <li key={plant.id}>
                                <button type="button" className="link-like" onClick={() => navigate(`/plant/${plant.id}`)}>
                                    {plant.name}
                                </button>
                                <button type="button" onClick={() => handleUnassignPlant(plant.id)}>Odepnij</button>
                            </li>
                        ))}
                    </ul>
                ) : (
                    <p>Brak przypisanych roślin.</p>
                )}

                <div className="assign-row">
                    <select value={selectedPlantId} onChange={(event) => setSelectedPlantId(event.target.value)}>
                        <option value="">Wybierz roślinę</option>
                        {assignablePlants.map((plant) => (
                            <option key={plant.id} value={plant.id}>{plant.name} ({plant.species})</option>
                        ))}
                    </select>
                    <button type="button" onClick={handleAssignPlant}>Przypisz</button>
                </div>
                {assignMessage ? <p>{assignMessage}</p> : null}
            </div>
            {!isSensorDevice ? (
                <section className="info-item" aria-labelledby="manual-control-title">
                    <div>
                        <h3>Sterowanie ręczne</h3>
                    </div>
                    <p>Czas pracy</p>
                    <select id="manual-duration" value={manualDurationSeconds} onChange={(event) => setManualDurationSeconds(Number(event.target.value))} disabled={!device.isEnabled || isCommandPending}>
                        <option value={1}>1 sekunda</option>
                        <option value={2}>2 sekundy</option>
                        <option value={5}>5 sekund</option>
                        <option value={10}>10 sekund</option>
                        <option value={30}>30 sekund</option>
                    </select>
                    <div className="manual-control-actions">
                        <button type="button" onClick={handleManualPump} disabled={!device.isEnabled || isCommandPending}>Uruchom</button>
                        <button type="button" className="emergency-stop" onClick={handleEmergencyStop} disabled={isCommandPending}>STOP</button>
                    </div>
                    {manualStatus ? <p className="manual-status" role="status">{manualStatus}</p> : null}
                    {!device.isEnabled ? <p className="manual-safety-note">Sterowanie zablokowane, ponieważ urządzenie jest wyłączone.</p> : null}
                </section>
            ) : null}
            <div className="edit-device-container">
            <button className="edit-device-button" onClick={() => setIsEditing(true)}>Edit Device</button>
            </div>

            {isEditing && <DeviceEditWindow device={device} onClose={handleEditClose} />}
        </div>
    );
}

export default DeviceDetails;
