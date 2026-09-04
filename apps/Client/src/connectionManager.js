const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") || "http://localhost:5000";
const ACTIVE_USER_ID_KEY = "replanted.activeUserId";
const AUTH_TOKEN_KEY = "replanted.authToken";

const getActiveUserId = () => {
  try {
    const stored = window.localStorage.getItem(ACTIVE_USER_ID_KEY);
    const parsed = Number(stored);
    if (Number.isInteger(parsed) && parsed > 0) {
      return parsed;
    }
  } catch {
  }

  return null;
};

const setActiveUserId = (userId) => {
  const parsed = Number(userId);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return;
  }

  try {
    window.localStorage.setItem(ACTIVE_USER_ID_KEY, String(parsed));
  } catch {
  }
};

const clearActiveUserId = () => {
  try {
    window.localStorage.removeItem(ACTIVE_USER_ID_KEY);
  } catch {
  }
};

const getAuthToken = () => {
  try {
    return window.localStorage.getItem(AUTH_TOKEN_KEY);
  } catch {
    return null;
  }
};

const setAuthToken = (token) => {
  if (!token) {
    return;
  }

  try {
    window.localStorage.setItem(AUTH_TOKEN_KEY, token);
  } catch {
  }
};

const clearAuthToken = () => {
  try {
    window.localStorage.removeItem(AUTH_TOKEN_KEY);
  } catch {
  }
};

const userPlantsEndpoint = (suffix = "", userId = getActiveUserId()) => {
  if (!Number.isInteger(userId) || userId <= 0) {
    throw new Error("No active user session");
  }

  return `/api/users/${userId}/plants${suffix}`;
};

const userDevicesEndpoint = (suffix = "", userId = getActiveUserId()) => {
  if (!Number.isInteger(userId) || userId <= 0) {
    throw new Error("No active user session");
  }

  return `/api/users/${userId}/devices${suffix}`;
};

const userLoginEndpoint = "/api/users/login";

const userByIdEndpoint = (userId = getActiveUserId()) => {
  if (!Number.isInteger(userId) || userId <= 0) {
    throw new Error("No active user session");
  }

  return `/api/users/${userId}`;
};

const userTelemetryEndpoint = (suffix = "", userId = getActiveUserId()) => {
  if (!Number.isInteger(userId) || userId <= 0) {
    throw new Error("No active user session");
  }

  return `/api/users/${userId}/telemetry${suffix}`;
};

const userTelemetryRefreshEndpoint = (userId = getActiveUserId()) => userTelemetryEndpoint('/refresh', userId);

const userAlertsEndpoint = (suffix = "", userId = getActiveUserId()) => {
  if (!Number.isInteger(userId) || userId <= 0) {
    throw new Error("No active user session");
  }

  return `/api/users/${userId}/alerts${suffix}`;
};

class ConnectionManager {
  constructor(baseUrl = API_BASE_URL) {
    this.baseUrl = baseUrl;
  }

  getAuthHeaders(extraHeaders = {}) {
    const token = getAuthToken();
    const headers = { ...extraHeaders };

    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    return headers;
  }

  async get(endpoint) {
    try {
      const response = await fetch(`${this.baseUrl}${endpoint}`, {
        headers: this.getAuthHeaders(),
      });
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      return await response.json();
    } catch (error) {
      console.error('GET request failed:', error);
      throw error;
    }
  }

  async getText(endpoint) {
    try {
      const response = await fetch(`${this.baseUrl}${endpoint}`, {
        headers: this.getAuthHeaders(),
      });
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      return await response.text();
    } catch (error) {
      console.error('GET text request failed:', error);
      throw error;
    }
  }

  async post(endpoint, data) {
    try {
      const response = await fetch(`${this.baseUrl}${endpoint}`, {
        method: 'POST',
        headers: this.getAuthHeaders({
          'Content-Type': 'application/json',
        }),
        body: JSON.stringify(data),
      });
      if (!response.ok) {
        const errorBody = await response.text();
        let errorMessage = `HTTP error! status: ${response.status}`;
        try {
          const parsedError = JSON.parse(errorBody);
          errorMessage = parsedError.response || parsedError.Response || parsedError.title || errorMessage;
        } catch {
        }
        throw new Error(errorMessage);
      }
      if (response.status === 204) {
        return null;
      }
      return await response.json();
    } catch (error) {
      console.error('POST request failed:', error);
      throw error;
    }
  }

  async put(endpoint, data) {
    try {
      const response = await fetch(`${this.baseUrl}${endpoint}`, {
        method: 'PUT',
        headers: this.getAuthHeaders({
          'Content-Type': 'application/json',
        }),
        body: JSON.stringify(data),
      });
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      return await response.json();
    } catch (error) {
      console.error('PUT request failed:', error);
      throw error;
    }
  }

  async delete(endpoint) {
    try {
      const response = await fetch(`${this.baseUrl}${endpoint}`, {
        method: 'DELETE',
        headers: this.getAuthHeaders(),
      });
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      return await response.json();
    } catch (error) {
      console.error('DELETE request failed:', error);
      throw error;
    }
  }
}

export const connectionManager = new ConnectionManager();
export default connectionManager;
export {
  API_BASE_URL,
  getActiveUserId,
  setActiveUserId,
  clearActiveUserId,
  getAuthToken,
  setAuthToken,
  clearAuthToken,
  userPlantsEndpoint,
  userDevicesEndpoint,
  userLoginEndpoint,
  userByIdEndpoint,
  userTelemetryEndpoint,
  userTelemetryRefreshEndpoint,
  userAlertsEndpoint,
};