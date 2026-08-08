import { useState, useEffect } from "react";
import DeviceProfile from "../DeviceProfile/DeviceProfile";
import connectionManager, { API_BASE_URL, userDevicesEndpoint } from "../../connectionManager";
import { HubConnectionBuilder } from "@microsoft/signalr";
import './DeviceList.css'

function DeviceList() {
    const [devices, setDevices] = useState([]);

    useEffect(() => {
      const fetchDevices = async () => {
        try {
          const result = await connectionManager.get(userDevicesEndpoint());
          if (result) {
             setDevices(result);
          }
        } catch (error) {
          console.error("Failed to fetch devices:", error);
        }
      };
      fetchDevices();

      const connection = new HubConnectionBuilder()
          .withUrl(`${API_BASE_URL}/userHub`)
          .withAutomaticReconnect()
          .build();

      connection.start()
          .then(() => {
              console.log("Connected to SignalR");
              connection.on("DevicesUpdated", () => {
                  console.log("SignalR: Devices updated, fetching new data...");
                  fetchDevices();
              });
          })
          .catch(e => console.error("Connection failed: ", e));

      return () => {
          connection.stop();
      };
  }, []);

return (
    <div className="device-list">
        {devices.map((device) => (
            <DeviceProfile key={device.id} device={device} />
        ))}
    </div>
  );
}

export default DeviceList;