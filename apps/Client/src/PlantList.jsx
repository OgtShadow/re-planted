import { useState } from "react";
import connectionManager from "./connectionManager";

function PlantList() {
    const [plants, setPlants] = useState([]);

    const fetchPlants = async () => {
    try {
      const result = await connectionManager.get("/api/plants");
      setPlants(result);
    } catch (error) {
      console.error("Failed to fetch plants:", error);
    }
  };

return (
    <div>
        <button onClick={fetchPlants}>Pobierz listę roślin</button>
        <ul>
          {plants.map((plant, index) => (
            <li key={index}>{plant.name} - {plant.species} - {plant.healthStatus}</li>
          ))}
        </ul>
    </div>
  );
}

export default PlantList;