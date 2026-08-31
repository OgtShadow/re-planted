import React from 'react';
import './AddButton.css';

const AddButton = ({ link }) => {
    return (
        <a className="add-button" href={link}>
            +
        </a>
    );
};

export default AddButton;