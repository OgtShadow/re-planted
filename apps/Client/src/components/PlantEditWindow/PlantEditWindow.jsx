import './PlantEditWindow.css';
import connectionManager, { userPlantsEndpoint } from '../../connectionManager';
import React, { useState } from 'react';
import { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import PlantParametersSeter from '../PlantParametersSeter/PlantParametersSeter';
import defaultPlantImage from '../../resources/plant.jpg';

function PlantEditWindow() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [plant, setPlant] = useState(null);
  const [editedPlant, setEditedPlant] = useState({ ...plant });
  const [isLoading, setIsLoading] = useState(true);
  const [showPreview, setShowPreview] = useState(false);

  useEffect(() => {
    connectionManager.get(userPlantsEndpoint(`/${id}`))
      .then((result) => {
        setPlant(result);
        setEditedPlant(result);
      })
      .catch((error) => console.error('Failed to fetch plant for editing:', error))
      .finally(() => setIsLoading(false));
  }, [id]);

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
          await connectionManager.put(userPlantsEndpoint(`/${plant.id}`), editedPlant);
          navigate(`/plant/${plant.id}`);
        } catch (error) {
          const errorMessage = "Error: " + error.message;
          console.error("Failed to edit plant:", error);
        }
  }

  const handleDeletePlant = async () => {
    try {
        await connectionManager.delete(userPlantsEndpoint(`/${plant.id}`));
        navigate('/');
    } catch (error) {
        console.error("Failed to delete plant:", error);
    }
  }

  if (isLoading) return <main className="plant-edit-page">Ładowanie...</main>;
  if (!plant) return <main className="plant-edit-page">Nie znaleziono rośliny.</main>;

  return (
    <div className="plant-edit-page">
      <button className="button-secondary plant-edit-back" type="button" onClick={() => navigate(`/plant/${plant.id}`)}>&larr; Back to Plant</button>
      <div className="plant-edit-window">
        <form onSubmit={handleSaveChanges}>
          <div className="plant-info-item">
            <div className="plant-info-item-grid">
            <label>
              Name:
              <input
                type="text"
                value={editedPlant.name}
                onChange={(e) => setEditedPlant({ ...editedPlant, name: e.target.value })}
              />
            </label>
            <label>
              Specie:
              <input
                type="text"
                value={editedPlant.species}
                onChange={(e) => setEditedPlant({ ...editedPlant, species: e.target.value })}
              />
            </label>
            </div>
            <label>
              Zdjęcie rośliny:
              <input
                type="file"
                accept="image/*"
                onChange={handleImageChange}
              />
            </label>

            <button
              className="button-secondary plant-preview-toggle"
              type="button"
              onClick={() => setShowPreview((current) => !current)}
            >
              {showPreview ? 'Ukryj podgląd zdjęcia' : 'Pokaż podgląd zdjęcia'}
            </button>

            {showPreview ? (
              <div className="plant-img-edit-preview">
                <img src={editedPlant.imageUrl || defaultPlantImage} alt={editedPlant.name || 'Podgląd rośliny'} />
              </div>
            ) : null}

                </div>

                <div className="plant-info-item">

                <PlantParametersSeter plant={editedPlant} setPlant={setEditedPlant} />
                </div>
                <div className="button-group">
                  <button className="button-secondary submit-button" type="submit">Save Changes</button>
                  <button className="button-secondary delete" type="button" onClick={handleDeletePlant}>Delete Plant</button>
                </div>
            </form>
        </div>
        </div>
  );
}
export default PlantEditWindow;