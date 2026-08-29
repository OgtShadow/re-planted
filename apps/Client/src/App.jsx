import { useState, useEffect } from 'react'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import DeviceList from './components/DeviceList/DeviceList'
import DeviceDetails from './components/DeviceDetails/DeviceDetails'
import './App.css'
import connectionManager, { clearActiveUserId, clearAuthToken, getActiveUserId, getAuthToken, userByIdEndpoint } from './connectionManager'
import PlantList from './components/PlantList/PlantList'
import PlantDetails from './components/PlantDetails/PlantDetails'
import Header from './components/Header/Header'
import PlantAdd from './components/PlantAdd/PlantAdd'
import Login from './components/login/Login'
import DeviceAdd from './components/DeviceAdd/DeviceAdd'
import TelemetryStats from './components/TelemetryStats/TelemetryStats'
import TelemetryDetails from './components/TelemetryDetails/TelemetryDetails'

function App() {
  const [test, setTest] = useState('')
  const [activeUser, setActiveUser] = useState(null)
  const [isSessionChecked, setIsSessionChecked] = useState(false)

  useEffect(() => {
    connectionManager.getText('/communication-test')
      .then(data => setTest(data))
      .catch(error => console.error('Failed to fetch:', error))
  }, [])

  useEffect(() => {
    const restoreSession = async () => {
      const sessionUserId = getActiveUserId()
      const token = getAuthToken()

      if (!sessionUserId || !token) {
        setActiveUser(null)
        setIsSessionChecked(true)
        return
      }

      try {
        const user = await connectionManager.get(userByIdEndpoint(sessionUserId))
        setActiveUser(user)
      } catch {
        clearActiveUserId()
        clearAuthToken()
        setActiveUser(null)
      } finally {
        setIsSessionChecked(true)
      }
    }

    restoreSession()
  }, [])

  const handleLoginSuccess = (user) => {
    setActiveUser(user)
  }

  const handleLogout = () => {
    clearActiveUserId()
    clearAuthToken()
    setActiveUser(null)
  }

  if (!isSessionChecked) {
    return <div className="auth-loading">Ładowanie sesji...</div>
  }

  if (!activeUser) {
    return <Login onLoginSuccess={handleLoginSuccess} />
  }

  return (
    <BrowserRouter>
        <Header test={test} activeUser={activeUser} onLogout={handleLogout} />
        <Routes>
          <Route path="/" element={
              <>
                <PlantList />
              </>
          } />

          <Route path="/devices" element={
              <>
                <DeviceList/>
              </>
          } />

          <Route path="/plant/:id" element={
            <PlantDetails/> 
          } />

            <Route path="/device/:id" element={
            <DeviceDetails/> 
          } />

          <Route path="/plant/add" element={
            <PlantAdd/> 
          } />

          <Route path="/device/add" element={
            <DeviceAdd/>
          } />

          <Route path="/stats" element={
            <TelemetryStats/>
          } />

          <Route path="/telemetry/:deviceId" element={
            <TelemetryDetails/>
          } />
        </Routes>
      
    </BrowserRouter>
  )
}

export default App
