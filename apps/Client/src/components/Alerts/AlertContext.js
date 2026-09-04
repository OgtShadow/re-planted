import { createContext, useContext } from 'react';

export const AlertContext = createContext(null);

export function useAlerts() {
  const context = useContext(AlertContext);
  if (!context) throw new Error('useAlerts must be used inside AlertProvider');
  return context;
}
