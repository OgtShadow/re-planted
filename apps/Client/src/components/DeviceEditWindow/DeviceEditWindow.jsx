import './DeviceEditWindow.css';
import connectionManager, { userDevicesEndpoint } from '../../connectionManager';
import React, { useState } from 'react';
import ParametersSeter from '../ParametersSeter/ParametersSeter';

function DeviceEditWindow({ device, onClose }) {
  const [editedDevice, setEditedDevice] = useState({ ...device });

  const handleSaveChanges = async (e) => {
    e.preventDefault();
    try {
          const result = await connectionManager.put(userDevicesEndpoint(`/${device.id}`), editedDevice);
          const resultMessage = JSON.stringify(result);
          if (onClose) {
            onClose(resultMessage);
          }
        } catch (error) {
          const errorMessage = "Error: " + error.message;
          console.error("Failed to edit device:", error);
           if (onClose) {
            onClose(errorMessage);
           }
        }
  }

  const handleDeleteDevice = async () => {
    try {
        const result = await connectionManager.delete(userDevicesEndpoint(`/${device.id}`));
        const resultMessage = JSON.stringify(result);
        if (onClose) {
            onClose(resultMessage);
        }
    } catch (error) {
        const errorMessage = "Error: " + error.message;
        console.error("Failed to delete device:", error);
        if (onClose) {
            onClose(errorMessage);
        }
    }
  }

  return (
    <div className="device-edit-overlay" onClick={() => onClose && onClose()}>
        <div className="device-edit-window" onClick={(e) => e.stopPropagation()}>
          <h2>Edit Device Details</h2>
            <form onSubmit={handleSaveChanges}>
                <label>
                    Name:
                    <input
                        type="text"
                        value={editedDevice.name}
                        onChange={(e) => setEditedDevice({ ...editedDevice, name: e.target.value })}
                    />
                </label>
                <label>
                    Target Parameter:
                    <input
                        type="text"
                        value={editedDevice.targetParameter}
                        onChange={(e) => setEditedDevice({ ...editedDevice, targetParameter: e.target.value })}
                    />
                </label>

                <ParametersSeter device={editedDevice} setDevice={setEditedDevice} />
                
                <div className="button-group">
                    <button type="submit">Save Changes</button>
                    <button type="button" className="delete" onClick={handleDeleteDevice}>Delete Device</button>
                </div>
            </form>
        </div>
    </div>
  );
}
export default DeviceEditWindow;