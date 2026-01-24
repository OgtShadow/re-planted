import React from 'react';
import './Header.css';
import StatusDot from '../StatusDot/StatusDot';

export const Header = ({ test }) => {
    return (
        <header className="header">
        <h1>RE-PLANTED {test === "Communication with Client works!" ? <StatusDot status="green" size="medium" /> : <StatusDot status="red" size="medium" />}</h1>
                <nav className="nav">
                    <ul>
                        <li><a href="/">Home</a></li>
                        <li><a href="/about">About</a></li>
                        <li><a href="/contact">Contact</a></li>
                    </ul>
                </nav>
        </header>
    );
};
export default Header;