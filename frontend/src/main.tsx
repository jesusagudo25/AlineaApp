import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { InitializationPage } from './features/initialization/InitializationPage';
import './styles.css';

createRoot(document.getElementById('root')!).render(<StrictMode><InitializationPage /></StrictMode>);
