(function () {
    'use strict';

    var modalId = 'report-preview-modal';
    var implementedReports = ['queue-summary', 'cabinet-load'];
    var reportCustomConfig = {
        'doctor-cabinet-load-downtime': [
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
        'service-categories-performance': [
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
        lastResult: null
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

    function openReportPreviewModal(modal) {
        if (!modal) return;
        lastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        modal.classList.add('is-open');
        setModalHiddenState(modal, false);
    }

    function closeReportPreviewModal() {
        var modal = getModal();
        if (!modal) return;
        var active = document.activeElement;
        if (active instanceof HTMLElement && modal.contains(active)) {
            active.blur();
        }
        modal.classList.remove('is-open');
        setModalHiddenState(modal, true);
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

    function isImplemented(reportId) {
        return implementedReports.indexOf(reportId) >= 0;
    }

    function updateCatalogActive() {
        var cards = document.querySelectorAll('.report-catalog-card[data-report-id]');
        cards.forEach(function (card) {
            var id = card.getAttribute('data-report-id') || '';
            card.classList.toggle('is-active', id === state.selectedReportId);
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

    function updateSelectedTitle() {
        var node = document.getElementById('report-toolbar-selected');
        if (!node) return;
        var id = state.selectedReportId;
        var title = id && state.titlesById ? state.titlesById[id] : '';
        if (title) {
            node.textContent = title;
            node.classList.remove('is-empty');
        } else {
            node.textContent = '';
            node.classList.add('is-empty');
        }
    }

    function updateToolbarScope() {
        renderCustomFields();
        updateGenerateButton();
        updateSelectedTitle();
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

    function showInfoToast(message) {
        var mgr = window.AppToasts && window.AppToasts.getManager
            ? window.AppToasts.getManager('global-toast-stack')
            : null;
        if (mgr) mgr.show(message, 'info');
    }

    function bindCatalogSelection() {
        var cards = document.querySelectorAll('.report-catalog-card[data-report-id]');
        cards.forEach(function (card) {
            card.addEventListener('click', function (e) {
                e.preventDefault();
                var id = card.getAttribute('data-report-id') || '';
                if (!id) return;
                state.selectedReportId = id;
                updateCatalogActive();
                updateToolbarScope();
            });
        });
    }

    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('#reports-generate-form input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
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

    function renderPreviewTable(result) {
        var root = document.getElementById('report-preview-content');
        if (!root) return;
        if (!result || !result.columnHeaders || !result.rows) {
            root.innerHTML = '<p class="report-params__empty">Нет данных для предпросмотра.</p>';
            return;
        }

        var html = '<div class="users-table-wrap"><table class="users-table"><thead><tr>';
        result.columnHeaders.forEach(function (h) {
            html += '<th>' + String(h) + '</th>';
        });
        html += '</tr></thead><tbody>';
        result.rows.forEach(function (row) {
            html += '<tr>';
            (row.cells || []).forEach(function (cell) {
                html += '<td>' + String(cell) + '</td>';
            });
            html += '</tr>';
        });
        html += '</tbody></table></div>';
        root.innerHTML = html;
    }

    function updateModalTitle(title) {
        var modal = getModal();
        if (!modal) return;
        var titleNode = modal.querySelector('.register-panel-title');
        var dialogNode = modal.querySelector('.app-modal__dialog');
        if (titleNode) titleNode.textContent = title || 'Предпросмотр отчёта';
        if (dialogNode) dialogNode.setAttribute('aria-label', title || 'Предпросмотр отчёта');
    }

    async function generateReport() {
        if (!state.selectedReportId) return;
        if (!isImplemented(state.selectedReportId)) {
            showInfoToast('Формирование этого отчёта находится в разработке.');
            return;
        }
        collectFiltersFromUi();
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
        var response = await fetch('/Reports/Generate', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify(payload)
        });
        if (!response.ok) return;
        var data = await response.json();
        if (!data || !data.success) return;
        state.lastResult = data.result || null;
        updateModalTitle(state.lastResult && state.lastResult.title ? state.lastResult.title : 'Предпросмотр отчёта');
        renderPreviewTable(state.lastResult);
        openReportPreviewModal(getModal());
    }

    async function exportReport(format) {
        if (!state.lastResult) return;
        collectFiltersFromUi();
        var payload = {
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
        var response = await fetch('/Reports/Export', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify(payload)
        });
        if (!response.ok) return;
        var blob = await response.blob();
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = (state.selectedReportId || 'report') + '.' + format;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    }

    function bindActions() {
        var generateBtn = document.getElementById('report-generate-btn');
        if (generateBtn) {
            generateBtn.addEventListener('click', function () {
                generateReport();
            });
        }
        var exports = document.querySelectorAll('[data-export-format]');
        exports.forEach(function (btn) {
            btn.addEventListener('click', function () {
                var format = btn.getAttribute('data-export-format') || 'csv';
                exportReport(format);
            });
        });
    }

    function initReportPreviewModal() {
        var modal = getModal();
        if (!modal) return;

        modal.addEventListener('click', function (e) {
            var t = e.target;
            if (!(t instanceof Element)) return;
            var closeEl = t.closest('[data-modal-close="' + modalId + '"]');
            if (closeEl) {
                e.preventDefault();
                closeReportPreviewModal();
            }
        });

        document.addEventListener('keydown', function (e) {
            if (e.key !== 'Escape') return;
            if (!modal.classList.contains('is-open')) return;
            closeReportPreviewModal();
        });
    }

    function boot() {
        readInitialState();
        collectReportTitles();
        bindCatalogSelection();
        bindCategoryAnimations();
        updateCatalogActive();
        updateToolbarScope();
        bindActions();
        initReportPreviewModal();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})();
