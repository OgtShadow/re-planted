import { useNavigate } from 'react-router-dom';
import './PlantProfile.css';
import StatusDot from '../StatusDot/StatusDot';

function PlantProfile({plant}) {
    const navigate = useNavigate();

return (
    <div className="plant-profile" onClick={() => navigate(`/plant/${plant.id}`)} style={{ cursor: 'pointer' }}>
      <div className="plant-profile-header">
        <h2>{plant.name}</h2>
        {plant.healthStatus === "Healthy" ? <StatusDot status="green" size="small" /> : plant.healthStatus === "Unhealthy" ? <StatusDot status="red" size="small" /> : <StatusDot status="gray" size="small" />} 
      </div>
        <p>Specie: {plant.species}</p>
    </div>
  );
}

export default PlantProfile;        