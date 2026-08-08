import { useState, useEffect } from 'react';
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

    return (
        <div className="device-details-container">
            <button className="back-button" onClick={() => navigate("/")}>&larr; Back to List</button>
            
            <div className="device-details-header">
                <h1>{device.name}</h1>
                <StatusDot status={device.isEnabled ? "green" : "gray"} size="large" />
            </div>

            <div className="device-info-grid">
                <div className="info-item">
                    <span className="label">Typ:</span>
                    <span className="value">{device.deviceKind || 'actuator'}</span>
                </div>
                <div className="info-item">
                    <span className="label">Parameter:</span>
                    <span className="value">{device.targetParameter}</span>
                </div>
                <div className="info-item">
                    <span className="label">Czujniki:</span>
                    <span className="value">{Array.isArray(device.sensorFields) && device.sensorFields.length > 0 ? device.sensorFields.join(', ') : 'brak'}</span>
                </div>
                <div className="info-item">
                    <span className="label">Telemetry ID:</span>
                    <span className="value">{device.externalDeviceId || 'brak'}</span>
                </div>
                <div className="info-item">
                    <span className="label">Effect Strength:</span>
                    <span className="value">{device.effectStrength}</span>
                </div>
            </div>

            <div className="relation-box">
                <h3>Przypisane rośliny</h3>
                {Array.isArray(device.plants) && device.plants.length > 0 ? (
                    <ul className="relation-list">
                        {device.plants.map((plant) => (
                            <li key={plant.id}>
                                <button type="button" className="link-like" onClick={() => navigate(`/plant/${plant.id}`)}>
                                    {plant.name} ({plant.species})
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

            <button className="edit-button" onClick={() => setIsEditing(true)}>Edit Device</button>

            {isEditing && <DeviceEditWindow device={device} onClose={handleEditClose} />}
        </div>
    );
}

export default DeviceDetails;
