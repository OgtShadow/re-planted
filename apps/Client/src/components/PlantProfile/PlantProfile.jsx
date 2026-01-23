import './PlantProfile.css'

function PlantProfile({plant}) {

return (
    <div className="plant-profile">
        <h2>{plant.name}</h2>
        <p>Species: {plant.species}</p>
        <p>Health Status: {plant.healthStatus}</p>
        <button>Edit Plant</button>
    </div>
  );
}

export default PlantProfile;        