import React from 'react';
import './Nav.css';
import UserSettings from '../UserSettings/UserSettings';
import StatusDot from '../StatusDot/StatusDot';

export const Nav = ({ test, activeUser, onLogout, alertCenter }) => {
    return (
        <nav className="nav" aria-label="Main navigation">
            <div className="nav-header">
                <a className="nav-brand" href="/">RE-PLANTED</a>
                <StatusDot status={test === "Communication with Client works!" ? "green" : "red"} size="medium" />
            </div>
            <ul>
                <li><a href="/">Plants</a></li>
                <li><a href="/devices">Devices</a></li>
                <li><a href="/stats">Stats</a></li>
                <li><a href="/contact">Contact</a></li>
            </ul>
            <div className="nav-account">
                {alertCenter}
                <div className="user-session">
                    <UserSettings
                        activeUser={activeUser}
                        onLogout={onLogout}
                    />
                </div>
            </div>
        </nav>
    );
};

export default Nav;