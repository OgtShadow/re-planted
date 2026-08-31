import React from 'react';
import './Nav.css';

export const Nav = () => {
    return (
        <nav className="nav">
                    <ul>
                        <li><div><a href="/">Plants</a></div></li>
                        <li><div><a href="/devices">Devices</a></div></li>
                        <li><div><a href="/stats">Stats</a></div></li>
                        <li><div><a href="/contact">Contact</a></div></li>
                    </ul>
                </nav>
    );
};

export default Nav;