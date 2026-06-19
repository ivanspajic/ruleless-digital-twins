// Home Assistant discovery setup: select entities, generate a review-only draft,
// export runtime bindings, and let the browser download/copy the local config.
(() => {
  const api = 'http://localhost:8080';
  const downloadFileName = 'ha-bindings.discovery-selection.json';
  const placeholderBindingsPath = 'C:\\path\\to\\ha-bindings.discovery-selection.json';
  const $ = (id) => document.getElementById(id);
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  let latestDiscovery = null;
  let latestDraft = null;
  let latestDraftText = '';
  let latestRuntimeConfigText = '';
  let latestRunCommandsText = '';
  const selectedEntityIds = new Set();

  function setState(msg, kind) {
    const el = $('discovery-state');
    if (!el) return;
    el.className = `state${kind ? ' ' + kind : ''}`;
    el.textContent = msg || '';
  }

  function setRuntimeState(msg, kind) {
    const el = $('runtime-export-state');
    if (!el) return;
    el.className = `state${kind ? ' ' + kind : ''}`;
    el.textContent = msg || '';
  }

  function renderRuntimeNotes(body) {
    const notes = $('runtime-export-notes');
    if (!notes) return;

    const warnings = Array.isArray(body?.warnings) ? body.warnings : [];
    const issues = Array.isArray(body?.validation?.issues) ? body.validation.issues : [];
    const validationMessages = issues
      .filter((issue) => issue && issue.severity !== 0)
      .map((issue) => `${issue.code}: ${issue.message}`);
    const lines = warnings.concat(validationMessages);

    notes.innerHTML = lines.map((line) => `<div class="warning">${esc(line)}</div>`).join('');
  }

  function currentHaUrl() {
    const input = $('url');
    const value = input && input.value ? input.value.trim() : '';
    return value || 'http://localhost:8123';
  }

  function runCommands(mode) {
    return [
      `$env:SMARTNODE_MODE = "${mode}"`,
      `$env:HA_URL = "${currentHaUrl()}"`,
      '$env:TOKEN_HA = "<your_long_lived_access_token>"',
      `$env:HA_BINDINGS_FILE = "${placeholderBindingsPath}"`,
      'dotnet run --project .\\SmartNode\\SmartNode\\SmartNode.csproj'
    ].join('\n');
  }

  function renderRunInstructions() {
    const full = runCommands('full');
    const chatbox = runCommands('chatbox-only');
    const fullEl = $('runtime-run-full');
    const chatboxEl = $('runtime-run-chatbox');
    const panel = $('runtime-run-instructions');

    if (fullEl) fullEl.textContent = full;
    if (chatboxEl) chatboxEl.textContent = chatbox;
    if (panel) panel.hidden = false;

    latestRunCommandsText = [
      '# Full mode',
      full,
      '',
      '# API-only mode',
      chatbox
    ].join('\n');
  }

  function entityMeta(entityId) {
    const groups = latestDiscovery?.groups || [];
    for (const group of groups) {
      for (const entity of group.entities || []) {
        if (entity.entityId === entityId) {
          return { group, entity };
        }
      }
    }
    return null;
  }

  function selectedCounts() {
    let observables = 0;
    let actuators = 0;
    for (const entityId of selectedEntityIds) {
      const meta = entityMeta(entityId);
      if (!meta) continue;
      if (meta.group.role === 'observable') observables += 1;
      if (meta.group.role === 'actuator') actuators += 1;
    }
    return { selected: selectedEntityIds.size, observables, actuators };
  }

  function updateSelectionSummary() {
    const summary = $('selection-summary');
    const generate = $('generate-draft');
    const copy = $('copy-draft');
    const exportRuntime = $('export-runtime-config');
    const copyRuntime = $('copy-runtime-config');
    const downloadRuntime = $('download-runtime-config');
    const copyRunCommands = $('copy-run-commands');
    const counts = selectedCounts();

    if (summary) {
      summary.innerHTML = `
        <span class="pill">selected: ${esc(counts.selected)}</span>
        <span class="pill">observables: ${esc(counts.observables)}</span>
        <span class="pill">actuators: ${esc(counts.actuators)}</span>`;
    }
    if (generate) generate.disabled = counts.selected === 0;
    if (copy) copy.disabled = !latestDraftText;
    if (exportRuntime) exportRuntime.disabled = !latestDraft;
    if (copyRuntime) copyRuntime.disabled = !latestRuntimeConfigText;
    if (downloadRuntime) downloadRuntime.disabled = !latestRuntimeConfigText;
    if (copyRunCommands) copyRunCommands.disabled = !latestRunCommandsText;
  }

  function clearRuntimeExport() {
    latestRuntimeConfigText = '';
    latestRunCommandsText = '';
    const output = $('runtime-export-output');
    const pre = $('runtime-config-json');
    const notes = $('runtime-export-notes');
    const state = $('runtime-export-state');
    const instructions = $('runtime-run-instructions');
    const full = $('runtime-run-full');
    const chatbox = $('runtime-run-chatbox');
    if (pre) pre.textContent = '';
    if (notes) notes.innerHTML = '';
    if (full) full.textContent = '';
    if (chatbox) chatbox.textContent = '';
    if (instructions) instructions.hidden = true;
    if (state) {
      state.className = 'state';
      state.textContent = '';
    }
    if (output) output.classList.remove('visible');
    const loadExportedBtn = $('wizard-load-exported-bindings');
    if (loadExportedBtn) loadExportedBtn.disabled = true;
  }

  function clearDraft() {
    latestDraft = null;
    latestDraftText = '';
    clearRuntimeExport();
    const output = $('draft-output');
    const pre = $('draft-json');
    if (pre) pre.textContent = '';
    if (output) output.classList.remove('visible');
    updateSelectionSummary();
  }

  function capRows(caps) {
    const entries = Object.entries(caps || {});
    if (!entries.length) return '';
    return `
      <div class="caps">
        ${entries.map(([name, ok]) =>
          `<span class="pill cap ${ok ? 'ok' : 'missing'}">${esc(name)}: ${ok ? 'yes' : 'no'}</span>`
        ).join('')}
      </div>`;
  }

  function entityTable(group) {
    const entities = group.entities || [];
    if (!entities.length) return '';
    return `
      <table>
        <thead><tr><th class="select-cell"></th><th>Entity</th><th>Name</th><th>State</th></tr></thead>
        <tbody>
          ${entities.map((e) => `
            <tr>
              <td class="select-cell">
                <input type="checkbox" class="entity-select" aria-label="Select ${esc(e.entityId)}" data-entity-id="${esc(e.entityId)}">
              </td>
              <td><code>${esc(e.entityId)}</code></td>
              <td>${esc(e.friendlyName)}</td>
              <td>${esc(e.state)}</td>
            </tr>`).join('')}
        </tbody>
      </table>`;
  }

  function render(dto) {
    const result = $('discovery-result');
    if (!result) return;

    latestDiscovery = dto;
    selectedEntityIds.clear();
    clearDraft();

    const domains = dto.domains || [];
    const groups = dto.groups || [];
    const warnings = dto.warnings || [];

    result.innerHTML = `
      <div class="summary">
        <span class="pill">total: ${esc(dto.totalEntities ?? 0)}</span>
        ${domains.map((d) =>
          `<span class="pill">${esc(d.domain)} (${esc(d.role)}): ${esc(d.entityCount)}</span>`
        ).join('')}
      </div>
      ${warnings.map((w) => `<div class="warning">${esc(w)}</div>`).join('')}
      ${groups.map((g) => `
        <section class="group">
          <h3>${esc(g.domain)} <span class="pill">${esc(g.role)}</span></h3>
          ${g.services && g.services.length
            ? `<div class="state">services: ${g.services.map(esc).join(', ')}</div>`
            : ''}
          ${capRows(g.capabilities)}
          ${(g.warnings || []).map((w) => `<div class="warning">${esc(w)}</div>`).join('')}
          ${entityTable(g)}
        </section>`).join('')}
    `;
    updateSelectionSummary();
  }

  async function runDiscovery() {
    const btn = $('run-discovery');
    if (btn) btn.disabled = true;
    setState('Running discovery...');
    const result = $('discovery-result');
    if (result) result.innerHTML = '';
    latestDiscovery = null;
    selectedEntityIds.clear();
    clearDraft();

    try {
      const res = await fetch(`${api}/api/ha/discovery`);
      let body = null;
      try { body = await res.json(); } catch { /* no json */ }
      if (!res.ok) {
        const msg = (body && body.error) ? body.error : `HTTP ${res.status}`;
        setState(`Error: ${msg}`, 'error');
        return;
      }
      render(body);
      setState('Discovery complete.', 'ok');
    } catch (err) {
      const msg = (err && err.message === 'Failed to fetch')
        ? 'SmartNode unreachable at http://localhost:8080'
        : `Error: ${err && err.message}`;
      setState(msg, 'error');
    } finally {
      if (btn) btn.disabled = false;
    }
  }

  async function generateDraft() {
    if (selectedEntityIds.size === 0) {
      setState('Select at least one entity.', 'error');
      return;
    }

    const btn = $('generate-draft');
    if (btn) btn.disabled = true;
    clearDraft();
    setState('Generating draft...');

    try {
      const res = await fetch(`${api}/api/ha/discovery/draft`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ selectedEntityIds: Array.from(selectedEntityIds).sort() }),
      });
      let body = null;
      try { body = await res.json(); } catch { /* no json */ }
      if (!res.ok) {
        const msg = (body && body.error) ? body.error : `HTTP ${res.status}`;
        setState(`Error: ${msg}`, 'error');
        return;
      }

      latestDraft = body;
      latestDraftText = JSON.stringify(body, null, 2);
      const pre = $('draft-json');
      const output = $('draft-output');
      if (pre) pre.textContent = latestDraftText;
      if (output) output.classList.add('visible');
      setState('Draft generated.', 'ok');
    } catch (err) {
      const msg = (err && err.message === 'Failed to fetch')
        ? 'SmartNode unreachable at http://localhost:8080'
        : `Error: ${err && err.message}`;
      setState(msg, 'error');
    } finally {
      if (btn) btn.disabled = selectedEntityIds.size === 0;
      updateSelectionSummary();
    }
  }

  async function copyDraft() {
    if (!latestDraftText) return;
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        await navigator.clipboard.writeText(latestDraftText);
      } else {
        fallbackCopy(latestDraftText);
      }
      setState('Draft copied.', 'ok');
    } catch {
      try {
        fallbackCopy(latestDraftText);
        setState('Draft copied.', 'ok');
      } catch {
        setState('Copy failed; select the JSON manually.', 'error');
      }
    }
  }

  async function exportRuntimeConfig() {
    if (!latestDraft) {
      setState('Generate a binding draft first.', 'error');
      return;
    }

    const btn = $('export-runtime-config');
    if (btn) btn.disabled = true;
    clearRuntimeExport();
    updateSelectionSummary();
    setRuntimeState('Exporting runtime config...');
    setState('Exporting runtime config...');

    try {
      const res = await fetch(`${api}/api/ha/discovery/draft/export`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ draft: latestDraft }),
      });
      let body = null;
      try { body = await res.json(); } catch { /* no json */ }
      if (!res.ok) {
        const msg = (body && body.error) ? body.error : `HTTP ${res.status}`;
        setRuntimeState(`Error: ${msg}`, 'error');
        setState(`Error: ${msg}`, 'error');
        return;
      }

      latestRuntimeConfigText = JSON.stringify(body.config || {}, null, 2);
      const pre = $('runtime-config-json');
      const output = $('runtime-export-output');
      const counts = body.counts || {};
      const validation = body.validation || {};
      const exportWarnings = Array.isArray(body.warnings) ? body.warnings.length : (counts.warnings || 0);
      const status = validation.status || 'UNKNOWN';
      if (pre) pre.textContent = latestRuntimeConfigText;
      if (output) output.classList.add('visible');
      renderRuntimeNotes(body);
      renderRunInstructions();
      const bindingsEditor = $('wizard-bindings-editor');
      if (bindingsEditor) bindingsEditor.value = latestRuntimeConfigText;
      const loadExportedBtn = $('wizard-load-exported-bindings');
      if (loadExportedBtn) loadExportedBtn.disabled = false;
      setRuntimeState(
        `Validation ${status}; sensors: ${counts.sensors ?? 0}; actuators: ${counts.actuators ?? 0}; ` +
        `skipped unsupported actuators: ${counts.skippedUnsupportedActuators ?? 0}; ` +
        `export warnings: ${exportWarnings}; validation errors: ${validation.errorCount ?? 0}; ` +
        `validation warnings: ${validation.warningCount ?? 0}.`,
        validation.hasFailures ? 'error' : 'ok');
      setState('Runtime config export ready.', validation.hasFailures ? 'error' : 'ok');
    } catch (err) {
      const msg = (err && err.message === 'Failed to fetch')
        ? 'SmartNode unreachable at http://localhost:8080'
        : `Error: ${err && err.message}`;
      setRuntimeState(msg, 'error');
      setState(msg, 'error');
    } finally {
      if (btn) btn.disabled = !latestDraft;
      updateSelectionSummary();
    }
  }

  async function copyRuntimeConfig() {
    if (!latestRuntimeConfigText) return;
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        await navigator.clipboard.writeText(latestRuntimeConfigText);
      } else {
        fallbackCopy(latestRuntimeConfigText);
      }
      setRuntimeState('Runtime config copied.', 'ok');
      setState('Runtime config copied.', 'ok');
    } catch {
      try {
        fallbackCopy(latestRuntimeConfigText);
        setRuntimeState('Runtime config copied.', 'ok');
        setState('Runtime config copied.', 'ok');
      } catch {
        setRuntimeState('Copy failed; select the runtime config JSON manually.', 'error');
        setState('Copy failed; select the runtime config JSON manually.', 'error');
      }
    }
  }

  function downloadRuntimeConfig() {
    if (!latestRuntimeConfigText) return;

    const blob = new Blob([latestRuntimeConfigText + '\n'], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = downloadFileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(() => URL.revokeObjectURL(url), 0);

    setRuntimeState(`Runtime config download started: ${downloadFileName}`, 'ok');
    setState('Runtime config download started.', 'ok');
  }

  async function copyRunCommands() {
    if (!latestRunCommandsText) return;
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        await navigator.clipboard.writeText(latestRunCommandsText);
      } else {
        fallbackCopy(latestRunCommandsText);
      }
      setRuntimeState('Run commands copied.', 'ok');
      setState('Run commands copied.', 'ok');
    } catch {
      try {
        fallbackCopy(latestRunCommandsText);
        setRuntimeState('Run commands copied.', 'ok');
        setState('Run commands copied.', 'ok');
      } catch {
        setRuntimeState('Copy failed; select the commands manually.', 'error');
        setState('Copy failed; select the commands manually.', 'error');
      }
    }
  }

  function fallbackCopy(text) {
    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.left = '-9999px';
    document.body.appendChild(area);
    area.select();
    const ok = document.execCommand('copy');
    document.body.removeChild(area);
    if (!ok) throw new Error('copy command failed');
  }

  function setBindingsState(msg, kind) {
    const el = $('wizard-bindings-validate-state');
    if (!el) return;
    el.className = `state${kind ? ' ' + kind : ''}`;
    el.textContent = msg || '';
  }

  function bindingsStatusKind(status) {
    if (status === 'FAIL') return 'error';
    if (status === 'WARN') return 'warn';
    return 'ok';
  }

  function renderBindingsValidation(data) {
    const out = $('wizard-bindings-validate-result');
    if (!out) return;
    const issues = Array.isArray(data.issues) ? data.issues : [];
    const issuesHtml = issues.length
      ? `<table>
           <thead><tr><th>Severity</th><th>Code</th><th>Message</th></tr></thead>
           <tbody>${issues.map((i) =>
             `<tr><td>${esc(i.severity)}</td><td><code>${esc(i.code)}</code></td><td>${esc(i.message)}</td></tr>`
           ).join('')}</tbody>
         </table>`
      : '<div class="state">No validation issues.</div>';
    out.innerHTML = `
      <div class="summary">
        <span class="pill cap ${data.status === 'PASS' ? 'ok' : 'missing'}">status: ${esc(data.status || 'UNKNOWN')}</span>
        <span class="pill">profile: ${esc(data.profile || '—')}</span>
        <span class="pill">sensors: ${esc(data.sensorCount ?? 0)}</span>
        <span class="pill">actuators: ${esc(data.actuatorCount ?? 0)}</span>
        <span class="pill">errors: ${esc(data.errorCount ?? 0)}</span>
        <span class="pill">warnings: ${esc(data.warningCount ?? 0)}</span>
      </div>
      ${issuesHtml}`;
  }

  // Validate edited binding JSON inline using the existing offline endpoint.
  // Validation only — this never saves/adopts the bindings (deferred, see docs).
  async function validateEditedBindings() {
    const editor = $('wizard-bindings-editor');
    const btn = $('wizard-validate-bindings');
    const out = $('wizard-bindings-validate-result');
    const body = editor && editor.value ? editor.value.trim() : '';
    if (out) out.innerHTML = '';
    if (!body) {
      setBindingsState('Paste or load binding JSON first.', 'error');
      return;
    }
    if (btn) btn.disabled = true;
    setBindingsState('Validating bindings...');
    try {
      const res = await fetch(`${api}/api/ha/bindings/validate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body,
      });
      let data = null;
      try { data = await res.json(); } catch { /* no json */ }
      if (!res.ok) {
        const msg = (data && data.error) ? data.error : `HTTP ${res.status}`;
        setBindingsState(`Error: ${msg}`, 'error');
        return;
      }
      renderBindingsValidation(data);
      setBindingsState(
        `Validation ${data.status}. Validation only - saving/adoption is deferred.`,
        bindingsStatusKind(data.status));
    } catch (err) {
      const msg = (err && err.message === 'Failed to fetch')
        ? 'SmartNode unreachable at http://localhost:8080'
        : `Error: ${err && err.message}`;
      setBindingsState(msg, 'error');
    } finally {
      if (btn) btn.disabled = false;
    }
  }

  function loadExportedBindings() {
    const editor = $('wizard-bindings-editor');
    if (!editor || !latestRuntimeConfigText) return;
    editor.value = latestRuntimeConfigText;
    setBindingsState('Loaded exported runtime config into the editor.', 'ok');
  }

  const result = $('discovery-result');
  if (result) {
    result.addEventListener('change', (ev) => {
      const input = ev.target;
      if (!input || !input.classList || !input.classList.contains('entity-select')) return;
      const entityId = input.getAttribute('data-entity-id');
      if (!entityId) return;
      if (input.checked) selectedEntityIds.add(entityId);
      else selectedEntityIds.delete(entityId);
      clearDraft();
      updateSelectionSummary();
    });
  }

  const runBtn = $('run-discovery');
  const draftBtn = $('generate-draft');
  const copyBtn = $('copy-draft');
  const exportBtn = $('export-runtime-config');
  const copyRuntimeBtn = $('copy-runtime-config');
  const downloadRuntimeBtn = $('download-runtime-config');
  const copyRunCommandsBtn = $('copy-run-commands');
  if (runBtn) runBtn.addEventListener('click', runDiscovery);
  if (draftBtn) draftBtn.addEventListener('click', generateDraft);
  if (copyBtn) copyBtn.addEventListener('click', copyDraft);
  if (exportBtn) exportBtn.addEventListener('click', exportRuntimeConfig);
  if (copyRuntimeBtn) copyRuntimeBtn.addEventListener('click', copyRuntimeConfig);
  if (downloadRuntimeBtn) downloadRuntimeBtn.addEventListener('click', downloadRuntimeConfig);
  if (copyRunCommandsBtn) copyRunCommandsBtn.addEventListener('click', copyRunCommands);

  const validateBindingsBtn = $('wizard-validate-bindings');
  const loadExportedBindingsBtn = $('wizard-load-exported-bindings');
  if (validateBindingsBtn) validateBindingsBtn.addEventListener('click', validateEditedBindings);
  if (loadExportedBindingsBtn) loadExportedBindingsBtn.addEventListener('click', loadExportedBindings);

  updateSelectionSummary();
})();
