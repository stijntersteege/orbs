import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource-variable/geist'
import '@fontsource-variable/geist-mono'
import './styles/global.css'
import { App } from './app/App'

function render() {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}

// The live demo answers API calls in the browser. The import is dropped from normal builds.
if (import.meta.env.MODE === 'demo') {
  void import('./demo/install').then(({ installDemo }) => {
    installDemo()
    render()
  })
} else {
  render()
}
