import { useState, useEffect } from "react";
import PlantProfile from "../PlantProfile/PlantProfile";
import connectionManager from "../../connectionManager";
import './PlantList.css'

function PlantList() {
    const [plants, setPlants] = useState([]);

    useEffect(() => {
      const fetchPlants = async () => {
        try {
          const result = await connectionManager.get("/api/plants");
          setPlants(result);
        } catch (error) {
          console.error("Failed to fetch plants:", error);
        }
      };
    fetchPlants();
  }, []);

return (
    <div className="plant-list">
        {plants.map((plant) => (
            <PlantProfile plant={plant} />
        ))}
    </div>
  );
}

export default PlantList;