import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.jsx'
import './index.css'
import { useAuthStore } from './store/authStore'

// Server-validate any persisted session on load so a revoked-but-unexpired
// token doesn't render as authenticated. Runs async so it doesn't block first
// paint; verifySession() clears auth on failure and routes redirect to login.
useAuthStore.getState().verifySession().catch((error) => {
  console.error('Startup session verification failed:', error?.message || error)
})

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)
