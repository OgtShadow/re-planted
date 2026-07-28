import { useState } from 'react';
import connectionManager, { setActiveUserId, setAuthToken, userLoginEndpoint } from '../../connectionManager';
import './Login.css';

export default function Login({ onLoginSuccess }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    if (!email || !password) {
      setError('Wszystkie pola są wymagane');
      return;
    }

    setIsSubmitting(true);

    try {
      const user = await connectionManager.post(userLoginEndpoint, {
        login: email,
        password,
      });

      setActiveUserId(user.id);
      setAuthToken(user.token);
      setError('');

      if (onLoginSuccess) {
        onLoginSuccess(user);
      }
    } catch (submitError) {
      if (submitError?.message?.includes('401')) {
        setError('Nieprawidłowy login lub hasło');
      } else {
        setError('Nie udało się zalogować. Spróbuj ponownie.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-box">
        <h1>Logowanie</h1>
        
        {error && <div className="error-message">{error}</div>}
        
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="email">Email lub nazwa użytkownika:</label>
            <input
              id="email"
              type="text"
              value={email}
              onChange={(e) => {
                setEmail(e.target.value);
                setError('');
              }}
              placeholder="Wpisz email lub nazwę użytkownika"
            />
          </div>

          <div className="form-group">
            <label htmlFor="password">Hasło:</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value);
                setError('');
              }}
              placeholder="Wpisz swoje hasło"
            />
          </div>

          <button type="submit" className="login-button">
            {isSubmitting ? 'Logowanie...' : 'Zaloguj się'}
          </button>
        </form>

        <div className="login-footer">
          <a href="#forgot">Zapomniałeś hasła?</a>
          <span> | </span>
          <a href="#register">Zarejestruj się</a>
        </div>
      </div>
    </div>
  );
}
