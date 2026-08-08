import './DeviceEditWindow.css';
import connectionManager, { userDevicesEndpoint } from '../../connectionManager';
import React, { useState } from 'react';
import DeviceParametersSeter from '../DeviceParametersSeter/DeviceParametersSeter';

function DeviceEditWindow({ device, onClose }) {
  const [editedDevice, setEditedDevice] = useState({ ...device });

  const handleSaveChanges = async (e) => {
    e.preventDefault();
    try {
          const isSensor = (editedDevice?.deviceKind || '').toLowerCase() === 'sensor';
          const endpoint = isSensor
            ? userDevicesEndpoint(`/sensors/${device.id}`)
            : userDevicesEndpoint(`/actuators/${device.id}`);

          const payload = isSensor
            ? {
                name: editedDevice?.name,
                sensorFields: editedDevice?.sensorFields,
                externalDeviceId: editedDevice?.externalDeviceId,
                isEnabled: editedDevice?.isEnabled,
              }
            : {
                name: editedDevice?.name,
                targetParameter: editedDevice?.targetParameter,
                externalDeviceId: editedDevice?.externalDeviceId,
                effectType: editedDevice?.effectType,
                effectStrength: Number(editedDevice?.effectStrength ?? 1),
                isEnabled: editedDevice?.isEnabled,
              };

          const result = await connectionManager.put(endpoint, payload);
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
                <DeviceParametersSeter device={editedDevice} setDevice={setEditedDevice} />
                
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