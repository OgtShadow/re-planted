import { useState } from "react";
import connectionManager, { userPlantsEndpoint } from "../../connectionManager";
import "./PlantAdd.css";

function PlantAdd() {
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
<div className="plant-details-container">
  <h1> Create Plant</h1>
  <div className="plant-info-grid">
    <div className="info-item">
      <input
        type="text"
        placeholder="Nazwa rośliny"
        value={name}
        onChange={(e) => setName(e.target.value)}
      />
    </div>
    <div className="info-item">
      <input
        type="text"
        placeholder="Gatunek rośliny"
        value={species}
        onChange={(e) => setSpecies(e.target.value)}
      />
    </div>
  </div>
  <div className="add-actions">
    <button className="add-button" onClick={handleCreatePlant}>
      Dodaj roślinę
    </button>
    <a className="secondary-button" href="/device/add">
      Dodaj urządzenie
    </a>
  </div>
  <p>{response}</p>
</div>      
  );
}

export default PlantAdd;