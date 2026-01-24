import { useState } from 'react';
import './PlantProfile.css';
import PlantEditWindow from '../PlantEditWindow/PlantEditWindow';

function PlantProfile({plant}) {
    const [isEditing, setIsEditing] = useState(false);
    const [response, setResponse] = useState("");

return (
    <div className="plant-profile">
        <h2>{plant.name}</h2>
        <p>Species: {plant.species}</p>
        <p>Health Status: {plant.healthStatus}</p>
        <button onClick={() => setIsEditing(true)}>Edit Plant</button>
        {isEditing && <PlantEditWindow plant={plant} onClose={(response) => { setIsEditing(false); setResponse(response); }} />}
          {console.log(response)}
        <p>{response}</p>
    </div>
  );
}

export default PlantProfile;        