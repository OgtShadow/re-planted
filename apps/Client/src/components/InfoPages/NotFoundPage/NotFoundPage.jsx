import React from 'react'
import './NotFoundPage.css'
import NotFoundImage from '../../../resources/404.png'

const NotFoundPage = () => {
    return (
        <div className="not-found-container">
            <div className="not-found-content">
            <h1>404 - Page Not Found</h1>
            <p>The page you are looking for does not exist.</p>
            </div>
            <div className= "not-found-img">
               <img src={NotFoundImage} alt="Not Found" />
            </div>
        </div>
    )
}

export default NotFoundPage