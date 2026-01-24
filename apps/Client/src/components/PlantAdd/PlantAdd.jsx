import { useState } from "react";
import connectionManager from "../../connectionManager";

function PlantAdd() {
  const [name, setName] = useState("");
  const [species, setSpecies] = useState("");
  const [response, setResponse] = useState("");

    const handleCreatePlant = async () => {
    try {
      const result = await connectionManager.post("/api/plants", {
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
    <div>
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
      <button onClick={handleCreatePlant}>Dodaj roślinę</button>
      <p>{response}</p>
    </div>
  );
}

export default PlantAdd;