import React from 'react';
import './Header.css';
import StatusDot from '../StatusDot/StatusDot';
import Nav from '../Nav/Nav';

export const Header = ({ test }) => {
    return (
        <header className="header">
            <div className="header-left">
                <a className='logo' href="/">RE-PLANTED</a> 
                {test === "Communication with Client works!" ? <StatusDot status="green" size="medium" /> : <StatusDot status="red" size="medium" />}
            </div>
            <Nav/>
        </header>
    );
};
export default Header;