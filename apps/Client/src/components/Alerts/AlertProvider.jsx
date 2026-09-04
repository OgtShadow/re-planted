import { useEffect, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import connectionManager, { API_BASE_URL, getAuthToken, userAlertsEndpoint } from '../../connectionManager';
import { AlertContext } from './AlertContext';

export function AlertProvider({ children, userId }) {
  const [alerts, setAlerts] = useState([]);
  const [toast, setToast] = useState(null);

  useEffect(() => {
    if (!userId) return undefined;

    let connection;
    let disposed = false;
    const loadAlerts = async () => {
      try {
        const data = await connectionManager.get(userAlertsEndpoint('?activeOnly=false', userId));
        if (!disposed) setAlerts(data);
      } catch (error) {
        console.error('Failed to load alerts:', error);
      }
    };

    loadAlerts();
    connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/alertsHub`, { accessTokenFactory: () => getAuthToken() || '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on('AlertCreated', (alert) => {
      setAlerts((current) => [alert, ...current.filter((item) => item.id !== alert.id)]);
      setToast(alert);
    });
    connection.on('AlertAcknowledged', (alertId) => {
      setAlerts((current) => current.map((alert) => alert.id === alertId
        ? { ...alert, acknowledgedAtUtc: new Date().toISOString() }
        : alert));
    });
    connection.start().catch((error) => console.error('Alert hub connection failed:', error));

    return () => {
      disposed = true;
      connection?.stop();
    };
  }, [userId]);

  useEffect(() => {
    if (!toast) return undefined;
    const timeout = window.setTimeout(() => setToast(null), 7000);
    return () => window.clearTimeout(timeout);
  }, [toast]);

  const acknowledge = async (alertId) => {
    await connectionManager.post(userAlertsEndpoint(`/${alertId}/acknowledge`, userId));
    setAlerts((current) => current.map((alert) => alert.id === alertId
      ? { ...alert, acknowledgedAtUtc: new Date().toISOString() }
      : alert));
  };

  return <AlertContext.Provider value={{ alerts, toast, dismissToast: () => setToast(null), acknowledge }}>
    {children}
  </AlertContext.Provider>;
}
