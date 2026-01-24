import React from 'react';
import './StatusDot.css';

function StatusDot({ status, size } ) {

    const validStatuses = ['green', 'orange', 'red', 'gray'];
    if (!validStatuses.includes(status)) {
        console.error(`Invalid status "${status}" provided to StatusDot component.`);
        status = 'error';
    }

    return (
        <div className={`status-dot status-${status} size-${size}`} />
    );
}
export default StatusDot;