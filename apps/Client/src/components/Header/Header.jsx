import React from 'react';
import './Header.css';
import StatusDot from '../StatusDot/StatusDot';
import Nav from '../Nav/Nav';

export const Header = ({ test }) => {
    return (
        <header className="header">
        <h1>RE-PLANTED {test === "Communication with Client works!" ? <StatusDot status="green" size="medium" /> : <StatusDot status="red" size="medium" />}</h1>
            <Nav/>
        </header>
    );
};
export default Header;