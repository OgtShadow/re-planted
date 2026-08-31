import { useState } from "react";
import connectionManager, { userPlantsEndpoint } from "../../connectionManager";
import "./PlantAdd.css";
import defaultPlantImage from '../../resources/plant.jpg';

function PlantAdd() {
  const [name, setName] = useState("");
  const [species, setSpecies] = useState("");
  const [imageUrl, setImageUrl] = useState("");
  const [response, setResponse] = useState("");

  const handleImageChange = (event) => {
    const file = event.target.files?.[0];

    if (!file) {
      setImageUrl("");
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      setImageUrl(typeof reader.result === "string" ? reader.result : "");
    };
    reader.readAsDataURL(file);
  };

    const handleCreatePlant = async () => {
    try {
      const result = await connectionManager.post(userPlantsEndpoint(), {
        name,
        species,
        imageUrl
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
    <div className="plant-info-section">
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
    <div className="info-item">
      <label htmlFor="plant-image">Zdjęcie rośliny</label>
      <input
        id="plant-image"
        type="file"
        accept="image/*"
        onChange={handleImageChange}
      />
      <div className="plant-image-preview">
        <img src={imageUrl || defaultPlantImage} alt="Podgląd rośliny" />
      </div>
    </div>
  </div>
  <div className="add-actions">
    <button className="add-plant-button" onClick={handleCreatePlant}>
      Dodaj roślinę
    </button>
  </div>
  <p>{response}</p>
</div>      
  );
}

export default PlantAdd;