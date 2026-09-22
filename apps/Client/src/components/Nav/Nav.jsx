import React from 'react';
import './Nav.css';

export const Nav = () => {
    return (
        <nav className="nav" aria-label="Main navigation">
            <div className="nav-header">
                <a className="nav-brand" href="/">RE-PLANTED</a>
            </div>
            <ul>
                <li><a href="/">Plants</a></li>
                <li><a href="/devices">Devices</a></li>
                <li><a href="/stats">Stats</a></li>
                <li><a href="/contact">Contact</a></li>
            </ul>
        </nav>
    );
};

export default Nav;