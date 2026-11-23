// Native verification only. Does not build, start, stop or reconfigure Docker.
// Uses new isolated databases on the already running PostgreSQL container.
const fs = require('node:fs');
const net = require('node:net');
const path = require('node:path');
const crypto = require('node:crypto');
const cp = require('node:child_process');
const root = path.resolve(__dirname, '..');
process.chdir(root);
const privateDir = path.join(root, '.local', 'phase02d');
const evidence = path.join(root, 'docs', 'evidence', 'phase02d');
fs.mkdirSync(privateDir, { recursive: true }); fs.mkdirSync(evidence, { recursive: true });
const configPath = path.join(privateDir, 'runtime.json');
const mode = process.argv[2];
if (mode === 'prepare') {
  if (fs.existsSync(configPath)) throw new Error('Private configuration exists; preserve it and use the existing isolated databases.');
  const suffix = crypto.randomBytes(4).toString('hex');
  const role = 'alinea02d_' + suffix, password = crypto.randomBytes(32).toString('hex');
  const tests = 'alinea02d_tests_' + suffix, browser = 'alinea02d_browser_' + suffix;
  const sql = `CREATE ROLE ${role} LOGIN PASSWORD '${password}';\nCREATE DATABASE ${tests} OWNER ${role};\nCREATE DATABASE ${browser} OWNER ${role};\n`;
  try { cp.execFileSync('docker', ['exec', '-i', 'alinea-local-postgres-1', 'sh', '-c', 'exec psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1'], { input: sql, stdio: ['pipe', 'ignore', 'pipe'] }); }
  catch { throw new Error('Could not create new isolated databases; no existing database was reset.'); }
  const connection = database => `Host=127.0.0.1;Port=5544;Database=${database};Username=${role};Password=${password};SSL Mode=Disable`;
  fs.writeFileSync(configPath, JSON.stringify({ tests: connection(tests), browser: connection(browser), inboxKey: crypto.randomBytes(32).toString('hex'), otpKey: crypto.randomBytes(32).toString('hex') }));
  console.log('Created two isolated databases. Private credentials stored only under ignored .local/.');
  process.exit(0);
}
const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
if (mode === 'renew-browser' || mode === 'renew-tests') {
  const target = mode === 'renew-browser' ? 'browser' : 'tests';
  checked(config[target], 'alinea02d_' + target + '_');
  const role = config[target].match(/Username=([a-z0-9_]+);/)[1];
  const database = 'alinea02d_' + target + '_' + crypto.randomBytes(4).toString('hex');
  cp.execFileSync('docker', ['exec', '-i', 'alinea-local-postgres-1', 'sh', '-c', 'exec psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1'], { input: `CREATE DATABASE ${database} OWNER ${role};`, stdio: ['pipe', 'ignore', 'pipe'] });
  config[target] = config[target].replace(/Database=[a-z0-9_]+;/, `Database=${database};`);
  fs.writeFileSync(configPath, JSON.stringify(config));
  console.log('New isolated verification database selected. Previous database preserved. Restart the native verifier.'); process.exit(0);
}
function checked(database, prefix) { if (!database.includes('Database=' + prefix)) throw new Error('Isolated database prefix required.'); return database; }
if (mode === 'upgrade') {
  const database = 'alinea02d_upgrade_' + crypto.randomBytes(4).toString('hex');
  function sql(input, db = database) {
    const result = cp.spawnSync('docker', ['exec', '-i', 'alinea-local-postgres-1', 'sh', '-c', `psql -U "$POSTGRES_USER" -d ${db} -v ON_ERROR_STOP=1 -q`], { input, encoding: 'utf8', windowsHide: true, maxBuffer: 8 * 1024 * 1024 });
    if (result.status !== 0) throw new Error('Isolated upgrade verification failed; details suppressed.'); return result.stdout;
  }
  sql(`CREATE DATABASE ${database};`, 'postgres');
  sql(fs.readFileSync(path.join(privateDir, 'baseline.sql')));
  sql(fs.readFileSync(path.join(evidence, 'upgrade-fixture.sql')));
  const check = `SELECT "Id","UserId","Kind","AccountId","CategoryId","Amount","Date","Description","IsVoided","Version","CreatedAt","UpdatedAt" FROM "TRANSACTIONS";
    SELECT md5(string_agg(row_to_json(r)::text,'' ORDER BY "Id")) AS revision_checksum FROM "TRANSACTION_REVISIONS" r;
    SELECT "OpeningBalance" - (SELECT sum("Amount") FROM "TRANSACTIONS") AS balance FROM "ACCOUNTS";`;
  const before = sql(check); sql(fs.readFileSync('infra/cards-commitments-migration.sql')); const after = sql(check);
  fs.writeFileSync(path.join(evidence, 'upgrade-before.txt'), before); fs.writeFileSync(path.join(evidence, 'upgrade-after.txt'), after);
  if (before !== after) throw new Error('Pre-existing ledger data changed during upgrade.');
  fs.writeFileSync(path.join(evidence, 'upgrade-result.json'), JSON.stringify({ database, dataPreserved: true, schema: sql('SELECT count(*) AS migrations FROM "__EFMigrationsHistory";') }, null, 2) + '\n');
  console.log('02C to 02D upgrade preserved the pre-existing transaction, audit snapshot and 87.66 balance.'); process.exit(0);
}
if (mode === 'backup') {
  checked(config.browser, 'alinea02d_browser_');
  const database = config.browser.match(/Database=([a-z0-9_]+);/)[1];
  const restored = 'alinea02d_restore_' + crypto.randomBytes(4).toString('hex');
  const sql = fs.readFileSync(path.join(evidence, 'restore-check.sql'));
  function command(command, input) {
    const result = cp.spawnSync('docker', ['exec', '-i', 'alinea-local-postgres-1', 'sh', '-c', command], { input, windowsHide: true, maxBuffer: 32 * 1024 * 1024 });
    if (result.status !== 0) throw new Error('Isolated backup/restore verification failed; details suppressed.'); return result.stdout;
  }
  const dump = command(`pg_dump -U "$POSTGRES_USER" -d ${database} -Fc`);
  fs.mkdirSync('backups', { recursive: true }); fs.writeFileSync('backups/phase02d.dump', dump);
  command('psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1', `CREATE DATABASE ${restored};`);
  command(`pg_restore -U "$POSTGRES_USER" -d ${restored} --exit-on-error --no-owner --no-privileges`, dump);
  const source = command(`psql -U "$POSTGRES_USER" -d ${database} -v ON_ERROR_STOP=1`, sql);
  const target = command(`psql -U "$POSTGRES_USER" -d ${restored} -v ON_ERROR_STOP=1`, sql);
  fs.writeFileSync(path.join(evidence, 'restore-source.txt'), source); fs.writeFileSync(path.join(evidence, 'restore-target.txt'), target);
  if (!source.equals(target)) throw new Error('Restored content differs.');
  fs.writeFileSync(path.join(evidence, 'backup-check.json'), JSON.stringify({ restoredDatabase: restored, sha256: crypto.createHash('sha256').update(dump).digest('hex'), contentMatches: true }, null, 2) + '\n');
  console.log(target.toString()); process.exit(0);
}
function environment(database) { return { ...process.env,
  ASPNETCORE_ENVIRONMENT: 'Testing', ASPNETCORE_URLS: 'http://127.0.0.1:5084',
  Database__ConnectionString: database, Database__ApplyMigrationsOnStartup: 'true',
  Auth__AllowInsecureLocalHttp: 'true', Auth__OtpKey: config.otpKey, Auth__DataProtectionPath: path.join(privateDir, 'keys'),
  Sms__Provider: 'DevelopmentMailbox', Sms__DevelopmentInboxKey: config.inboxKey, Sms__Brevo__SendingEnabled: 'false', Sms__Brevo__ApiKey: '',
}; }
if (mode === 'tests') {
  const result = cp.spawnSync('dotnet', ['test', 'tests/Alinea.Api.Tests', '--no-restore', '--filter', 'FullyQualifiedName~PlanningTests|FullyQualifiedName~TransactionTests|FullyQualifiedName~FinanceTests|FullyQualifiedName~Migration_seed_restart_and_existing_values_are_preserved', '--logger', 'console;verbosity=normal'], {
    env: { ...process.env, ALINEA_TEST_CONNECTION: checked(config.tests, 'alinea02d_tests_'), Auth__DataProtectionPath: path.join(privateDir, 'test-keys') }, encoding: 'utf8', windowsHide: true, maxBuffer: 16 * 1024 * 1024,
  });
  fs.writeFileSync(path.join(evidence, 'backend-tests.txt'), (result.stdout ?? '') + (result.stderr ?? ''));
  console.log((result.stdout ?? '').split(/\r?\n/).slice(-8).join('\n')); process.exit(result.status ?? 1);
}
if (mode !== 'bridge' && mode !== 'serve') throw new Error('Use prepare, bridge, tests or serve.');
const clients = new Set(), children = new Set();
const bridge = net.createServer(socket => {
  clients.add(socket);
  // Reap the reader before bash exits: PostgreSQL is PID 1 in this existing
  // container and must never inherit an auxiliary child terminated by signal.
  const child = cp.spawn('docker', ['exec', '-i', 'alinea-local-postgres-1', 'bash', '-c', 'exec 3<>/dev/tcp/127.0.0.1/5432; cat <&3 & reader=$!; cat >&3; kill "$reader" 2>/dev/null; wait "$reader" 2>/dev/null; exit 0'], { stdio: ['pipe', 'pipe', 'ignore'], windowsHide: true });
  children.add(child); socket.pipe(child.stdin); child.stdout.pipe(socket);
  child.stdin.on('error', () => socket.destroy()); child.stdout.on('error', () => socket.destroy());
  child.on('error', () => socket.destroy()); child.on('close', () => { children.delete(child); socket.destroy(); });
  socket.on('error', () => {}); socket.on('close', () => { clients.delete(socket); child.stdin.end(); });
});
function shutdown() { for (const socket of clients) socket.destroy(); for (const child of children) child.kill(); bridge.close(() => process.exit(0)); }
process.on('SIGINT', shutdown); process.on('SIGTERM', shutdown);
process.stdin.resume(); process.stdin.on('data', data => { if (data.toString().trim() === 'stop') shutdown(); });
bridge.listen(5544, '127.0.0.1', async () => {
  console.log('Local database bridge on loopback 5544; existing container unchanged.');
  if (mode !== 'serve') return;
  const env = environment(checked(config.browser, 'alinea02d_browser_'));
  const dll = path.join(root, 'backend', 'Alinea.Api', 'bin', 'Debug', 'net10.0', 'Alinea.Api.dll');
  const log = fs.openSync(path.join(evidence, 'native-services.log'), 'a');
  for (const phone of ['+999000000401', '+999000000402']) {
    await new Promise((resolve, reject) => {
      const child = cp.spawn('dotnet', [dll, '--allow-phone'], { cwd: path.join(root, 'backend', 'Alinea.Api'), env: { ...env, ALINEA_ADMIN_PHONE: phone }, windowsHide: true, stdio: ['ignore', log, log] });
      child.on('error', reject); child.on('exit', code => code === 0 ? resolve() : reject(new Error('Synthetic authorization failed.')));
    });
  }
  const api = cp.spawn('dotnet', [dll], { cwd: path.join(root, 'backend', 'Alinea.Api'), env, windowsHide: true, stdio: ['ignore', log, log] }); children.add(api);
  api.on('exit', code => { console.error('Native API exited with status ' + code); });
  const web = cp.spawn('node', ['node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--port', '5174', '--strictPort'], { cwd: path.join(root, 'frontend'), env: { ...process.env, API_PROXY_TARGET: 'http://127.0.0.1:5084' }, windowsHide: true, stdio: ['ignore', log, log] }); children.add(web);
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    try { const response = await fetch('http://127.0.0.1:5084/health/ready'); if (response.ok) { console.log('Native API ready on loopback 5084; Vite on 5174, isolated database and SMS mailbox.'); return; } } catch {}
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  console.error('Native readiness did not pass. Inspect the local service log before running browsers.');
});
