import { useState, useEffect } from 'react'
import './App.css'
import connectionManager from './connectionManager'
import MessageSender from './components/MessageSender/MessageSender'
import PlantCreator from './components/PlantCreator/PlantCreator'
import PlantList from './components/PlantList/PlantList'
import StatusDot from './components/StatusDot/StatusDot'

function App() {
  const [test, setTest] = useState('')

  useEffect(() => {
    connectionManager.getText('/communication-test')
      .then(data => setTest(data))
      .catch(error => console.error('Failed to fetch:', error))
  }, [])

  return (
    <div>
      <h1>RE-PLANTED {test === "Communication with Client works!" ? <StatusDot status="green" size="big" /> : <StatusDot status="red" size="big" />}</h1>
      <MessageSender />
      <PlantCreator />
      <PlantList />
    </div>
  )
}

export default App
