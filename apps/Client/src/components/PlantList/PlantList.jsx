import { useState, useEffect } from "react";
import PlantProfile from "../PlantProfile/PlantProfile";
import AddButton from "../AddButton/AddButton";
import connectionManager, { API_BASE_URL, userPlantsEndpoint } from "../../connectionManager";
import { HubConnectionBuilder } from "@microsoft/signalr";
import './PlantList.css'

function PlantList() {
    const [plants, setPlants] = useState([]);

    useEffect(() => {
      const fetchPlants = async () => {
        try {
          const result = await connectionManager.get(userPlantsEndpoint());
          // React sam sprawdzi czy dane są inne, ale warto upewnić się, że endpoint zwraca zawsze tablicę
          if (result) {
             setPlants(result);
          }
        } catch (error) {
          console.error("Failed to fetch plants:", error);
        }
      };

      // 1. Pobierz dane od razu po wejściu na stronę
      fetchPlants();

      // 2. Skonfiguruj SignalR
      const connection = new HubConnectionBuilder()
          .withUrl(`${API_BASE_URL}/plantHub`)
          .withAutomaticReconnect()
          .build();

      connection.start()
          .then(() => {
              console.log("Connected to SignalR");
              
              // Nasłuchuj zdarzenia "PlantsUpdated"
              connection.on("PlantsUpdated", () => {
                  console.log("SignalR: Plants updated, fetching new data...");
                  fetchPlants();
              });
          })
          .catch(e => console.error("Connection failed: ", e));

      // 3. Sprzątanie
      return () => {
          connection.stop();
      };
  }, []); // Pusta tablica zależności [] oznacza, że efekt uruchomi się przy montowaniu

return (
    <div className="plant-list">
        {plants.map((plant) => (
            <PlantProfile key={plant.id} plant={plant} />
        ))}
        <div className= "plant-add-button">
            <AddButton link="/plant/add"/>
        </div>
    </div>
  );
}

export default PlantList;