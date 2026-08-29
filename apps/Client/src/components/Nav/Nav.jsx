import React from 'react';
import './Nav.css';

export const Nav = () => {
    return (
        <nav className="nav">
                    <ul>
                        <li><a href="/">Plants</a></li>
                        <li><a href="/devices">Devices</a></li>
                        <li><a href="/stats">Stats</a></li>
                        <li><a href="/plant/add">Add Plant</a></li>
                        <li><a href="/device/add">Add Device</a></li>
                        <li><a href="/contact">Contact</a></li>
                    </ul>
                </nav>
    );
};

export default Nav;