import { useState, useEffect } from 'react'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import './App.css'
import connectionManager from './connectionManager'
import MessageSender from './components/MessageSender/MessageSender'
import PlantCreator from './components/PlantCreator/PlantCreator'
import PlantList from './components/PlantList/PlantList'
import StatusDot from './components/StatusDot/StatusDot'
import PlantDetails from './components/PlantDetails/PlantDetails'
import Header from './components/Header/Header'

function App() {
  const [test, setTest] = useState('')

  useEffect(() => {
    connectionManager.getText('/communication-test')
      .then(data => setTest(data))
      .catch(error => console.error('Failed to fetch:', error))
  }, [])

  return (
    <BrowserRouter>
        <Header test={test} />
        <Routes>
          <Route path="/" element={
            <>
              <PlantCreator />
              <PlantList />
            </>
          } />
          <Route path="/plant/:id" element={<PlantDetails />} />
        </Routes>
      
    </BrowserRouter>
  )
}

export default App
