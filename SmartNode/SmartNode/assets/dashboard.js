// Product dashboard for SmartNode. This file never sends dryRun:false.
const queryApi = new URLSearchParams(window.location.search).get('api');
const API = (queryApi || 'http://localhost:8080').replace(/\/$/, '');

const SECRET_MARKERS = [
  'bearer',
  'token',
  'secret',
  'password',
  'api_key',
  'apikey',
  'access_token',
  'credential',
];

const BINDINGS_SAMPLE = `{
  "profile": "dashboard-sample",
  "sensors": [
    {
      "sensorUri": "urn:rdt:sample:sensor:living-room",
      "procedureUri": "urn:rdt:sample:procedure:living-room",
      "kind": "HomeAssistant",
      "haEntityId": "sensor.living_room_temperature"
    }
  ],
  "actuators": [
    {
      "actuatorUri": "urn:rdt:sample:actuator:kitchen-light",
      "kind": "HomeAssistant",
      "haEntityId": "light.kitchen",
      "haKind": "Light"
    }
  ]
}`;

const $ = (id) => document.getElementById(id);
const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
}[char]));

const fmtNumber = (value, digits = 2) => {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return '-';
  return Number(value).toFixed(digits);
};

const fmtTime = (value) => {
  if (!value) return '-';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? String(value) : d.toLocaleString();
};

const yesNo = (value) => (value ? 'yes' : 'no');
const limitText = (value) => (Number(value) > 0 ? String(value) : 'unlimited');

function isSecretLike(value) {
  const text = String(value ?? '');
  return SECRET_MARKERS.some((marker) => text.toLowerCase().includes(marker));
}

function redactValue(value, key = '') {
  if (isSecretLike(key)) return '[redacted]';
  if (typeof value === 'string') return isSecretLike(value) ? '[redacted]' : value;
  if (Array.isArray(value)) return value.map((item) => redactValue(item));
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([childKey, childValue]) => {
      const safeKey = isSecretLike(childKey) ? 'redacted' : childKey;
      return [safeKey, redactValue(childValue, childKey)];
    }));
  }
  return value;
}

function pillClass(kind) {
  return `status-pill ${kind || 'neutral'}`;
}

function badge(label, kind = 'muted') {
  return `<span class="badge ${kind}">${esc(label)}</span>`;
}

function setPill(panelId, label, kind) {
  const el = $(`${panelId}-pill`);
  if (!el) return;
  el.className = pillClass(kind);
  el.textContent = label;
}

function setMetric(metricId, value, detail) {
  const state = $(`${metricId}-state`);
  const detailEl = $(`${metricId}-detail`);
  if (state) state.textContent = value;
  if (detailEl) detailEl.innerHTML = detail;
}

function stateHtml(message, kind = '') {
  return `<div class="state ${kind}">${esc(message)}</div>`;
}

function panelError(panelId, err) {
  const message = err?.message === 'Failed to fetch'
    ? `SmartNode unreachable at ${API}`
    : err?.message || 'Unknown error';
  const content = $(`${panelId}-content`);
  if (content) content.innerHTML = stateHtml(message, 'error');
  setPill(panelId, 'Error', 'bad');
}

async function fetchJson(path, options = {}) {
  const res = await fetch(`${API}${path}`, options);
  const text = await res.text();
  let data = null;
  if (text) {
    try { data = JSON.parse(text); }
    catch { data = { raw: text }; }
  }
  if (!res.ok) {
    const message = data?.error || data?.raw || `HTTP ${res.status}`;
    const err = new Error(message);
    err.status = res.status;
    err.payload = data;
    throw err;
  }
  return data ?? {};
}

function infoTable(rows) {
  return `
    <table class="info-table">
      <tbody>
        ${rows.map(([label, value]) => `
          <tr>
            <th>${esc(label)}</th>
            <td>${value}</td>
          </tr>`).join('')}
      </tbody>
    </table>`;
}

function issueList(items, emptyText) {
  if (!items || items.length === 0) return `<div class="state ok">${esc(emptyText)}</div>`;
  return `
    <ul class="issue-list">
      ${items.map((item) => {
        if (typeof item === 'string') return `<li>${esc(item)}</li>`;
        const severity = item.severity ? `${item.severity} ` : '';
        const code = item.code ? `${item.code}: ` : '';
        return `<li>${esc(`${severity}${code}${item.message || ''}`)}</li>`;
      }).join('')}
    </ul>`;
}

function actionLabel(action) {
  if (!action) return '-';
  const domain = action.domain || '-';
  const service = action.service || '-';
  const entity = action.entityId || '-';
  return `<code>${esc(domain)}.${esc(service)}</code> <span class="muted">on</span> <code>${esc(entity)}</code>`;
}

function constraintSummary(goal) {
  const constraints = goal.constraints || {};
  const parts = [];
  if (constraints.preferLowPrice) parts.push('prefer low price');
  if (constraints.maxPriceNokPerKwh !== null && constraints.maxPriceNokPerKwh !== undefined) {
    parts.push(`max ${constraints.maxPriceNokPerKwh} NOK/kWh`);
  }
  parts.push(constraints.dryRun === false ? 'real request' : 'dry-run');
  return parts.join(', ');
}

function persistenceStores(persistence = {}) {
  return [
    ['Goals', persistence.goals],
    ['Decisions', persistence.decisions],
    ['Settings', persistence.settings],
    ['Execution history', persistence.executionHistory],
    ['Safety events', persistence.safetyEvents],
  ];
}

async function loadProductStatus() {
  try {
    const data = await fetchJson('/api/product/status');
    const ha = data.homeAssistant || {};
    const auto = data.autonomous || {};
    const safety = data.safety || {};
    const build = data.build || {};
    const stores = persistenceStores(data.persistence);
    const sqliteCount = stores.filter(([, store]) => store?.provider === 'sqlite').length;
    const providers = [...new Set(stores.map(([, store]) => store?.provider).filter(Boolean))];

    const safetyKind = safety.killSwitchEngaged ? 'bad' : (safety.allowExecution ? 'warn' : 'good');
    const safetyLabel = safety.killSwitchEngaged ? 'Kill switch' : (safety.allowExecution ? 'Armed' : 'Dry-run safe');
    setPill('product', 'Ready', safetyKind);
    setMetric('product', data.appMode || 'unknown', build.version ? `Build ${esc(build.version)}` : 'Build info unavailable');
    setMetric('ha', ha.configured ? 'Configured' : 'Not configured',
      `${ha.connected ? 'connected' : 'not connected'}, source ${esc(ha.source || '-')}`);
    setMetric('safety', safetyLabel,
      `${esc(safety.allowedEntityCount ?? 0)} entities, ${esc(safety.allowedServiceCount ?? 0)} services`);
    setMetric('persistence', sqliteCount ? `${sqliteCount} SQLite` : 'Local',
      providers.length ? providers.map(esc).join(', ') : 'provider unavailable');

    const rows = [
      ['App mode', `<code>${esc(data.appMode || '-')}</code>`],
      ['Home Assistant', ha.configured ? badge('configured', 'ok') : badge('not configured')],
      ['HA connected', ha.connected ? badge('connected', 'ok') : badge('not connected')],
      ['Autonomous requested', auto.requested ? badge('requested', 'warn') : badge('off')],
      ['Real autonomous', auto.realExecutionRequested ? badge('requested', 'warn') : badge('off')],
      ['Autonomous dashboard mode', auto.dryRunOnly ? badge('dry-run only', 'dry') : badge('unknown', 'warn')],
      ['Safety posture', badge(safetyLabel, safetyKind === 'good' ? 'ok' : safetyKind)],
      ['Cooldown seconds', esc(safety.actionCooldownSeconds ?? 0)],
      ['Max actions/hour', esc(limitText(safety.maxActionsPerHour))],
      ['Build', `<code>${esc(build.informationalVersion || build.version || '-')}</code>`],
    ];

    $('product-content').innerHTML = `
      ${infoTable(rows)}
      <div class="subsection-title">Persistence providers</div>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Store</th><th>Provider</th><th>Safe path</th></tr></thead>
          <tbody>
            ${stores.map(([name, store]) => `
              <tr>
                <td>${esc(name)}</td>
                <td><code>${esc(store?.provider || '-')}</code></td>
                <td><code>${esc(store?.path || 'in-memory / local')}</code></td>
              </tr>`).join('')}
          </tbody>
        </table>
      </div>`;
    return data;
  } catch (err) {
    panelError('product', err);
    setMetric('product', 'Unavailable', esc(err.message || 'Product status failed'));
    setMetric('ha', 'Unknown', 'product status unavailable');
    setMetric('safety', 'Unknown', 'product status unavailable');
    setMetric('persistence', 'Unknown', 'product status unavailable');
    throw err;
  }
}

async function loadGoals() {
  try {
    const data = await fetchJson('/api/goals');
    const goals = data.goals || [];
    const active = goals.filter((goal) => goal.enabled).length;
    setPill('goals', `${goals.length} total`, goals.length ? 'good' : 'neutral');
    if (goals.length === 0) {
      $('goals-content').innerHTML = stateHtml('No goals configured.');
      return data;
    }
    $('goals-content').innerHTML = `
      <div class="summary-row">
        <span><strong>${active}</strong> enabled</span>
        <span><strong>${goals.length - active}</strong> disabled</span>
      </div>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>ID</th>
              <th>Enabled</th>
              <th>Type</th>
              <th>Actions</th>
              <th>Constraints</th>
              <th>Toggle</th>
            </tr>
          </thead>
          <tbody>
            ${goals.map((goal) => {
              const nextEnabled = goal.enabled !== true;
              return `
                <tr>
                  <td><code>${esc(goal.id)}</code></td>
                  <td>${goal.enabled ? badge('enabled', 'ok') : badge('disabled')}</td>
                  <td>${esc(goal.type || '-')}</td>
                  <td>${esc((goal.actions || []).length)}</td>
                  <td>${esc(constraintSummary(goal))}</td>
                  <td>
                    <button type="button" class="btn compact secondary"
                      data-action="toggle-goal"
                      data-goal-id="${esc(goal.id)}"
                      data-next-enabled="${nextEnabled}">
                      ${nextEnabled ? 'Enable' : 'Disable'}
                    </button>
                  </td>
                </tr>`;
            }).join('')}
          </tbody>
        </table>
      </div>`;
    return data;
  } catch (err) {
    panelError('goals', err);
    throw err;
  }
}

async function toggleGoal(button) {
  const id = button.dataset.goalId;
  const enabled = button.dataset.nextEnabled === 'true';
  if (!id) return;

  button.disabled = true;
  setPill('goals', 'Updating', 'neutral');
  try {
    await fetchJson(`/api/goals/${encodeURIComponent(id)}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ enabled }),
    });
    await loadGoals();
  } catch (err) {
    setPill('goals', 'Error', 'bad');
    $('goals-content').insertAdjacentHTML('afterbegin', stateHtml(err.message || 'Goal update failed.', 'error'));
  } finally {
    button.disabled = false;
  }
}

async function loadDecisions() {
  try {
    const data = await fetchJson('/api/decisions?limit=10');
    const decisions = data.decisions || [];
    setPill('decisions', `${data.count ?? decisions.length} records`, decisions.length ? 'good' : 'neutral');
    if (decisions.length === 0) {
      $('decisions-content').innerHTML = stateHtml('No decisions recorded yet.');
      return data;
    }
    $('decisions-content').innerHTML = `
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Time</th>
              <th>Goal</th>
              <th>Scenario</th>
              <th>Dry-run</th>
              <th>Actions</th>
              <th>Warnings</th>
            </tr>
          </thead>
          <tbody>
            ${decisions.map((d) => {
              const warnings = d.warnings || d.safetyWarnings || [];
              return `
                <tr>
                  <td>${esc(fmtTime(d.timestamp))}</td>
                  <td><code>${esc(d.goalId || '-')}</code></td>
                  <td><code>${esc(d.selectedScenario || '-')}</code></td>
                  <td>${d.dryRun ? badge('true', 'dry') : badge('false', 'warn')}</td>
                  <td>${esc((d.actions || []).length)}</td>
                  <td>${warnings.length ? esc(warnings.join('; ')) : '0'}</td>
                </tr>`;
            }).join('')}
          </tbody>
        </table>
      </div>`;
    return data;
  } catch (err) {
    panelError('decisions', err);
    throw err;
  }
}

function settingsFormHtml(message = 'Safe settings only. Secret-like keys or values are rejected.') {
  return `
    <div class="settings-form">
      <label class="field-block" for="settings-key">
        <span>Key</span>
        <input id="settings-key" type="text" autocomplete="off" placeholder="dashboard.refreshSeconds" />
      </label>
      <label class="field-block" for="settings-value">
        <span>Value</span>
        <input id="settings-value" type="text" autocomplete="off" placeholder="15" />
      </label>
      <button type="button" class="btn primary" id="save-setting" data-testid="save-setting">
        Save setting
      </button>
      <div class="state" id="settings-form-state">${esc(message)}</div>
    </div>`;
}

async function loadSettings() {
  try {
    const data = await fetchJson('/api/settings');
    const settings = (data.settings || [])
      .filter((setting) => !isSecretLike(setting.key) && !isSecretLike(setting.value));
    setPill('settings', `${settings.length} settings`, settings.length ? 'good' : 'neutral');
    const table = settings.length ? `
      <div class="table-wrap">
        <table>
          <thead><tr><th>Key</th><th>Value</th><th>Updated</th></tr></thead>
          <tbody>
            ${settings.map((setting) => `
              <tr>
                <td><code>${esc(setting.key)}</code></td>
                <td>${esc(setting.value)}</td>
                <td>${esc(fmtTime(setting.updatedAt))}</td>
              </tr>`).join('')}
          </tbody>
        </table>
      </div>` : stateHtml('No non-secret settings stored.');
    $('settings-content').innerHTML = `${settingsFormHtml()}${table}`;
    return data;
  } catch (err) {
    panelError('settings', err);
    throw err;
  }
}

async function saveSetting() {
  const key = $('settings-key')?.value?.trim() || '';
  const value = $('settings-value')?.value ?? '';
  const state = $('settings-form-state');
  const button = $('save-setting');

  if (!key || isSecretLike(key) || isSecretLike(value)) {
    if (state) {
      state.className = 'state error';
      state.textContent = 'Rejected: setting key and value must be non-secret.';
    }
    return;
  }

  button.disabled = true;
  if (state) {
    state.className = 'state';
    state.textContent = 'Saving setting.';
  }
  try {
    await fetchJson('/api/settings', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ key, value }),
    });
    await loadSettings();
    const newState = $('settings-form-state');
    if (newState) {
      newState.className = 'state ok';
      newState.textContent = 'Setting saved.';
    }
  } catch (err) {
    if (state) {
      state.className = 'state error';
      state.textContent = err.message || 'Setting save failed.';
    }
  } finally {
    button.disabled = false;
  }
}

async function loadSafety() {
  try {
    const data = await fetchJson('/api/safety');
    const state = data.state || {};
    const events = data.events || [];

    if (state.killSwitchEngaged) {
      setPill('safety', 'Kill switch ON', 'bad');
    } else if (state.allowExecution) {
      setPill('safety', 'Real execution armed', 'warn');
    } else {
      setPill('safety', 'Safe (dry-run)', 'good');
    }

    const postureHtml = `
      <div class="summary-grid">
        <div><span>Kill switch</span><strong>${state.killSwitchEngaged ? 'engaged' : 'off'}</strong></div>
        <div><span>Real execution</span><strong>${yesNo(state.allowExecution)}</strong></div>
        <div><span>HA token present</span><strong>${yesNo(state.tokenPresent)}</strong></div>
        <div><span>Cooldown (s)</span><strong>${limitText(state.actionCooldownSeconds)}</strong></div>
        <div><span>Max actions/hour</span><strong>${limitText(state.maxActionsPerHour)}</strong></div>
        <div><span>Max/entity/hour</span><strong>${limitText(state.maxActionsPerEntityPerHour)}</strong></div>
        <div><span>Allowlisted entities</span><strong>${esc(state.allowedEntityCount ?? 0)}</strong></div>
        <div><span>Allowlisted services</span><strong>${esc(state.allowedServiceCount ?? 0)}</strong></div>
        <div><span>Audit log</span><strong>${esc(state.eventLogProvider || '-')}</strong></div>
      </div>`;

    const eventsHtml = events.length ? `
      <div class="table-wrap">
        <table>
          <thead><tr><th>Time</th><th>Outcome</th><th>Gate</th><th>Target</th><th>Detail</th></tr></thead>
          <tbody>
            ${events.map((e) => {
              const target = e.entityId ? `${e.domain}.${e.service} on ${e.entityId}` : '-';
              const kind = e.outcome === 'executed' ? 'warn' : (e.outcome === 'failed' ? 'bad' : 'ok');
              return `
                <tr>
                  <td>${esc(fmtTime(e.timestamp))}</td>
                  <td>${badge(e.outcome, kind)}</td>
                  <td><code>${esc(e.gate)}</code></td>
                  <td>${esc(target)}</td>
                  <td>${esc(e.detail)}</td>
                </tr>`;
            }).join('')}
          </tbody>
        </table>
      </div>` : stateHtml('No safety events recorded yet.', 'ok');

    $('safety-content').innerHTML = `
      ${postureHtml}
      <div class="subsection-title">Recent safety events (${esc(data.eventCount ?? events.length)})</div>
      ${eventsHtml}`;
    return data;
  } catch (err) {
    panelError('safety', err);
    throw err;
  }
}

function resetBindingsSample() {
  $('bindings-editor').value = BINDINGS_SAMPLE;
  $('bindings-result').innerHTML = stateHtml('Validation results appear here.');
  const state = $('bindings-form-state');
  state.className = 'state';
  state.textContent = 'Sample loaded.';
  setPill('bindings', 'Not run', 'neutral');
}

function renderBindingsResult(data) {
  const status = data.status || 'UNKNOWN';
  const kind = status === 'PASS' ? 'good' : (status === 'WARN' ? 'warn' : 'bad');
  setPill('bindings', status, kind);
  $('bindings-result').innerHTML = `
    <div class="summary-grid">
      <div><span>Status</span><strong>${esc(status)}</strong></div>
      <div><span>Sensors</span><strong>${esc(data.sensorCount ?? 0)}</strong></div>
      <div><span>Actuators</span><strong>${esc(data.actuatorCount ?? 0)}</strong></div>
      <div><span>Errors</span><strong>${esc(data.errorCount ?? 0)}</strong></div>
      <div><span>Warnings</span><strong>${esc(data.warningCount ?? 0)}</strong></div>
      <div><span>Profile</span><strong>${esc(data.profile || '-')}</strong></div>
    </div>
    <div class="subsection-title">Issues</div>
    ${issueList(data.issues || [], 'No validation issues.')}`;
}

async function validateBindings() {
  const editor = $('bindings-editor');
  const state = $('bindings-form-state');
  const button = $('validate-bindings');
  const body = editor.value.trim();

  if (!body) {
    state.className = 'state error';
    state.textContent = 'Binding JSON is required.';
    return;
  }

  button.disabled = true;
  state.className = 'state';
  state.textContent = 'Validating bindings.';
  setPill('bindings', 'Running', 'neutral');
  try {
    const data = await fetchJson('/api/ha/bindings/validate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body,
    });
    state.className = data.valid ? 'state ok' : 'state error';
    state.textContent = data.valid ? 'Validation completed.' : 'Validation completed with failures.';
    renderBindingsResult(data);
  } catch (err) {
    setPill('bindings', 'Error', 'bad');
    state.className = 'state error';
    state.textContent = err.message || 'Binding validation failed.';
    $('bindings-result').innerHTML = stateHtml(err.message || 'Binding validation failed.', 'error');
  } finally {
    button.disabled = false;
  }
}

function renderTick(data) {
  const scenarios = data.simulatedScenarios || [];
  const selected = data.selectedPlan || {};
  const actions = data.actions || selected.actions || [];
  const observed = data.observedState || {};
  const analysis = data.analysis || {};
  const findings = analysis.findings || [];
  const selectedScenario = selected.scenarioId || data.decision?.selectedScenario || '-';

  setPill('tick', data.dryRun ? 'Dry-run complete' : 'Execution blocked', data.dryRun ? 'dry' : 'warn');

  $('tick-content').innerHTML = `
    <div class="tick-layout">
      <div class="tick-main">
        <div class="dry-banner">DRY-RUN result. Staged actions remain executed:false.</div>
        ${infoTable([
          ['Timestamp', esc(fmtTime(data.timestamp))],
          ['Selected scenario', `<code>${esc(selectedScenario)}</code>`],
          ['Selected plan', `<code>${esc(selectedScenario)}</code>`],
          ['Rationale', esc(selected.rationale || data.explanation || '-')],
          ['Observed price', `${fmtNumber(observed.currentPriceNokPerKwh, 3)} NOK/kWh`],
          ['Active goals', esc((data.activeGoals || []).length)],
          ['Executed', actions.some((a) => a.executed) ? badge('true', 'warn') : badge('false', 'ok')],
        ])}
      </div>
      <div class="tick-side">
        <div class="subsection-title">Warnings</div>
        ${issueList([...(data.warnings || []), ...(observed.warnings || [])], 'No warnings.')}
      </div>
    </div>

    <div class="subsection-title">Scenario scores</div>
    <div class="scenario-grid">
      ${scenarios.length ? scenarios.map((scenario) => {
        const active = scenario.scenarioId === selectedScenario;
        return `
          <article class="scenario-card ${active ? 'selected' : ''}">
            <span>${active ? 'Selected' : 'Scenario'}</span>
            <strong>${esc(scenario.scenarioId)}</strong>
            <b>${fmtNumber(scenario.score, 2)}</b>
            <p>${esc(scenario.description || '')}</p>
          </article>`;
      }).join('') : stateHtml('No scenarios returned.')}
    </div>

    <div class="subsection-title">Staged actions</div>
    ${actions.length ? `
      <div class="table-wrap">
        <table>
          <thead><tr><th>Action</th><th>Executed</th><th>Payload</th></tr></thead>
          <tbody>
            ${actions.map((action) => `
              <tr>
                <td>${actionLabel(action)}</td>
                <td>${action.executed ? badge('true', 'warn') : badge('false', 'ok')}</td>
                <td><code>${esc(JSON.stringify(redactValue(action.data || {})))}</code></td>
              </tr>`).join('')}
          </tbody>
        </table>
      </div>` : stateHtml('No actions staged for this plan.', 'ok')}

    <div class="subsection-title">Analysis findings</div>
    ${findings.length ? issueList(findings.map((f) => `${f.severity || 'info'} ${f.code || ''}: ${f.message || ''}`), '') : stateHtml('No analysis findings.', 'ok')}`;
}

async function runDryTick() {
  const btn = $('run-tick');
  btn.disabled = true;
  $('tick-content').innerHTML = stateHtml('Running dry-run tick.');
  try {
    const data = await fetchJson('/api/mapek/tick', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ dryRun: true }),
    });
    renderTick(data);
    await loadDecisions();
  } catch (err) {
    $('tick-content').innerHTML = stateHtml(err.message || 'Dry-run tick failed.', 'error');
    setPill('tick', 'Error', 'bad');
  } finally {
    btn.disabled = false;
  }
}

async function refreshAll() {
  $('api-base').textContent = API;
  setPill('product', 'Loading', 'neutral');
  setPill('goals', 'Loading', 'neutral');
  setPill('decisions', 'Loading', 'neutral');
  setPill('settings', 'Loading', 'neutral');
  setPill('safety', 'Loading', 'neutral');

  await Promise.allSettled([
    loadProductStatus(),
    loadGoals(),
    loadDecisions(),
    loadSettings(),
    loadSafety(),
  ]);
}

document.addEventListener('click', (event) => {
  const toggle = event.target.closest('[data-action="toggle-goal"]');
  if (toggle) {
    toggleGoal(toggle);
    return;
  }
  if (event.target.id === 'save-setting') {
    saveSetting();
    return;
  }
  if (event.target.id === 'validate-bindings') {
    validateBindings();
    return;
  }
  if (event.target.id === 'reset-bindings-sample') {
    resetBindingsSample();
  }
});

$('refresh').addEventListener('click', refreshAll);
$('run-tick').addEventListener('click', runDryTick);

$('bindings-editor').value = BINDINGS_SAMPLE;
$('api-base').textContent = API;
refreshAll();
