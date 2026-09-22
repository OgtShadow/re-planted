import React from "react";
import "./UserSettings.css";

export const UserSettings = ({ activeUser, onLogout }) => {
    return (
        <details className="user-settings">
            <summary className="user-label">
                {activeUser?.username || activeUser?.email || 'User'}
            </summary>
            <div className="user-options">
                <span className="user-options-title">Ustawienia użytkownika</span>
                <button type="button" className="logout-button" onClick={onLogout}>Wyloguj</button>
            </div>
        </details>
    );
};

export default UserSettings;