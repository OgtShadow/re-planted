import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import connectionManager, { API_BASE_URL, userPlantsEndpoint, userTelemetryRefreshEndpoint } from '../../connectionManager';
import { HubConnectionBuilder } from '@microsoft/signalr';
import StatusDot from '../StatusDot/StatusDot';
import PlantEditWindow from '../PlantEditWindow/PlantEditWindow';
import './PlantDetails.css';
import defaultPlantImage from '../../resources/plant.jpg';

function PlantDetails() {
    const { id } = useParams();
    const navigate = useNavigate();
    const [plant, setPlant] = useState(null);
    const [liveSnapshots, setLiveSnapshots] = useState([]);
    const [isEditing, setIsEditing] = useState(false);
    const [loading, setLoading] = useState(true);
    const plantImage = plant?.imageUrl || defaultPlantImage;

    useEffect(() => {
        const fetchPlant = async () => {
            try {
                const result = await connectionManager.get(userPlantsEndpoint(`/${id}`));
                setPlant(result);
            } catch (error) {
                console.error("Failed to fetch plant details:", error);
            } finally {
                setLoading(false);
            }
        };

        fetchPlant();
    }, [id]);

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
            connectionManager.get(userPlantsEndpoint(`/${id}`)).then(setPlant);
        }
    };

    useEffect(() => {
        const connection = new HubConnectionBuilder()
            .withUrl(`${API_BASE_URL}/telemetryHub`)
            .withAutomaticReconnect()
            .build();

        connection.on('TelemetryUpdated', (snapshots) => {
            if (!Array.isArray(snapshots)) {
                return;
            }

            setLiveSnapshots(snapshots);
        });

        connection.start().catch((error) => {
            console.error('PlantDetails telemetry SignalR connection failed:', error);
        });
        connectionManager.post(userTelemetryRefreshEndpoint()).then((snapshots) => {
            if (Array.isArray(snapshots)) {
                setLiveSnapshots(snapshots);
            }
        }).catch((error) => {
            console.error('PlantDetails telemetry refresh failed:', error);
        });

        return () => {
            connection.stop();
        };
    }, []);

    if (loading) return <div className="plant-details-container">Loading...</div>;
    if (!plant) return <div className="plant-details-container">Plant not found</div>;

    const sensorDevices = (plant.devices || []).filter((device) => (device.deviceKind || '').toLowerCase() === 'sensor');
    const sensorExternalIds = sensorDevices
        .map((device) => (device.externalDeviceId || '').trim().toLowerCase())
        .filter(Boolean);

    const plantLiveSnapshots = liveSnapshots.filter((snapshot) => {
        const snapshotDeviceId = (snapshot?.deviceId || snapshot?.DeviceId || '').trim().toLowerCase();
        if (!snapshotDeviceId) {
            return false;
        }

        return sensorExternalIds.some((externalId) => snapshotDeviceId === externalId || snapshotDeviceId.startsWith(`${externalId}-`));
    });

    return (
        <div className="plant-details-container">
            <button className="back-button" onClick={() => navigate("/") }>&larr; Back to List</button>

            <div className="plant-img">
                <img src={plantImage} alt={plant.name} />
            </div>
            <div className="plant-details-header">
                <h1>{plant.name}</h1>
            </div>
            <div className="plant-info-grid">
                <div className="info-item">
                    <h3 className="label">Planted Date:</h3>
                    <p className="value">{new Date(plant.plantedDate).toLocaleDateString()}</p>
                </div>
                <div className="info-item">
                    <h3 className="label">Last Watered:</h3>
                    <p className="value">{new Date(plant.lastWatered).toLocaleDateString()} {new Date(plant.lastWatered).toLocaleTimeString()}</p>
                </div>
                 <div className="info-item">
                    <h3 className="label">Health Status:</h3>
                    <p className="value">{plant.healthStatus}</p>
                </div>
            </div>
            <div className="info-item">
                    <h3 className="label">Species:</h3>
                    <p className="value">{plant.species}</p>
            </div>

                <div className="info-item">
                <h3>Przypisane sensory:</h3>
                {sensorDevices.length > 0 ? (
                    <ul className="relation-list">
                        {sensorDevices
                            .map((device) => (
                                <li key={device.id}>
                                    <button type="button" className="link-like" onClick={() => navigate(`/device/${device.id}`)}>
                                        {device.name}
                                    </button>
                                    <span>{device.externalDeviceId || 'brak telemetry id'}</span>
                                </li>
                            ))}
                    </ul>
                ) : (
                    <p>Brak przypisanych sensorów.</p>
                )}
                </div>
                <div className="info-item">
                <h3>Dane live dla tej rośliny:</h3>
                {plantLiveSnapshots.length > 0 ? (
                    <div className="plant-live-grid">
                        {plantLiveSnapshots.map((snapshot) => (
                            <div key={`${snapshot.deviceId || snapshot.DeviceId}-${snapshot.timestamp || snapshot.Timestamp}`} className="plant-live-card">
                                <strong>{snapshot.deviceId || snapshot.DeviceId}</strong>
                                <span>Gleba: {snapshot.soilMoistureAnalog ?? snapshot.SoilMoistureAnalog}</span>
                                <span>Temp: {snapshot.temperature ?? snapshot.Temperature}</span>
                                <span>Wilg: {snapshot.humidity ?? snapshot.Humidity}</span>
                                <span>Woda: {snapshot.waterLevelCm ?? snapshot.WaterLevelCm} cm</span>
                                <span>{new Date(snapshot.timestamp || snapshot.Timestamp || Date.now()).toLocaleTimeString()}</span>
                            </div>
                        ))}
                    </div>
                ) : (
                    <p>Brak bieżących odczytów dla przypisanych sensorów.</p>
                )}
                </div>
                <div className="info-item">
                <h3>Przypisane actuatory:</h3>
                {(plant.devices || []).filter((device) => (device.deviceKind || '').toLowerCase() !== 'sensor').length > 0 ? (
                    <ul className="relation-list">
                        {(plant.devices || [])
                            .filter((device) => (device.deviceKind || '').toLowerCase() !== 'sensor')
                            .map((device) => (
                                <li key={device.id}>
                                    <button type="button" onClick={() => navigate(`/device/${device.id}`)}>
                                        {device.name}
                                    </button>
                                    <span>{device.targetParameter || 'brak celu'}</span>
                                </li>
                            ))}
                    </ul>
                ) : (
                    <p>Brak przypisanych actuatorów.</p>
                )}
                </div>
            

            <button className="edit-button" onClick={() => setIsEditing(true)}>Edit Plant</button>

            {isEditing && <PlantEditWindow plant={plant} onClose={handleEditClose} />}
        </div>
    );
}

export default PlantDetails;
