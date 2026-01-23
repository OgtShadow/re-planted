import { useState } from 'react'
import connectionManager from '../../connectionManager'

function MessageSender() {
  let [message, setMessage] = useState('')
  let [response, setResponse] = useState('')

  const handleSend = async () => {
    try {
      const result = await connectionManager.post('/api/post', { message })
      setResponse(JSON.stringify(result))
    } catch (error) {
      setResponse('Error: ' + error.message)
      console.error('Failed to send message:', error)
    }
  }

  return (
    <div>
      <h2>Wyślij wiadomość do serwera</h2>
      <input 
        type="text" 
        value={message} 
        onChange={(e) => setMessage(e.target.value)} 
        placeholder="Wpisz wiadomość" 
      />
      <button onClick={handleSend}>Wyślij</button>
      <p>Odpowiedź: {response}</p>
    </div>
  )
}

export default MessageSender