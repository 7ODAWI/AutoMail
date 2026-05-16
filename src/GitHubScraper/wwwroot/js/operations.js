// ── Tag Input ─────────────────────────────────────────────────────────────────
/**
 * initTagInput(containerId, inputId, tagListId, hiddenInputId)
 * Wires up a tag-input widget:
 *  - Enter or comma adds a tag
 *  - Clicking × on a badge removes it
 *  - Hidden input is kept in sync as comma-separated values
 */
function initTagInput(containerId, inputId, tagListId, hiddenInputId) {
    const container = document.getElementById(containerId);
    const textInput = document.getElementById(inputId);
    const tagList   = document.getElementById(tagListId);
    const hidden    = document.getElementById(hiddenInputId);

    if (!container || !textInput || !tagList || !hidden) return;

    const tags = new Set();

    function sync() {
        hidden.value = [...tags].join(',');
    }

    function addTag(value) {
        const v = value.trim().replace(/,/g, '');
        if (!v || tags.has(v)) return;
        tags.add(v);

        const badge = document.createElement('span');
        badge.className = 'tag-badge';
        badge.innerHTML = `${escapeHtml(v)} <span class="tag-remove" title="Remove">&#x2715;</span>`;
        badge.querySelector('.tag-remove').addEventListener('click', () => {
            tags.delete(v);
            badge.remove();
            sync();
        });
        tagList.appendChild(badge);
        sync();
    }

    textInput.addEventListener('keydown', e => {
        if (e.key === 'Enter' || e.key === ',') {
            e.preventDefault();
            addTag(textInput.value);
            textInput.value = '';
        } else if (e.key === 'Backspace' && textInput.value === '') {
            const lastBadge = tagList.lastElementChild;
            if (lastBadge) {
                const lastTag = lastBadge.textContent.trim().replace('✕', '').trim();
                tags.delete(lastTag);
                lastBadge.remove();
                sync();
            }
        }
    });

    textInput.addEventListener('blur', () => {
        if (textInput.value.trim()) {
            addTag(textInput.value);
            textInput.value = '';
        }
    });

    container.addEventListener('click', () => textInput.focus());

    // Restore existing tags from hidden field (e.g. on validation failure)
    if (hidden.value) {
        hidden.value.split(',').forEach(t => { if (t.trim()) addTag(t.trim()); });
    }
}

function escapeHtml(str) {
    return str
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

// ── Confirm dialog ────────────────────────────────────────────────────────────
function confirmAction(message, form) {
    if (!confirm(message)) return false;
    if (form) form.submit();
    return false; // prevent default, form submitted above
}

// ── Dashboard polling (Index page) ────────────────────────────────────────────
/**
 * Polls /api/operations/summary every intervalMs and updates card stats in-place.
 * Only refreshes cards that are on the page (by id op-card-{id}).
 */
function startDashboardPolling(intervalMs) {
    const poll = () => {
        fetch('/api/operations/summary')
            .then(r => r.json())
            .then(data => {
                data.forEach(op => {
                    updateEl(`emails-${op.id}`,   op.emailsFound);
                    updateEl(`profiles-${op.id}`, op.profilesScanned);
                    updateEl(`failures-${op.id}`, op.failures);

                    const badge = document.getElementById(`badge-${op.id}`);
                    if (badge) {
                        badge.className = `badge status-badge status-${op.status.toLowerCase()}`;
                        badge.textContent = op.status;
                        if (op.status === 'Running') {
                            const spinner = document.createElement('span');
                            spinner.className = 'spinner-border spinner-border-sm me-1';
                            spinner.setAttribute('role', 'status');
                            badge.prepend(spinner);
                        }
                    }

                    const userEl = document.getElementById(`current-user-${op.id}`);
                    if (userEl) {
                        userEl.textContent = op.currentUser
                            ? `\u{1F4CB} ${op.currentUser}`
                            : '';
                        userEl.style.display = op.currentUser ? 'block' : 'none';
                    }
                });
            })
            .catch(() => {});
    };

    poll();
    setInterval(poll, intervalMs);
}

// ── Detail page stats polling ─────────────────────────────────────────────────
/**
 * Polls /api/operations/{id}/stats every intervalMs and updates the stat cards.
 * Stops polling when status is no longer Running.
 */
function startStatsPolling(operationId, intervalMs) {
    let handle = null;

    const poll = () => {
        fetch(`/api/operations/${operationId}/stats`)
            .then(r => r.json())
            .then(data => {
                updateEl('stat-emails',    data.emailsFound);
                updateEl('stat-profiles',  data.profilesScanned);
                updateEl('stat-failures',  data.failures);
                updateEl('stat-ratelimit', data.rateLimitRemaining >= 0 ? data.rateLimitRemaining : '—');

                // Update running alert
                const alertBox = document.getElementById('running-alert');
                if (alertBox) {
                    if (data.status !== 'Running') {
                        alertBox.style.display = 'none';
                    } else {
                        updateEl('live-current-user',  data.currentUser || '');
                        updateEl('live-current-query', data.currentQuery || '');
                    }
                }

                // Update page badge
                const badge = document.getElementById('page-status-badge');
                if (badge) {
                    badge.className = `badge status-badge status-${data.status.toLowerCase()} fs-6`;
                    badge.textContent = data.status;
                    if (data.status === 'Running') {
                        const spinner = document.createElement('span');
                        spinner.className = 'spinner-border spinner-border-sm me-1';
                        spinner.setAttribute('role', 'status');
                        badge.prepend(spinner);
                    }
                }

                // Also refresh results table while running
                if (data.status === 'Running') {
                    loadResults(operationId, 1,
                        document.getElementById('results-search')?.value || '');
                }

                // Stop polling once no longer running
                if (data.status !== 'Running' && handle !== null) {
                    clearInterval(handle);
                    handle = null;
                }
            })
            .catch(() => {});
    };

    poll();
    handle = setInterval(poll, intervalMs);
}

// ── Results table AJAX loader ─────────────────────────────────────────────────
function loadResults(operationId, page, query) {
    const url = `/api/operations/${operationId}/results?page=${page}&pageSize=50&q=${encodeURIComponent(query || '')}`;

    fetch(url)
        .then(r => r.json())
        .then(data => {
            const tbody = document.getElementById('results-tbody');
            if (!tbody) return;

            updateEl('results-count', data.totalCount);

            if (data.items.length === 0) {
                tbody.innerHTML = `
                    <tr><td colspan="7" class="text-center py-4 text-muted">
                        <i class="bi bi-inbox me-2"></i>No results found
                    </td></tr>`;
                return;
            }

            tbody.innerHTML = data.items.map(r => `
                <tr>
                    <td>
                        <a href="${r.profileUrl || '#'}" target="_blank" rel="noopener noreferrer"
                           class="text-decoration-none">
                            <i class="bi bi-github me-1 text-muted"></i>${escapeHtml(r.username)}
                        </a>
                    </td>
                    <td class="text-muted small">${escapeHtml(r.name || '')}</td>
                    <td><a href="mailto:${escapeHtml(r.email)}" class="text-decoration-none">${escapeHtml(r.email)}</a></td>
                    <td>
                        <span class="badge confidence-${(r.emailConfidence || 'low').toLowerCase()}">
                            ${escapeHtml(r.emailConfidence || 'Low')}
                        </span>
                    </td>
                    <td class="text-muted small">${escapeHtml(r.location || '')}</td>
                    <td class="text-muted small">${(r.followers || 0).toLocaleString()}</td>
                    <td class="text-muted small">${formatDate(r.foundAtUtc)}</td>
                </tr>`).join('');
        })
        .catch(() => {});
}

// ── Helpers ───────────────────────────────────────────────────────────────────
function updateEl(id, value) {
    const el = document.getElementById(id);
    if (el && el.textContent !== String(value)) el.textContent = value;
}

function formatDate(isoString) {
    if (!isoString) return '';
    const d = new Date(isoString);
    return d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}
