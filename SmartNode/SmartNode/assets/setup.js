// P4-A setup wizard. POSTs {url, token} to /api/ha/connection (test + store in RAM),
// renders HA version / location / entity count or a typed error. The token is sent
// once and never rendered back; on load we only read connection STATUS (no token).
// No localStorage/sessionStorage — the token lives only in the backend's RAM.
const API = 'http://localhost:8080';
const $ = (id) => document.getElementById(id);
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) =>
  ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

function setState(msg, kind) {
  const el = $('state');
  el.className = `state${kind ? ' ' + kind : ''}`;
  el.textContent = msg || '';
}

function revealDiscovery() {
  const panel = $('discovery');
  if (panel) panel.hidden = false;
}

async function loadStatus() {
  try {
    const res = await fetch(`${API}/api/ha/connection`);
    if (!res.ok) return;
    const s = await res.json();
    if (s.url) $('url').value = s.url;               // prefill URL only (never a token)
    if (s.tokenSet) {
      setState(`A token is already set (source: ${esc(s.source)}). Submit to replace it.`);
      revealDiscovery();
    }
  } catch { /* SmartNode not up yet — leave the form blank */ }
}

async function testConnection(ev) {
  ev.preventDefault();
  const url = $('url').value.trim();
  const token = $('token').value;
  if (!url || !token) { setState('URL and token are required.', 'error'); return; }
  const btn = $('test');
  btn.disabled = true;
  setState('Testing connection…');
  $('result').innerHTML = '';
  try {
    const res = await fetch(`${API}/api/ha/connection`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url, token }),
    });
    let body = null;
    try { body = await res.json(); } catch { /* no json */ }
    if (!res.ok) {
      const msg = (body && body.error) ? body.error : `HTTP ${res.status}`;
      setState(`Error: ${esc(msg)}`, 'error');
      return;
    }
    setState('Connected.', 'ok');
    revealDiscovery();
    $('token').value = '';                            // do not keep the secret in the field
    const count = body.entityCount == null ? '—' : esc(body.entityCount);
    $('result').innerHTML = `
      <table>
        <tr><th>Home Assistant</th><td>${esc(body.haVersion || '—')}</td></tr>
        <tr><th>Location</th><td>${esc(body.locationName || '—')}</td></tr>
        <tr><th>Entities discovered</th><td>${count}</td></tr>
      </table>
      ${body.warning ? `<div class="state error">${esc(body.warning)}</div>` : ''}`;
  } catch (err) {
    const msg = (err && err.message === 'Failed to fetch')
      ? 'SmartNode unreachable at http://localhost:8080 — is it running?'
      : `Error: ${esc(err && err.message)}`;
    setState(msg, 'error');
  } finally {
    btn.disabled = false;
  }
}

$('conn-form').addEventListener('submit', testConnection);
loadStatus();
