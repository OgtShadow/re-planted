import { useEffect, useMemo, useState } from "react";
import connectionManager, { userDevicesEndpoint, userPlantsEndpoint } from "../../connectionManager";
import "./DeviceAdd.css";

function DeviceAdd() {
  const [name, setName] = useState("");
  const [targetParameter, setTargetParameter] = useState("soilMoisture");
  const [effectType, setEffectType] = useState("increase");
  const [effectStrength, setEffectStrength] = useState(1);
  const [isEnabled, setIsEnabled] = useState(true);
  const [response, setResponse] = useState("");

  const [plants, setPlants] = useState([]);
  const [selectedPlantIds, setSelectedPlantIds] = useState([]);
  const [catalog, setCatalog] = useState({
    targetParameters: [
      { key: "soilMoisture", sensorField: "soilMoistureAnalog", defaultCommand: "pump", defaultCommandPath: "/command/pump", defaultStateField: "pumpState", suggestedEffectType: "increase" },
      { key: "light", sensorField: "lightIsDark", defaultCommand: "light", defaultCommandPath: "/command/light", defaultStateField: "lampState", suggestedEffectType: "set" },
      { key: "temperature", sensorField: "temperature", defaultCommand: "light", defaultCommandPath: "/command/light", defaultStateField: "lampState", suggestedEffectType: "set" },
      { key: "humidity", sensorField: "humidity", defaultCommand: "pump", defaultCommandPath: "/command/pump", defaultStateField: "pumpState", suggestedEffectType: "increase" },
      { key: "waterLevel", sensorField: "waterLevelCm", defaultCommand: "pump", defaultCommandPath: "/command/pump", defaultStateField: "pumpState", suggestedEffectType: "increase" },
    ],
    supportedEffectTypes: ["increase", "decrease", "set"],
  });

  useEffect(() => {
    const loadData = async () => {
      try {
        const [catalogResult, plantsResult] = await Promise.all([
          connectionManager.get(userDevicesEndpoint("/catalog")),
          connectionManager.get(userPlantsEndpoint()),
        ]);

        if (catalogResult) {
          setCatalog(catalogResult);
          const firstTarget = catalogResult.targetParameters?.[0];
          if (firstTarget?.key) {
            setTargetParameter(firstTarget.key);
            setEffectType(firstTarget.suggestedEffectType || effectType);
          }
        }

        if (Array.isArray(plantsResult)) {
          setPlants(plantsResult);
        }
      } catch (error) {
        setResponse("Error loading form data: " + error.message);
      }
    };

    loadData();
  }, []);

  const targetOptions = useMemo(() => catalog.targetParameters || [], [catalog.targetParameters]);
  const selectedTarget = useMemo(
    () => targetOptions.find((target) => target.key === targetParameter),
    [targetOptions, targetParameter]
  );
  const handleTargetParameterChange = (value) => {
    setTargetParameter(value);

    const target = targetOptions.find((item) => item.key === value);
    if (!target) {
      return;
    }

    if (target.suggestedEffectType) {
      setEffectType(target.suggestedEffectType);
    }
  };

  const togglePlantSelection = (plantId) => {
    setSelectedPlantIds((prev) => {
      if (prev.includes(plantId)) {
        return prev.filter((id) => id !== plantId);
      }

      return [...prev, plantId];
    });
  };

  const assignDeviceToSelectedPlants = async (deviceId) => {
    for (const plantId of selectedPlantIds) {
      await connectionManager.put(userDevicesEndpoint(`/${deviceId}/plants/${plantId}`), {});
    }
  };

  const handleCreateDevice = async () => {
    try {
      const result = await connectionManager.post(userDevicesEndpoint(), {
        name,
        targetParameter,
        effectType,
        effectStrength: Number(effectStrength),
        isEnabled,
      });

      if (result?.id && selectedPlantIds.length > 0) {
        await assignDeviceToSelectedPlants(result.id);
      }

      setResponse(`Dodano urządzenie: ${result?.id || "brak id"}`);
      setName("");
      setSelectedPlantIds([]);
    } catch (error) {
      setResponse("Error: " + error.message);
      console.error("Failed to create device:", error);
    }
  };

  return (
    <div className="device-add-container">
      <h1>Create Device</h1>
      <div className="device-info-grid">
        <div className="info-item">
          <label>Nazwa urządzenia</label>
          <input
            type="text"
            placeholder="np. Room Light"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </div>

        <div className="info-item">
          <label>Target parameter</label>
          <select value={targetParameter} onChange={(e) => handleTargetParameterChange(e.target.value)}>
            {targetOptions?.map((parameter) => (
              <option key={parameter.key} value={parameter.key}>
                {parameter.key} ({parameter.sensorField})
              </option>
            ))}
          </select>
        </div>

        <div className="info-item">
          <label>Domyślny handler</label>
          <input
            type="text"
            value={selectedTarget ? `${selectedTarget.defaultCommand} (${selectedTarget.defaultCommandPath})` : "pump (/command/pump)"}
            readOnly
          />
        </div>

        <div className="info-item">
          <label>Pole stanu z sensora</label>
          <select disabled value={selectedTarget?.defaultStateField || "pumpState"}>
            {(selectedTarget?.defaultStateField ? [selectedTarget.defaultStateField] : ["pumpState"]).map((parameter) => (
              <option key={parameter} value={parameter}>
                {parameter}
              </option>
            ))}
          </select>
        </div>

        <div className="info-item">
          <label>Effect type</label>
          <select value={effectType} onChange={(e) => setEffectType(e.target.value)}>
            {(catalog.supportedEffectTypes || ["increase", "decrease", "set"]).map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </div>

        <div className="info-item">
          <label>Effect strength</label>
          <input
            type="number"
            min="0"
            step="0.1"
            value={effectStrength}
            onChange={(e) => setEffectStrength(e.target.value)}
          />
        </div>

        <div className="info-item checkbox-item">
          <label>
            <input
              type="checkbox"
              checked={isEnabled}
              onChange={(e) => setIsEnabled(e.target.checked)}
            />
            Enabled
          </label>
        </div>
      </div>

      <h2>Assign to plants</h2>
      <div className="plants-assign-grid">
        {plants.length === 0 && <p>Brak roślin do przypięcia.</p>}
        {plants.map((plant) => (
          <label key={plant.id} className="plant-checkbox">
            <input
              type="checkbox"
              checked={selectedPlantIds.includes(plant.id)}
              onChange={() => togglePlantSelection(plant.id)}
            />
            <span>{plant.name} ({plant.species})</span>
          </label>
        ))}
      </div>

      <button className="add-button" onClick={handleCreateDevice}>
        Dodaj urządzenie
      </button>
      <p>{response}</p>
    </div>
  );
}

export default DeviceAdd;
