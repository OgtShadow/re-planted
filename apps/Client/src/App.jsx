import { useState, useEffect } from 'react'
import './App.css'
import connectionManager from './connectionManager'
import MessageSender from './MessageSender'

function App() {
  const [test, setTest] = useState('')

  useEffect(() => {
    connectionManager.getText('/communication-test')
      .then(data => setTest(data))
      .catch(error => console.error('Failed to fetch:', error))
  }, [])

  return (
    <div>
      <h1>tekst to:{test}</h1>
      <MessageSender />
    </div>
  )
}

export default App
