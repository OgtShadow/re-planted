import { useState } from "react";
import connectionManager, { userPlantsEndpoint } from "../../connectionManager";
import "./PlantCreator.css";

function PlantCreator() {
  const [name, setName] = useState("");
  const [species, setSpecies] = useState("");
  const [response, setResponse] = useState("");

    const handleCreatePlant = async () => {
    try {
      const result = await connectionManager.post(userPlantsEndpoint(), {
        name,
        species
      });
      setResponse(JSON.stringify(result));
    } catch (error) {
      setResponse("Error: " + error.message);
      console.error("Failed to create plant:", error);
    }
  };

  return (
    <div className="plant-creator-container">
      <div className="plant-creator-form">
        <input
          type="text"
          placeholder="Nazwa rośliny"
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
        <input
          type="text"
          placeholder="Gatunek rośliny"
          value={species}
          onChange={(e) => setSpecies(e.target.value)}
        />
        <button className="button-primary" onClick={handleCreatePlant}>Dodaj roślinę</button>
        <p className="plant-creator-response">{response}</p>
      </div>
    </div>
  );
}

export default PlantCreator;