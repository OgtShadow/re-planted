import React from 'react';
import './Header.css';
import StatusDot from '../StatusDot/StatusDot';
import Nav from '../Nav/Nav';

export const Header = ({ test, activeUser, onLogout, alertCenter }) => {
    return (
        <header className="header">
            <div className="header-left">
                <a className='logo' href="/">RE-PLANTED</a> 
                {test === "Communication with Client works!" ? <StatusDot status="green" size="medium" /> : <StatusDot status="red" size="medium" />}
            </div>
            <div className="header-right">
                <Nav/>
                {alertCenter}
                <div className="user-session">
                    <span className="user-label">{activeUser?.username || activeUser?.email || 'User'}</span>
                    <button type="button" className="logout-button" onClick={onLogout}>Wyloguj</button>
                </div>
            </div>
        </header>
    );
};
export default Header;