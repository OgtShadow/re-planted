import { useState, useEffect } from 'react'
import './App.css'

function App() {
  const [test, setTest] = useState('')

  useEffect(() => {
    fetch("http://localhost:5111/communication-test")
      .then(r => r.text())
      .then(data => setTest(data))
  }, [])

  return (
    <div>
      <h1>tekst to:{test}</h1>
    </div>
  )
}

export default App
