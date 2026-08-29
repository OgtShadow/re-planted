import './PlantEditWindow.css';
import connectionManager, { userPlantsEndpoint } from '../../connectionManager';
import React, { useState } from 'react';
import PlantParametersSeter from '../PlantParametersSeter/PlantParametersSeter';
import defaultPlantImage from '../../resources/plant.jpg';

function PlantEditWindow({ plant, onClose }) {
  const [editedPlant, setEditedPlant] = useState({ ...plant });

  const handleImageChange = (event) => {
    const file = event.target.files?.[0];

    if (!file) {
      setEditedPlant((currentPlant) => ({ ...currentPlant, imageUrl: '' }));
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      setEditedPlant((currentPlant) => ({
        ...currentPlant,
        imageUrl: typeof reader.result === 'string' ? reader.result : ''
      }));
    };
    reader.readAsDataURL(file);
  };

  const handleSaveChanges = async (e) => {
    e.preventDefault();
    try {
          const result = await connectionManager.put(userPlantsEndpoint(`/${plant.id}`), editedPlant);
          const resultMessage = JSON.stringify(result);
          if (onClose) {
            onClose(resultMessage);
          }
        } catch (error) {
          const errorMessage = "Error: " + error.message;
          console.error("Failed to edit plant:", error);
           if (onClose) {
            onClose(errorMessage);
           }
        }
  }

  const handleDeletePlant = async () => {
    try {
        const result = await connectionManager.delete(userPlantsEndpoint(`/${plant.id}`));
        const resultMessage = JSON.stringify(result);
        if (onClose) {
            onClose(resultMessage);
        }
    } catch (error) {
        const errorMessage = "Error: " + error.message;
        console.error("Failed to delete plant:", error);
        if (onClose) {
            onClose(errorMessage);
        }
    }
  }

  return (
    <div className="plant-edit-overlay" onClick={() => onClose && onClose()}>
        <div className="plant-edit-window" onClick={(e) => e.stopPropagation()}>
          <h2>Edit Plant Details</h2>
            <form onSubmit={handleSaveChanges}>
                <label>
                    Name:
                    <input
                        type="text"
                        value={editedPlant.name}
                        onChange={(e) => setEditedPlant({ ...editedPlant, name: e.target.value })}
                    />
                </label>
                <label>
                    Species:
                    <input
                        type="text"
                        value={editedPlant.species}
                        onChange={(e) => setEditedPlant({ ...editedPlant, species: e.target.value })}
                    />
                </label>

                <label>
                  Zdjęcie rośliny:
                  <input
                    type="file"
                    accept="image/*"
                    onChange={handleImageChange}
                  />
                </label>

                <div className="plant-edit-preview">
                  <img src={editedPlant.imageUrl || defaultPlantImage} alt={editedPlant.name || 'Podgląd rośliny'} />
                </div>

                <PlantParametersSeter plant={editedPlant} setPlant={setEditedPlant} />
                
                <div className="button-group">
                    <button type="submit">Save Changes</button>
                    <button type="button" className="delete" onClick={handleDeletePlant}>Delete Plant</button>
                </div>
            </form>
        </div>
    </div>
  );
}
export default PlantEditWindow;