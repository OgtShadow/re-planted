import './DeviceProfile.css';

function DeviceProfile({ device }) {
  const name = device?.name ?? 'Unnamed device'
  const status = device?.status ?? 'unknown'

  return (
    <div className="device-profile-card">
      <h3>{name}</h3>
      <p>Status: {status}</p>
    </div>
  )
}

export default DeviceProfile
