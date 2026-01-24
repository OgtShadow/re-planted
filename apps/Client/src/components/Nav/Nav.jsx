import React from 'react';
import './Nav.css';

export const Nav = () => {
    return (
        <nav className="nav">
                    <ul>
                        <li><a href="/">Home</a></li>
                        <li><a href="/plant/add">Add</a></li>
                        <li><a href="/contact">Contact</a></li>
                    </ul>
                </nav>
    );
};

export default Nav;