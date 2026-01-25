import './ParametersSeter.css';
import Slider from '../Slider/Slider';

const ParametersSeter = ({ plant, setPlant }) => {

    const handleTemperatureChange = (newValue) => {
        const newPlant = { ...plant };
        if (!newPlant.parameters) newPlant.parameters = {};
        if (!newPlant.parameters.temperature) newPlant.parameters.temperature = {};
        
        newPlant.parameters.temperature.min = newValue[0];
        newPlant.parameters.temperature.max = newValue[1];
        if (setPlant) {setPlant(newPlant);}
    };

    const handleHumidityChange = (newValue) => {
        const newPlant = { ...plant };
        if (!newPlant.parameters) newPlant.parameters = {};
        if (!newPlant.parameters.humidity) newPlant.parameters.humidity = {};

        newPlant.parameters.humidity.min = newValue[0];
        newPlant.parameters.humidity.max = newValue[1];

        if (setPlant) {setPlant(newPlant);}
    };

    const tempVal = [
        plant?.parameters?.temperature?.min ?? 0,
        plant?.parameters?.temperature?.max ?? 100
    ];

    const humidVal = [
        plant?.parameters?.humidity?.min ?? 0,
        plant?.parameters?.humidity?.max ?? 100
    ];

    return (
        <div className="parameters-seter">
            <h2>Ustaw Parametry</h2>
            <ul style={{ listStyle: 'none', padding: 0 }}>
                
                    <Slider 
                        text="Zakres temperatury (°C)" 
                        value={tempVal}
                        onChange={handleTemperatureChange}
                        min={0}
                        max={50}
                    />
                
                
                    <Slider 
                        text="Zakres wilgotności (%)" 
                        value={humidVal}
                        onChange={handleHumidityChange}
                        min={0}
                        max={100}
                    />
                
            </ul>
        </div>
    );
};

export default ParametersSeter;