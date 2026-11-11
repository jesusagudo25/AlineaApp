type IconName = 'home' | 'activity' | 'accounts' | 'calendar' | 'phone' | 'arrow' | 'plus' | 'lock' | 'receipt';

const paths: Record<IconName, string> = {
  home: 'M3 10.5 12 3l9 7.5M5.5 9v11h5v-6h3v6h5V9',
  activity: 'M4 7h16M16 3l4 4-4 4M20 17H4m4-4-4 4 4 4',
  accounts: 'M4 7h16v13H4zM4 7V5l13-2v4M15 12h5v4h-5z',
  calendar: 'M5 5h14v16H5zM8 3v4m8-4v4M5 10h14m-10 4h2m2 3h2',
  phone: 'M8 3h8a1 1 0 0 1 1 1v16a1 1 0 0 1-1 1H8a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1Zm3 14h2',
  arrow: 'M4 12h15m-5-5 5 5-5 5',
  plus: 'M12 5v14M5 12h14',
  lock: 'M7 10V7a5 5 0 0 1 10 0v3M5 10h14v11H5zM12 14v3',
  receipt: 'M6 3h12v18l-3-2-3 2-3-2-3 2V3Zm3 5h6m-6 4h4',
};

export function Icon({ name, className }: { name: IconName; className?: string }) {
  return <svg className={className} width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name]} /></svg>;
}

export function Brand() {
  return <span className="brand"><span className="brand-symbol" aria-hidden="true"><svg width="24" height="24" viewBox="0 0 24 24" fill="none"><path d="M12 5h7M8 12h11M4 19h15" stroke="currentColor" strokeWidth="3" strokeLinecap="round" /></svg></span><span>alinea<span className="brand-app">app</span></span></span>;
}
