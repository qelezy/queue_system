(function () {
    'use strict';

    var modalId = 'report-workflow-modal';

    var reportCustomConfig = {
        'load-and-downtime': [
            {
                key: 'analysisMode',
                label: 'Срез',
                type: 'select',
                options: [
                    { value: 'doctor', label: 'По врачам' },
                    { value: 'cabinet', label: 'По кабинетам' }
                ]
            }
        ],
        'service-categories-comparison': [
            {
                key: 'categoryId',
                label: 'Категория',
                type: 'select-dynamic',
                source: 'categoryOptions',
                placeholderLabel: 'Все категории'
            }
        ],
        'queue-summary': [
            { key: 'cabinetId', label: 'Кабинет', type: 'select-dynamic', source: 'cabinetOptions' },
            { key: 'doctorId', label: 'Врач', type: 'select-dynamic', source: 'doctorOptions' }
        ],
        'cabinet-load': [
            { key: 'weekStart', label: 'Неделя с (понедельник)', type: 'date' }
        ]
    };

    var state = {
        selectedReportId: '',
        titlesById: {},
        filters: {
            periodFrom: '',
            periodTo: '',
            customByReport: {}
        },
        options: { cabinetOptions: [], doctorOptions: [], categoryOptions: [] },
        lastResult: null,
        loadPreviewChart: null
    };

    function getModal() {
        return document.getElementById(modalId);
    }

    function setModalHiddenState(modal, isHidden) {
        if (!modal) return;
        modal.setAttribute('aria-hidden', isHidden ? 'true' : 'false');
        if ('inert' in modal) {
            modal.inert = isHidden;
        }
    }

    var lastFocusedElement = null;

    function setWorkflowView(mode) {
        var modal = getModal();
        if (!modal) return;
        if (mode === 'preview') {
            modal.classList.add('is-preview');
        } else {
            modal.classList.remove('is-preview');
        }
    }

    function openWorkflowModal() {
        var modal = getModal();
        if (!modal) return;
        lastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        setWorkflowView('params');
        modal.classList.add('is-open');
        setModalHiddenState(modal, false);
    }

    function closeWorkflowModal() {
        var modal = getModal();
        if (!modal) return;
        var active = document.activeElement;
        if (active instanceof HTMLElement && modal.contains(active)) {
            active.blur();
        }
        modal.classList.remove('is-open');
        modal.classList.remove('is-preview');
        setModalHiddenState(modal, true);
        clearGenerateStatus();
        destroyLoadPreviewChart();
        if (lastFocusedElement && document.contains(lastFocusedElement)) {
            lastFocusedElement.focus();
        }
        lastFocusedElement = null;
    }

    function readInitialState() {
        var node = document.getElementById('reports-page-data');
        if (!node) return;
        try {
            var payload = JSON.parse(node.textContent || '{}');
            state.selectedReportId = payload.selectedReportId || '';
            state.filters.periodFrom = toDateTimeLocal(payload.toolbar && payload.toolbar.dateFrom ? payload.toolbar.dateFrom : '');
            state.filters.periodTo = toDateTimeLocal(payload.toolbar && payload.toolbar.dateTo ? payload.toolbar.dateTo : '');
            state.options.cabinetOptions = payload.toolbar && payload.toolbar.cabinetOptions ? payload.toolbar.cabinetOptions : [];
            state.options.doctorOptions = payload.toolbar && payload.toolbar.doctorOptions ? payload.toolbar.doctorOptions : [];
            state.options.categoryOptions = payload.toolbar && payload.toolbar.categoryOptions ? payload.toolbar.categoryOptions : [];
            state.filters.customByReport['queue-summary'] = {
                cabinetId: payload.toolbar && payload.toolbar.cabinetId != null ? String(payload.toolbar.cabinetId) : '',
                doctorId: payload.toolbar && payload.toolbar.doctorId != null ? String(payload.toolbar.doctorId) : ''
            };
            state.filters.customByReport['cabinet-load'] = {
                weekStart: payload.toolbar && payload.toolbar.weekStart ? payload.toolbar.weekStart : ''
            };
        } catch (_) {
            state.selectedReportId = '';
        }
    }

    function toDateTimeLocal(raw) {
        if (!raw) return '';
        var d = new Date(raw);
        if (isNaN(d.getTime())) return '';
        var m = String(d.getMonth() + 1).padStart(2, '0');
        var day = String(d.getDate()).padStart(2, '0');
        var h = String(d.getHours()).padStart(2, '0');
        var min = String(d.getMinutes()).padStart(2, '0');
        return d.getFullYear() + '-' + m + '-' + day + 'T' + h + ':' + min;
    }

    function updateCatalogActive() {
        var cards = document.querySelectorAll('.report-catalog-card[data-report-id]');
        cards.forEach(function (card) {
            var id = card.getAttribute('data-report-id') || '';
            card.classList.toggle('is-active', id === state.selectedReportId && getModal() && getModal().classList.contains('is-open'));
        });
    }

    function ensureReportCustomState(reportId) {
        if (!state.filters.customByReport[reportId]) {
            state.filters.customByReport[reportId] = {};
        }
        return state.filters.customByReport[reportId];
    }

    function renderCustomFields() {
        var root = document.getElementById('report-custom-fields');
        if (!root) return;
        var fields = reportCustomConfig[state.selectedReportId] || [];
        var values = ensureReportCustomState(state.selectedReportId);
        if (!fields.length) {
            root.innerHTML = '';
            return;
        }
        var html = '';
        fields.forEach(function (f) {
            var value = values[f.key] != null ? String(values[f.key]) : '';
            html += '<label class="form-field"><span class="form-field__label">' + f.label + '</span>';
            if (f.type === 'select') {
                html += '<select class="form-input" data-custom-key="' + f.key + '">';
                (f.options || []).forEach(function (opt) {
                    html += '<option value="' + opt.value + '"' + (value === String(opt.value) ? ' selected="selected"' : '') + '>' + opt.label + '</option>';
                });
                html += '</select>';
            } else if (f.type === 'select-dynamic') {
                var list = state.options[f.source] || [];
                var placeholderLabel = f.placeholderLabel || 'Все';
                html += '<select class="form-input" data-custom-key="' + f.key + '"><option value="">' + placeholderLabel + '</option>';
                list.forEach(function (opt) {
                    var oid = String(opt.id);
                    html += '<option value="' + oid + '"' + (value === oid ? ' selected="selected"' : '') + '>' + opt.label + '</option>';
                });
                html += '</select>';
            } else {
                var type = f.type === 'date' ? 'date' : (f.type || 'text');
                var minAttr = f.min ? ' min="' + f.min + '"' : '';
                var pAttr = f.placeholder ? ' placeholder="' + f.placeholder + '"' : '';
                html += '<input class="form-input" type="' + type + '" data-custom-key="' + f.key + '" value="' + value + '"' + minAttr + pAttr + ' />';
            }
            html += '</label>';
        });
        root.innerHTML = html;
    }

    function updateGenerateButton() {
        var btn = document.getElementById('report-generate-btn');
        if (!btn) return;
        var noSelection = !state.selectedReportId;
        btn.disabled = noSelection;
        btn.classList.toggle('report-toolbar__run-disabled', noSelection);
    }

    function collectReportTitles() {
        var titlesById = {};
        document.querySelectorAll('.report-catalog-card[data-report-id]').forEach(function (card) {
            var id = card.getAttribute('data-report-id') || '';
            var t = card.querySelector('.report-catalog-card__title');
            if (id && t) titlesById[id] = t.textContent.trim();
        });
        state.titlesById = titlesById;
    }

    function setModalTitle(title) {
        var node = document.getElementById('report-workflow-title');
        if (node) node.textContent = title || 'Отчёт';
        var dialog = getModal() && getModal().querySelector('.app-modal__dialog');
        if (dialog) dialog.setAttribute('aria-label', title || 'Отчёт');
    }

    function setModalPeriodSubtitle(text) {
        var node = document.getElementById('report-workflow-subtitle');
        if (!node) return;
        if (text) {
            node.textContent = text;
            node.classList.remove('is-empty');
        } else {
            node.textContent = '';
            node.classList.add('is-empty');
        }
    }

    function formatDateTimeLocalForSubtitle(value) {
        if (!value || typeof value !== 'string') return '…';
        var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value.trim());
        if (!m) return value;
        return m[3] + '-' + m[2] + '-' + m[1] + ' ' + m[4] + ':' + m[5];
    }

    function formatPeriodHint() {
        var form = document.getElementById('reports-generate-form');
        if (!form) return '';
        var periodFrom = form.querySelector('input[name="periodFrom"]');
        var periodTo = form.querySelector('input[name="periodTo"]');
        var a = periodFrom ? periodFrom.value : '';
        var b = periodTo ? periodTo.value : '';
        if (!a && !b) return '';
        return 'Период: ' + (a ? formatDateTimeLocalForSubtitle(a) : '…') + ' — ' + (b ? formatDateTimeLocalForSubtitle(b) : '…');
    }

    function updateToolbarScope() {
        renderCustomFields();
        updateGenerateButton();
    }

    function showInfoToast(message) {
        var mgr = window.AppToasts && window.AppToasts.getManager
            ? window.AppToasts.getManager('global-toast-stack')
            : null;
        if (mgr) mgr.show(message, 'info');
    }

    function showErrorToast(message) {
        var mgr = window.AppToasts && window.AppToasts.getManager
            ? window.AppToasts.getManager('global-toast-stack')
            : null;
        if (mgr) mgr.show(message, 'error');
        else showInfoToast(message);
    }

    function clearGenerateStatus() {
        var el = document.getElementById('report-generate-status');
        if (el) {
            el.textContent = '';
            el.classList.add('is-empty');
        }
        var btn = document.getElementById('report-generate-btn');
        if (btn) btn.disabled = false;
    }

    function setGenerating(isLoading) {
        var btn = document.getElementById('report-generate-btn');
        var el = document.getElementById('report-generate-status');
        if (btn) {
            btn.disabled = isLoading || !state.selectedReportId;
            btn.classList.toggle('report-toolbar__run-disabled', isLoading || !state.selectedReportId);
        }
        if (el) {
            el.textContent = '';
            el.classList.add('is-empty');
        }
    }

    function bindCategoryAnimations() {
        var detailsList = document.querySelectorAll('details.report-category');
        detailsList.forEach(function (det) {
            var summary = det.querySelector('summary.report-category__summary');
            var panel = det.querySelector('.report-category__panel');
            var chevron = summary ? summary.querySelector('.report-category__chevron') : null;
            if (!summary || !panel) return;

            summary.addEventListener('click', function (e) {
                e.preventDefault();
                if (det.dataset.animating === '1') return;

                if (det.hasAttribute('open')) {
                    det.dataset.animating = '1';
                    panel.style.height = panel.scrollHeight + 'px';
                    if (chevron) chevron.style.transform = 'rotate(-45deg)';
                    requestAnimationFrame(function () {
                        panel.style.height = '0px';
                    });
                    window.setTimeout(function () {
                        det.removeAttribute('open');
                        panel.style.height = '';
                        if (chevron) chevron.style.transform = '';
                        delete det.dataset.animating;
                    }, 240);
                } else {
                    det.setAttribute('open', '');
                    det.dataset.animating = '1';
                    panel.style.height = '0px';
                    if (chevron) chevron.style.transform = 'rotate(-45deg)';
                    requestAnimationFrame(function () {
                        panel.style.height = panel.scrollHeight + 'px';
                        if (chevron) chevron.style.transform = 'rotate(45deg)';
                    });
                    window.setTimeout(function () {
                        panel.style.height = '';
                        if (chevron) chevron.style.transform = '';
                        delete det.dataset.animating;
                    }, 240);
                }
            });
        });
    }

    function bindCatalogSelection() {
        var cards = document.querySelectorAll('.report-catalog-card[data-report-id]');
        cards.forEach(function (card) {
            card.addEventListener('click', function () {
                var id = card.getAttribute('data-report-id') || '';
                if (!id) return;
                state.selectedReportId = id;
                setModalTitle(state.titlesById[id] || 'Отчёт');
                setModalPeriodSubtitle('');
                syncFormPeriodFromState();
                updateToolbarScope();
                openWorkflowModal();
                updateCatalogActive();
            });
        });
    }

    function syncFormPeriodFromState() {
        var form = document.getElementById('reports-generate-form');
        if (!form) return;
        var periodFrom = form.querySelector('input[name="periodFrom"]');
        var periodTo = form.querySelector('input[name="periodTo"]');
        if (periodFrom && state.filters.periodFrom) periodFrom.value = state.filters.periodFrom;
        if (periodTo && state.filters.periodTo) periodTo.value = state.filters.periodTo;
    }

    function collectFiltersFromUi() {
        var form = document.getElementById('reports-generate-form');
        if (!form) return;
        var periodFrom = form.querySelector('input[name="periodFrom"]');
        var periodTo = form.querySelector('input[name="periodTo"]');
        state.filters.periodFrom = periodFrom ? periodFrom.value : '';
        state.filters.periodTo = periodTo ? periodTo.value : '';

        var custom = ensureReportCustomState(state.selectedReportId);
        var inputs = form.querySelectorAll('[data-custom-key]');
        inputs.forEach(function (el) {
            var key = el.getAttribute('data-custom-key');
            if (!key) return;
            custom[key] = el.value;
        });
    }

    function destroyLoadPreviewChart() {
        if (state.loadPreviewChart) {
            try {
                state.loadPreviewChart.destroy();
            } catch (_) { /* ignore */ }
            state.loadPreviewChart = null;
        }
    }

    function mountLoadPreviewPieChart(pie) {
        if (typeof Chart === 'undefined') return;
        var rawVals = pie.values || [];
        var labels = (pie.labels || []).map(function (x) { return String(x); });
        var vals = rawVals.map(function (v) {
            var n = Number(v);
            return isNaN(n) ? 0 : n;
        });
        while (vals.length < labels.length) vals.push(0);
        if (vals.length > labels.length) vals = vals.slice(0, labels.length);
        var sum = vals.reduce(function (a, b) { return a + b; }, 0);
        if (sum <= 0) return;
        var canvas = document.getElementById('report-load-preview-pie');
        if (!canvas) return;
        state.loadPreviewChart = new Chart(canvas, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: vals,
                    backgroundColor: ['rgba(0, 179, 184, 0.88)', 'rgba(148, 163, 184, 0.78)'],
                    borderColor: ['rgba(0, 153, 158, 1)', 'rgba(100, 116, 139, 0.95)'],
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                aspectRatio: 1.15,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { boxWidth: 12, padding: 10, font: { size: 12 } }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                var val = ctx.raw != null ? Number(ctx.raw) : 0;
                                var pct = sum > 0 ? (100 * val / sum).toFixed(1) : '0';
                                var lab = ctx.label || '';
                                return lab + ': ' + val.toFixed(1) + ' мин (' + pct + '%)';
                            }
                        }
                    }
                }
            }
        });
    }

    function renderPreviewTable(result) {
        var root = document.getElementById('report-preview-content');
        if (!root) return;
        destroyLoadPreviewChart();
        if (!result || !result.columnHeaders || !result.rows) {
            root.innerHTML = '<p class="report-params__empty">Нет данных для предпросмотра.</p>';
            return;
        }

        var pie = result.previewPieChart;
        var chartBlock = '';
        if (pie && pie.labels && pie.values && typeof Chart !== 'undefined') {
            var valsCheck = (pie.values || []).map(function (v) { var n = Number(v); return isNaN(n) ? 0 : n; });
            var sumCheck = valsCheck.reduce(function (a, b) { return a + b; }, 0);
            if (sumCheck > 0) {
                chartBlock = '<div class="report-preview-modal__chart-wrap" role="presentation">' +
                    '<canvas id="report-load-preview-pie" aria-label="Соотношение длительности обслуживания и простоя"></canvas></div>';
            }
        }

        var html = chartBlock;
        html += '<div class="users-table-wrap report-preview-table"><table class="users-table users-table--report-preview"><thead><tr>';
        result.columnHeaders.forEach(function (h) {
            html += '<th>' + String(h) + '</th>';
        });
        html += '</tr></thead><tbody>';
        result.rows.forEach(function (row) {
            var rc = row.rowClass;
            var safeClass = typeof rc === 'string' && /^[a-zA-Z0-9 _-]+$/.test(rc) ? rc : '';
            html += safeClass ? '<tr class="' + safeClass + '">' : '<tr>';
            var cells = row.cells || [];
            var colSpans = row.cellColSpans;
            for (var ci = 0; ci < cells.length; ci++) {
                var span = (colSpans && colSpans.length > ci) ? colSpans[ci] : 1;
                if (span === 0) continue;
                var attr = span > 1 ? ' colspan="' + span + '"' : '';
                html += '<td' + attr + '>' + String(cells[ci]) + '</td>';
            }
            html += '</tr>';
        });
        html += '</tbody></table></div>';
        root.innerHTML = html;
        if (chartBlock && pie) {
            window.requestAnimationFrame(function () {
                mountLoadPreviewPieChart(pie);
            });
        }
    }

    async function generateReport() {
        if (!state.selectedReportId) return;
        collectFiltersFromUi();
        setGenerating(true);
        var payload = {
            reportId: state.selectedReportId,
            dateFrom: state.filters.periodFrom || null,
            dateTo: state.filters.periodTo || null,
            weekStart: state.filters.customByReport[state.selectedReportId] && state.filters.customByReport[state.selectedReportId].weekStart
                ? state.filters.customByReport[state.selectedReportId].weekStart
                : null,
            cabinetId: state.filters.customByReport[state.selectedReportId] && state.filters.customByReport[state.selectedReportId].cabinetId
                ? Number(state.filters.customByReport[state.selectedReportId].cabinetId)
                : null,
            doctorId: state.filters.customByReport[state.selectedReportId] && state.filters.customByReport[state.selectedReportId].doctorId
                ? Number(state.filters.customByReport[state.selectedReportId].doctorId)
                : null,
            customParams: state.filters.customByReport[state.selectedReportId] || {}
        };
        try {
            var response = await fetch('/Reports/Generate', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(payload)
            });
            var data = null;
            try {
                data = await response.json();
            } catch (_) {
                data = null;
            }
            if (!response.ok) {
                var msg = (data && data.message) ? data.message : 'Не удалось сформировать отчёт (ошибка сервера).';
                showErrorToast(msg);
                return;
            }
            if (!data || !data.success) {
                showErrorToast((data && data.message) ? data.message : 'Не удалось сформировать отчёт.');
                return;
            }
            if (!data.implemented) {
                showInfoToast(data.message || 'Формирование этого отчёта находится в разработке.');
                return;
            }
            state.lastResult = data.result || null;
            var previewTitle = state.lastResult && state.lastResult.title ? state.lastResult.title : (state.titlesById[state.selectedReportId] || 'Предпросмотр');
            setModalTitle(previewTitle);
            setModalPeriodSubtitle(formatPeriodHint());
            renderPreviewTable(state.lastResult);
            setWorkflowView('preview');
        } catch (e) {
            showErrorToast('Сеть недоступна или запрос прерван.');
        } finally {
            setGenerating(false);
            updateGenerateButton();
        }
    }

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('#reports-generate-form input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function collectExportPayload(format) {
        collectFiltersFromUi();
        return {
            reportId: state.selectedReportId,
            format: format,
            dateFrom: state.filters.periodFrom || null,
            dateTo: state.filters.periodTo || null,
            weekStart: state.filters.customByReport[state.selectedReportId] && state.filters.customByReport[state.selectedReportId].weekStart
                ? state.filters.customByReport[state.selectedReportId].weekStart
                : null,
            cabinetId: state.filters.customByReport[state.selectedReportId] && state.filters.customByReport[state.selectedReportId].cabinetId
                ? Number(state.filters.customByReport[state.selectedReportId].cabinetId)
                : null,
            doctorId: state.filters.customByReport[state.selectedReportId] && state.filters.customByReport[state.selectedReportId].doctorId
                ? Number(state.filters.customByReport[state.selectedReportId].doctorId)
                : null,
            customParams: state.filters.customByReport[state.selectedReportId] || {}
        };
    }

    function parseFileNameFromContentDisposition(header) {
        if (!header || typeof header !== 'string') return null;
        var star = header.match(/filename\*=(?:UTF-8''|utf-8'')([^;\s]+)/i);
        if (star && star[1]) {
            try {
                var dec = decodeURIComponent(star[1].replace(/["']/g, '').trim());
                if (dec) return dec;
            } catch (_) { /* ignore */ }
        }
        var quoted = header.match(/filename="([^"]+)"/i);
        if (quoted && quoted[1]) return quoted[1].trim();
        var loose = header.match(/filename=([^;\s]+)/i);
        if (loose && loose[1]) return loose[1].replace(/^["']|["']$/g, '').trim();
        return null;
    }

    function formatFromFileName(name) {
        var n = (name || '').toLowerCase();
        if (n.endsWith('.xlsx')) return 'xlsx';
        if (n.endsWith('.pdf')) return 'pdf';
        if (n.endsWith('.csv')) return 'csv';
        return 'csv';
    }

    function defaultExportBaseName() {
        var id = state.selectedReportId || 'report';
        return String(id).replace(/[^\w.-]+/g, '_');
    }

    async function fetchExportBlob(format) {
        var payload = collectExportPayload(format);
        var response = await fetch('/Reports/Export', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify(payload)
        });
        if (!response.ok) {
            throw new Error('export_http_' + response.status);
        }
        var blob = await response.blob();
        var cd = response.headers.get('Content-Disposition');
        var parsed = parseFileNameFromContentDisposition(cd);
        var fileName = parsed || (defaultExportBaseName() + '.' + format);
        return { blob: blob, fileName: fileName };
    }

    function downloadBlobViaAnchor(blob, fileName) {
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    }

    async function saveReportAs() {
        if (!state.lastResult) return;
        if (typeof window.showSaveFilePicker === 'function') {
            try {
                var base = defaultExportBaseName();
                var handle = await window.showSaveFilePicker({
                    suggestedName: base + '.csv',
                    types: [
                        { description: 'CSV', accept: { 'text/csv': ['.csv'] } },
                        { description: 'Excel', accept: { 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': ['.xlsx'] } },
                        { description: 'PDF', accept: { 'application/pdf': ['.pdf'] } }
                    ]
                });
                var format = formatFromFileName(handle.name);
                var result = await fetchExportBlob(format);
                var writable = await handle.createWritable();
                await writable.write(result.blob);
                await writable.close();
            } catch (e) {
                if (e && e.name === 'AbortError') return;
                var dlgErr = document.getElementById('report-export-fallback-dialog');
                if (dlgErr && typeof dlgErr.showModal === 'function') {
                    dlgErr.showModal();
                    return;
                }
                showErrorToast('Не удалось выгрузить файл.');
            }
            return;
        }
        var dlg = document.getElementById('report-export-fallback-dialog');
        if (dlg && typeof dlg.showModal === 'function') {
            dlg.showModal();
        } else {
            showErrorToast('Сохранение недоступно в этом браузере.');
        }
    }

    function bindExportFallbackDialog() {
        var dlg = document.getElementById('report-export-fallback-dialog');
        if (!dlg) return;
        dlg.querySelectorAll('[data-export-fallback-format]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var fmt = btn.getAttribute('data-export-fallback-format') || 'csv';
                dlg.close();
                fetchExportBlob(fmt).then(function (result) {
                    downloadBlobViaAnchor(result.blob, result.fileName);
                }).catch(function () {
                    showErrorToast('Не удалось выгрузить файл.');
                });
            });
        });
        var cancel = dlg.querySelector('[data-export-fallback-cancel]');
        if (cancel) {
            cancel.addEventListener('click', function () {
                dlg.close();
            });
        }
    }

    function bindActions() {
        var generateBtn = document.getElementById('report-generate-btn');
        if (generateBtn) {
            generateBtn.addEventListener('click', function () {
                generateReport();
            });
        }
        var backBtn = document.getElementById('report-back-to-params');
        if (backBtn) {
            backBtn.addEventListener('click', function () {
                setWorkflowView('params');
                setModalTitle(state.titlesById[state.selectedReportId] || 'Отчёт');
                setModalPeriodSubtitle('');
                clearGenerateStatus();
                updateGenerateButton();
            });
        }
        var saveAsBtn = document.getElementById('report-save-as');
        if (saveAsBtn) {
            saveAsBtn.addEventListener('click', function () {
                saveReportAs();
            });
        }
        bindExportFallbackDialog();
    }

    function initWorkflowModal() {
        var modal = getModal();
        if (!modal) return;

        modal.addEventListener('click', function (e) {
            var t = e.target;
            if (!(t instanceof Element)) return;
            var closeEl = t.closest('[data-modal-close="' + modalId + '"]');
            if (closeEl) {
                e.preventDefault();
                closeWorkflowModal();
                updateCatalogActive();
            }
        });

        document.addEventListener('keydown', function (e) {
            if (e.key !== 'Escape') return;
            if (!modal.classList.contains('is-open')) return;
            var fbDlg = document.getElementById('report-export-fallback-dialog');
            if (fbDlg && fbDlg.open) return;
            closeWorkflowModal();
            updateCatalogActive();
        });
    }

    function openSelectedFromQuery() {
        var id = state.selectedReportId;
        if (!id) return;
        var card = null;
        document.querySelectorAll('.report-catalog-card[data-report-id]').forEach(function (c) {
            if (c.getAttribute('data-report-id') === id) card = c;
        });
        if (!card) return;
        var details = card.closest('details.report-category');
        if (details && !details.hasAttribute('open')) {
            details.setAttribute('open', '');
        }
        state.selectedReportId = id;
        setModalTitle(state.titlesById[id] || 'Отчёт');
        setModalPeriodSubtitle('');
        syncFormPeriodFromState();
        updateToolbarScope();
        openWorkflowModal();
        updateCatalogActive();
    }

    function boot() {
        readInitialState();
        collectReportTitles();
        bindCatalogSelection();
        bindCategoryAnimations();
        bindActions();
        initWorkflowModal();
        updateGenerateButton();
        openSelectedFromQuery();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})();
