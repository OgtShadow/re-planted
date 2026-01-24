import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import connectionManager from '../../connectionManager';
import StatusDot from '../StatusDot/StatusDot';
import PlantEditWindow from '../PlantEditWindow/PlantEditWindow';
import './PlantDetails.css';

function PlantDetails() {
    const { id } = useParams();
    const navigate = useNavigate();
    const [plant, setPlant] = useState(null);
    const [isEditing, setIsEditing] = useState(false);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const fetchPlant = async () => {
            try {
                const result = await connectionManager.get(`/api/plants/${id}`);
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
                 if (parsed && typeof parsed === 'object' && parsed.Response && parsed.Response.includes("Usunięto")) {
                     navigate("/");
                     return;
                 }
            } catch (error) { 
                console.error("Failed to parse response:", error);
            }
            connectionManager.get(`/api/plants/${id}`).then(setPlant);
        }
    };

    if (loading) return <div className="plant-details-container">Loading...</div>;
    if (!plant) return <div className="plant-details-container">Plant not found</div>;

    return (
        <div className="plant-details-container">
            <button className="back-button" onClick={() => navigate("/")}>&larr; Back to List</button>
            
            <div className="plant-details-header">
                <h1>{plant.name}</h1>
                <StatusDot status={plant.healthStatus === "Healthy" ? "green" : plant.healthStatus === "Unhealthy" ? "red" : "gray"} size="large" />
            </div>

            <div className="plant-info-grid">
                <div className="info-item">
                    <span className="label">Species:</span>
                    <span className="value">{plant.species}</span>
                </div>
                <div className="info-item">
                    <span className="label">Planted Date:</span>
                    <span className="value">{new Date(plant.plantedDate).toLocaleDateString()}</span>
                </div>
                <div className="info-item">
                    <span className="label">Last Watered:</span>
                    <span className="value">{new Date(plant.lastWatered).toLocaleDateString()} {new Date(plant.lastWatered).toLocaleTimeString()}</span>
                </div>
                 <div className="info-item">
                    <span className="label">Health Status:</span>
                    <span className="value">{plant.healthStatus}</span>
                </div>
            </div>

            <button className="edit-button" onClick={() => setIsEditing(true)}>Edit Plant</button>

            {isEditing && <PlantEditWindow plant={plant} onClose={handleEditClose} />}
        </div>
    );
}

export default PlantDetails;
