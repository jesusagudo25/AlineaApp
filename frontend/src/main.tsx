import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { DesignPreview } from './features/design-preview/DesignPreview';
import './styles.css';

createRoot(document.getElementById('root')!).render(<StrictMode><DesignPreview /></StrictMode>);
