import './PlantParametersSeter.css';
import Slider2 from '../Sliders/Slider2';
import Slider1 from '../Sliders/Slider1';

const PlantParametersSeter = ({ plant, setPlant }) => {

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

    const handleLightHoursChange = (newValue) => {
        const newPlant = { ...plant };
        if (!newPlant.parameters) newPlant.parameters = {};
        if (!newPlant.parameters.lightHoursPerDay) newPlant.parameters.lightHoursPerDay = {};

        newPlant.parameters.lightHoursPerDay = newValue;
        if (setPlant) {setPlant(newPlant);}
    }

    const handleWateringFrequencyChange = (newValue) => {
        const newPlant = { ...plant };
        if (!newPlant.parameters) newPlant.parameters = {};
        if (!newPlant.parameters.wateringIntervalDays) newPlant.parameters.wateringIntervalDays = {};

        newPlant.parameters.wateringIntervalDays = newValue;
        if (setPlant) {setPlant(newPlant);}
    }

    const handleLightScheduleChange = (field) => (event) => {
        const inputValue = event.target.value;
        const newPlant = { ...plant };
        if (!newPlant.parameters) newPlant.parameters = {};

        newPlant.parameters[field] = inputValue ? `${inputValue}:00` : null;
        if (setPlant) {setPlant(newPlant);}
    };

    const handleClearLightSchedule = () => {
        const newPlant = { ...plant };
        if (!newPlant.parameters) newPlant.parameters = {};

        newPlant.parameters.lightScheduleStart = null;
        newPlant.parameters.lightScheduleEnd = null;
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

    const lightVal = plant?.parameters?.lightHoursPerDay ?? 12;

    const wateringVal = plant?.parameters?.wateringIntervalDays ?? 7;

    const toTimeInputValue = (timeSpan) => (timeSpan ? timeSpan.slice(0, 5) : '');
    const lightScheduleStart = toTimeInputValue(plant?.parameters?.lightScheduleStart);
    const lightScheduleEnd = toTimeInputValue(plant?.parameters?.lightScheduleEnd);

    
    return (
        <div className="parameters-seter">
            <h2>Ustaw Parametry</h2>
            <ul style={{ listStyle: 'none', padding: 0 }}>
                
                <Slider2
                    text="Zakres temperatur (°C)"
                    value={tempVal}
                    onChange={handleTemperatureChange}
                    min={0}
                    max={40}
                />
                
                <Slider2
                    text="Zakres wilgotności (%)" 
                    value={humidVal}
                    onChange={handleHumidityChange}
                    min={0}
                    max={100}
                />
                
                <Slider1
                    text = "ilość godzin światła dziennego"
                    value={lightVal}
                    onChange={handleLightHoursChange}
                    min={0}
                    max={24}
                />

                <div className="light-schedule">
                    <p>Dozwolone godziny włączania światła (aby nie przeszkadzać np. we śnie)</p>
                    <div className="light-schedule-inputs">
                        <label>
                            Od
                            <input
                                type="time"
                                value={lightScheduleStart}
                                onChange={handleLightScheduleChange('lightScheduleStart')}
                            />
                        </label>
                        <label>
                            Do
                            <input
                                type="time"
                                value={lightScheduleEnd}
                                onChange={handleLightScheduleChange('lightScheduleEnd')}
                            />
                        </label>
                        <button type="button" onClick={handleClearLightSchedule}>Bez ograniczenia</button>
                    </div>
                </div>

                <Slider1
                    text = "Częstotliwość podlewania (dni)"
                    value={wateringVal}
                    onChange={handleWateringFrequencyChange}
                    min={1}
                    max={30}
                />
            </ul>
        </div>
    );
};

export default PlantParametersSeter;