import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import DeviceEditWindow from '../DeviceEditWindow/DeviceEditWindow';
import connectionManager, { userDevicesEndpoint } from '../../connectionManager';
import StatusDot from '../StatusDot/StatusDot';
import './DeviceDetails.css';

function DeviceDetails() {
    const { id } = useParams();
    const navigate = useNavigate();
    const [device, setDevice] = useState(null);
    const [isEditing, setIsEditing] = useState(false);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const fetchDevice = async () => {
            try {
                const result = await connectionManager.get(userDevicesEndpoint(`/${id}`));
                setDevice(result);
            } catch (error) {
                console.error("Failed to fetch device details:", error);
            } finally {
                setLoading(false);
            }
        };

        fetchDevice();
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
            connectionManager.get(userDevicesEndpoint(`/${id}`)).then(setDevice);
        }
    };

    if (loading) return <div className="device-details-container">Loading...</div>;
    if (!device) return <div className="device-details-container">Device not found</div>;

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
            <button className="edit-button" onClick={() => setIsEditing(true)}>Edit Device</button>

            {isEditing && <DeviceEditWindow device={device} onClose={handleEditClose} />}
        </div>
    );
}

export default DeviceDetails;
