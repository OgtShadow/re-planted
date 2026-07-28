const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") || "http://localhost:5000";
const ACTIVE_USER_ID_KEY = "replanted.activeUserId";

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

const userPlantsEndpoint = (suffix = "", userId = getActiveUserId()) => {
  if (!Number.isInteger(userId) || userId <= 0) {
    throw new Error("No active user session");
  }

  return `/api/users/${userId}/plants${suffix}`;
};

const userLoginEndpoint = "/api/users/login";

const userByIdEndpoint = (userId = getActiveUserId()) => {
  if (!Number.isInteger(userId) || userId <= 0) {
    throw new Error("No active user session");
  }

  return `/api/users/${userId}`;
};

class ConnectionManager {
  constructor(baseUrl = API_BASE_URL) {
    this.baseUrl = baseUrl;
  }

  async get(endpoint) {
    try {
      const response = await fetch(`${this.baseUrl}${endpoint}`);
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
      const response = await fetch(`${this.baseUrl}${endpoint}`);
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
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
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
        headers: {
          'Content-Type': 'application/json',
        },
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
  userPlantsEndpoint,
  userLoginEndpoint,
  userByIdEndpoint,
};