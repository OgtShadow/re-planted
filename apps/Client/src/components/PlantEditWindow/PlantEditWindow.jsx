import './PlantEditWindow.css';
import connectionManager from '../../connectionManager';
import React, { useState } from 'react';

function PlantEditWindow({ plant, onClose }) {
  const [editedPlant, setEditedPlant] = useState({ ...plant });

  const handleSaveChanges = async (e) => {
    e.preventDefault();
    try {
          const result = await connectionManager.put(`/api/plants/${plant.id}`, editedPlant);
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
        const result = await connectionManager.delete(`/api/plants/${plant.id}`);
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