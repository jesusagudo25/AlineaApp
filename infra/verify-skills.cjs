// Solo RPC de lectura: no crea threads ni ejecuta turnos de agente.
const { spawn } = require('node:child_process');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const server = spawn(process.platform === 'win32' ? 'codex.cmd' : 'codex', ['app-server', '--listen', 'stdio://'], {
  cwd: root, shell: process.platform === 'win32', windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'],
});
let buffer = '';
let completed = false;
const timeout = setTimeout(() => {
  console.error('Timeout al listar skills; no se inició ningún turno.');
  process.exitCode = 1;
  server.stdin.end();
  server.kill();
}, 20000);
const send = value => server.stdin.write(JSON.stringify(value) + '\n');
server.on('error', error => { clearTimeout(timeout); console.error(error.code); process.exitCode = 1; });
server.stderr.on('data', () => {}); // No imprimir configuración o rutas de otros proyectos.
server.stdout.on('data', chunk => {
  buffer += chunk.toString();
  let newline;
  while ((newline = buffer.indexOf('\n')) !== -1) {
    const line = buffer.slice(0, newline); buffer = buffer.slice(newline + 1);
    let message;
    try { message = JSON.parse(line); } catch { continue; }
    if (message.id === 1) {
      if (message.error) { console.error('No se pudo inicializar RPC.'); server.stdin.end(); process.exitCode = 1; continue; }
      send({ method: 'initialized' });
      send({ id: 2, method: 'skills/list', params: { cwds: [root], forceReload: true } });
    }
    if (message.id === 2) {
      completed = true; clearTimeout(timeout);
      const items = message.result?.data ?? [];
      const skills = items.flatMap(item => item.skills ?? []).filter(skill =>
        path.resolve(skill.path).startsWith(path.join(root, '.agents', 'skills') + path.sep));
      const errors = items.flatMap(item => item.errors ?? []);
      console.log(JSON.stringify({ method: 'skills/list', count: skills.length,
        skills: skills.map(skill => ({ name: skill.name, scope: skill.scope, enabled: skill.enabled, path: path.relative(root, skill.path) })),
        errorCount: errors.length, rpcError: message.error?.code ?? null }, null, 2));
      if (skills.length !== 6 || skills.some(skill => !skill.enabled) || errors.length || message.error) process.exitCode = 1;
      server.stdin.end();
    }
  }
});
server.on('close', () => { clearTimeout(timeout); if (!completed) process.exitCode = 1; });
send({ id: 1, method: 'initialize', params: { clientInfo: { name: 'alinea_phase00_verifier', title: 'AlineaApp skill discovery', version: '0.0.0' }, capabilities: { experimentalApi: true } } });
