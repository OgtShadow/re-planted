import { useState } from 'react';
import './PlantProfile.css';
import PlantEditWindow from '../PlantEditWindow/PlantEditWindow';
import StatusDot from '../StatusDot/StatusDot';

function PlantProfile({plant}) {
    const [isEditing, setIsEditing] = useState(false);
    const [response, setResponse] = useState("");

return (
    <div className="plant-profile">
      <div className="plant-profile-header">
        <h2>{plant.name}</h2>
        {plant.healthStatus === "Healthy" ? <StatusDot status="green" size="small" /> : plant.healthStatus === "Unhealthy" ? <StatusDot status="red" size="small" /> : <StatusDot status="gray" size="small" />} 
      </div>
        <p>Specie: {plant.species}</p>
        <button onClick={() => setIsEditing(true)}>Edit Plant</button>
        {isEditing && <PlantEditWindow plant={plant} onClose={(response) => { setIsEditing(false); setResponse(response); }} />}
          {console.log(response)}
        <p>{response}</p>
    </div>
  );
}

export default PlantProfile;        