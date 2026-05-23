/* ReportPreviewCore: встроено в этот файл (раньше отдельный report-preview-core.js), чтобы скрипт всегда поднимался одним запросом. */
(function (global) {
    'use strict';

    var chartFactories = {
        doughnut: mountDoughnutOrPie,
        pie: mountDoughnutOrPie,
        bar: mountBarChart,
        groupedbar: mountGroupedBarChart
    };

    /** Синхронизировать с Services/Reports/ReportChartPalette.cs BaseRgb */
    var REPORT_CHART_BASE_RGB = [
        [0, 179, 184],
        [148, 163, 184],
        [251, 191, 36],
        [239, 68, 68],
        [99, 102, 241],
        [16, 185, 129],
        [37, 99, 235],
        [168, 85, 247],
        [236, 72, 153],
        [6, 182, 212],
        [234, 88, 12],
        [255, 159, 67],
        [132, 204, 22],
        [153, 27, 27],
        [202, 138, 4],
        [133, 77, 14],
        [192, 38, 211],
        [13, 148, 136],
        [244, 63, 94],
        [109, 40, 217],
        [101, 163, 13],
        [157, 23, 77],
        [52, 211, 153],
        [217, 119, 6]
    ];
    var REPORT_CHART_PALETTE = (function () {
        function rgbaFromBase(rgb, alpha) {
            return 'rgba(' + rgb[0] + ', ' + rgb[1] + ', ' + rgb[2] + ', ' + alpha + ')';
        }
        function darken(rgb, factor) {
            return [
                Math.round(rgb[0] * factor),
                Math.round(rgb[1] * factor),
                Math.round(rgb[2] * factor)
            ];
        }
        return {
            bg: REPORT_CHART_BASE_RGB.map(function (rgb) { return rgbaFromBase(rgb, 0.88); }),
            norm: REPORT_CHART_BASE_RGB.map(function (rgb) { return rgbaFromBase(rgb, 0.45); }),
            border: REPORT_CHART_BASE_RGB.map(function (rgb) { return rgbaFromBase(darken(rgb, 0.84), 1); })
        };
    })();

    function escapeHtml(text) {
        if (text == null) return '';
        var s = String(text);
        var map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
        return s.replace(/[&<>"']/g, function (ch) { return map[ch] || ch; });
    }

    function escapeHtmlAttribute(text) {
        return escapeHtml(text).replace(/`/g, '&#96;');
    }

    function sumNumericValues(arr) {
        var sum = 0;
        (arr || []).forEach(function (v) {
            if (v == null) return;
            var n = Number(v);
            if (!isNaN(n) && n > 0) sum += n;
        });
        return sum;
    }

    function sumDescriptorChartValues(descriptor) {
        if (!descriptor) return 0;
        if (descriptor.datasets && descriptor.datasets.length) {
            var total = 0;
            descriptor.datasets.forEach(function (ds) {
                total += sumNumericValues(ds.values);
                total += sumNumericValues(ds.normValues);
            });
            return total;
        }
        return sumNumericValues(descriptor.values);
    }

    function descriptorHasRenderableChartData(descriptor) {
        if (!descriptor) return false;
        function hasFiniteValue(arr) {
            return (arr || []).some(function (v) {
                return v != null && !isNaN(Number(v));
            });
        }
        if (descriptor.datasets && descriptor.datasets.length) {
            return descriptor.datasets.some(function (ds) {
                return hasFiniteValue(ds.values) || hasFiniteValue(ds.normValues);
            });
        }
        return sumNumericValues(descriptor.values) > 0;
    }

    function extractChartDescriptors(result) {
        if (!result) return [];
        if (result.previewCharts && result.previewCharts.length > 0) {
            return result.previewCharts;
        }
        var pie = result.previewPieChart;
        if (pie && pie.labels && pie.values && pie.labels.length) {
            return [{
                kind: 'doughnut',
                labels: pie.labels,
                values: pie.values,
                valueUnit: 'мин',
                ariaLabel: 'Соотношение длительности занятости и простоя',
                canvasElementId: 'report-preview-chart-0'
            }];
        }
        return [];
    }

    function calcChartLayoutMetrics(descriptor) {
        var kind = (descriptor.kind || '').toLowerCase();
        if (kind !== 'groupedbar') return null;
        var dayCount = (descriptor.labels || []).length;
        var seriesCount = (descriptor.datasets || []).length || 1;
        var groupSlot = Math.min(64, 24 + seriesCount * 3);
        var minWidthPx = Math.min(2200, Math.max(560, dayCount * groupSlot + 64));
        var legendRows = Math.ceil(seriesCount / Math.max(1, Math.floor(640 / 140)));
        var minHeightPx = Math.min(420, 280 + legendRows * 16);
        return { minWidthPx: minWidthPx, minHeightPx: minHeightPx, dayCount: dayCount };
    }

    function buildChartBlocksHtml(descriptors) {
        var html = '';
        (descriptors || []).forEach(function (d, i) {
            var id = d.canvasElementId || ('report-preview-chart-' + i);
            if (!descriptorHasRenderableChartData(d)) return;
            var aria = d.ariaLabel ? escapeHtmlAttribute(d.ariaLabel) : '';
            var metrics = calcChartLayoutMetrics(d);
            var isGrouped = metrics !== null;
            var wrapClass = 'report-preview-modal__chart-wrap' + (isGrouped ? ' report-preview-modal__chart-wrap--grouped-bar' : '');
            var styleAttr = isGrouped
                ? ' style="min-width:' + metrics.minWidthPx + 'px;height:' + metrics.minHeightPx + 'px;"'
                : '';
            var canvasTag = '<canvas id="' + escapeHtmlAttribute(id) + '"' +
                (aria ? ' aria-label="' + aria + '"' : '') + '></canvas>';
            if (isGrouped) {
                html += '<div class="report-preview-modal__chart-scroll" role="presentation">' +
                    '<div class="' + wrapClass + '"' + styleAttr + '>' + canvasTag + '</div></div>';
                if (d.footnote) {
                    html += '<p class="report-preview-modal__chart-footnote">' + escapeHtml(d.footnote) + '</p>';
                }
            } else {
                html += '<div class="' + wrapClass + '" role="presentation">' + canvasTag + '</div>';
            }
        });
        return html;
    }

    function mountDoughnutOrPie(descriptor, canvas, chartsOut) {
        if (typeof Chart === 'undefined' || !canvas) return;
        var rawVals = descriptor.values || [];
        var labels = (descriptor.labels || []).map(function (x) { return String(x); });
        var vals = rawVals.map(function (v) {
            var n = Number(v);
            return isNaN(n) ? 0 : n;
        });
        while (vals.length < labels.length) vals.push(0);
        if (vals.length > labels.length) vals = vals.slice(0, labels.length);
        var sum = vals.reduce(function (a, b) { return a + b; }, 0);
        if (sum <= 0) return;
        var kind = (descriptor.kind || 'doughnut').toLowerCase() === 'pie' ? 'pie' : 'doughnut';
        var unit = descriptor.valueUnit != null ? String(descriptor.valueUnit).trim() : '';
        var paletteBg = REPORT_CHART_PALETTE.bg;
        var paletteBorder = REPORT_CHART_PALETTE.border;
        var bgColors = labels.map(function (_, i) {
            return paletteBg[i % paletteBg.length];
        });
        var borderColors = labels.map(function (_, i) {
            return paletteBorder[i % paletteBorder.length];
        });
        var chart = new Chart(canvas, {
            type: kind,
            data: {
                labels: labels,
                datasets: [{
                    data: vals,
                    backgroundColor: bgColors,
                    borderColor: borderColors,
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                layout: { padding: { top: 4, bottom: 4, left: 4, right: 4 } },
                plugins: {
                    legend: {
                        position: 'bottom',
                        align: 'center',
                        labels: {
                            boxWidth: 14,
                            padding: 12,
                            font: { size: 12 },
                            maxWidth: 900
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                var val = ctx.raw != null ? Number(ctx.raw) : 0;
                                var pct = sum > 0 ? (100 * val / sum).toFixed(1) : '0';
                                var lab = ctx.label || '';
                                var mid = unit ? (val.toFixed(1) + ' ' + unit) : val.toFixed(1);
                                return lab + ': ' + mid + ' (' + pct + '%)';
                            }
                        }
                    }
                }
            }
        });
        if (chartsOut) chartsOut.push(chart);
    }

    function mountBarChart(descriptor, canvas, chartsOut) {
        if (typeof Chart === 'undefined' || !canvas) return;
        var labels = (descriptor.labels || []).map(function (x) { return String(x); });
        var vals = (descriptor.values || []).map(function (v) {
            var n = Number(v);
            return isNaN(n) ? 0 : n;
        });
        while (vals.length < labels.length) vals.push(0);
        if (vals.length > labels.length) vals = vals.slice(0, labels.length);
        if (labels.length === 0 || vals.every(function (v) { return v <= 0; })) return;
        var unit = descriptor.valueUnit != null ? String(descriptor.valueUnit).trim() : '';
        var chart = new Chart(canvas, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    data: vals,
                    backgroundColor: 'rgba(0, 179, 184, 0.88)',
                    borderColor: 'rgba(0, 153, 158, 1)',
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                layout: { padding: { top: 4, bottom: 4, left: 4, right: 4 } },
                scales: {
                    y: {
                        beginAtZero: true,
                        title: {
                            display: !!unit,
                            text: unit || undefined
                        }
                    },
                    x: {
                        ticks: { maxRotation: 45, minRotation: 0 }
                    }
                },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                var val = ctx.raw != null ? Number(ctx.raw) : 0;
                                var lab = ctx.label || '';
                                var mid = unit ? (val.toFixed(1) + ' ' + unit) : val.toFixed(1);
                                return lab + ': ' + mid;
                            }
                        }
                    }
                }
            }
        });
        if (chartsOut) chartsOut.push(chart);
    }

    function mountGroupedBarChart(descriptor, canvas, chartsOut) {
        if (typeof Chart === 'undefined' || !canvas) return;
        var dayLabels = (descriptor.labels || []).map(function (x) { return String(x); });
        var series = descriptor.datasets || [];
        if (!dayLabels.length || !series.length) return;
        var paletteBg = REPORT_CHART_PALETTE.bg;
        var paletteBorder = REPORT_CHART_PALETTE.border;
        var paletteNorm = REPORT_CHART_PALETTE.norm;
        var unit = descriptor.valueUnit != null ? String(descriptor.valueUnit).trim() : '';
        var datasets = [];
        var hasChartData = false;
        var hasOverlayNorm = series.some(function (ds) {
            var nv = ds.normValues;
            return nv && nv.length > 0;
        });
        function normalizeVals(raw) {
            var vals = (raw || []).map(function (v) {
                if (v == null) return null;
                var n = Number(v);
                return isNaN(n) ? null : n;
            });
            while (vals.length < dayLabels.length) vals.push(null);
            if (vals.length > dayLabels.length) vals = vals.slice(0, dayLabels.length);
            vals.forEach(function (v) { if (v != null) hasChartData = true; });
            return vals;
        }
        function normBg(colorIdx) {
            return paletteNorm[colorIdx % paletteNorm.length];
        }
        series.forEach(function (ds, si) {
            var factVals = normalizeVals(ds.values);
            var normVals = ds.normValues ? normalizeVals(ds.normValues) : null;
            var colorIdx = si % paletteBg.length;
            var sliceLabel = ds.label || ('Серия ' + si);
            if (normVals && normVals.length === factVals.length) {
                var stackId = 'slice-' + si;
                datasets.push({
                    type: 'bar',
                    label: sliceLabel + ' · норм.',
                    data: normVals,
                    stack: stackId,
                    backgroundColor: normBg(colorIdx),
                    borderColor: paletteBorder[colorIdx],
                    borderWidth: 1,
                    order: 2,
                    maxBarThickness: 18
                });
                datasets.push({
                    type: 'bar',
                    label: sliceLabel,
                    data: factVals,
                    stack: stackId,
                    backgroundColor: paletteBg[colorIdx],
                    borderColor: paletteBorder[colorIdx],
                    borderWidth: 1,
                    order: 1,
                    maxBarThickness: 12
                });
            } else {
                datasets.push({
                    type: 'bar',
                    label: sliceLabel,
                    data: factVals,
                    backgroundColor: paletteBg[colorIdx],
                    borderColor: paletteBorder[colorIdx],
                    borderWidth: 1
                });
            }
        });
        if (!hasChartData) return;
        var slotCount = hasOverlayNorm ? series.length : datasets.length;
        var dayCount = dayLabels.length;
        var chart = new Chart(canvas, {
            type: 'bar',
            data: { labels: dayLabels, datasets: datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                layout: { padding: { top: 4, bottom: 4, left: 4, right: 4 } },
                datasets: {
                    bar: {
                        categoryPercentage: 0.82,
                        barPercentage: 0.92,
                        maxBarThickness: slotCount > 12 ? 10 : 14
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        stacked: false,
                        title: { display: !!unit, text: unit || undefined }
                    },
                    x: {
                        stacked: !!hasOverlayNorm,
                        ticks: {
                            autoSkip: true,
                            maxTicksLimit: dayCount > 14 ? 14 : dayCount,
                            maxRotation: 45,
                            minRotation: 0
                        }
                    }
                },
                plugins: {
                    legend: {
                        position: 'bottom',
                        align: 'center',
                        labels: {
                            boxWidth: 10,
                            padding: 6,
                            font: { size: 10 },
                            filter: function () {
                                return true;
                            }
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                if (ctx.raw == null || ctx.parsed == null || ctx.parsed.y == null) return null;
                                var val = Number(ctx.raw);
                                if (isNaN(val)) return null;
                                var seriesLabel = ctx.dataset && ctx.dataset.label ? ctx.dataset.label : '';
                                var day = ctx.label || '';
                                var mid = unit ? (val.toFixed(1) + ' ' + unit) : val.toFixed(1);
                                return seriesLabel + ', ' + day + ': ' + mid;
                            }
                        }
                    }
                }
            }
        });
        if (chartsOut) chartsOut.push(chart);
    }

    function mountChartsFromDescriptors(descriptors) {
        var charts = [];
        if (typeof Chart === 'undefined') return charts;
        (descriptors || []).forEach(function (d, i) {
            if (!descriptorHasRenderableChartData(d)) return;
            var id = d.canvasElementId || ('report-preview-chart-' + i);
            var canvas = document.getElementById(id);
            var kind = (d.kind || 'doughnut').toLowerCase();
            var factory = chartFactories[kind] || chartFactories.doughnut;
            factory(d, canvas, charts);
        });
        return charts;
    }

    function isArrivedCompletedDetailPreviewRow(row) {
        if (!row || row.rowClass) return false;
        var cells = row.cells || [];
        if (cells.length < 5) return false;
        var c0 = (cells[0] || '').trim();
        if (c0 === 'Итого за период') return false;
        var n = parseInt(String(cells[2] || '').trim(), 10);
        return !isNaN(n);
    }

    function isRouteAndPausesDetailPreviewRow(row) {
        if (!row || row.rowClass) return false;
        var cells = row.cells || [];
        if (cells.length < 5) return false;
        var c0 = (cells[0] || '').trim();
        if (c0 === 'Итого за период') return false;
        var n = parseInt(String(cells[2] || '').trim(), 10);
        return !isNaN(n);
    }

    function isAppointmentDurationDetailPreviewRow(row) {
        if (!row || row.rowClass) return false;
        var cells = row.cells || [];
        if (cells.length < 8) return false;
        var c0 = (cells[0] || '').trim();
        if (c0 === 'Итого за период' || c0 === 'Итого за день') return false;
        var countIdx = cells.length >= 9 ? 3 : 2;
        var n = parseInt(String(cells[countIdx] || '').trim(), 10);
        return !isNaN(n);
    }

    function isLoadDowntimeDetailPreviewRow(row) {
        if (!row || row.rowClass) return false;
        var cells = row.cells || [];
        if (cells.length < 2) return false;
        return String(cells[1] || '').trim() !== '—';
    }

    var pageReportsMetaCache = null;

    function getPageReportsMeta() {
        if (pageReportsMetaCache) return pageReportsMetaCache;
        var node = document.getElementById('reports-page-data');
        if (!node) {
            pageReportsMetaCache = { dateRowspanReportIds: [], detailRowKindByReportId: {} };
            return pageReportsMetaCache;
        }
        try {
            var payload = JSON.parse(node.textContent || '{}');
            pageReportsMetaCache = {
                dateRowspanReportIds: Array.isArray(payload.dateRowspanReportIds)
                    ? payload.dateRowspanReportIds.map(function (id) { return String(id || '').trim(); }).filter(Boolean)
                    : [],
                detailRowKindByReportId: payload.detailRowKindByReportId && typeof payload.detailRowKindByReportId === 'object'
                    ? payload.detailRowKindByReportId
                    : {}
            };
        } catch (_) {
            pageReportsMetaCache = { dateRowspanReportIds: [], detailRowKindByReportId: {} };
        }
        return pageReportsMetaCache;
    }

    function usesDateRowspanInPreview(reportId) {
        var rid = (reportId || '').trim();
        if (!rid) return false;
        return getPageReportsMeta().dateRowspanReportIds.some(function (id) {
            return id.toLowerCase() === rid.toLowerCase();
        });
    }

    function getDetailRowKindForReport(reportId) {
        var rid = (reportId || '').trim();
        if (!rid) return '';
        var map = getPageReportsMeta().detailRowKindByReportId;
        var key = Object.keys(map).find(function (k) {
            return k.toLowerCase() === rid.toLowerCase();
        });
        return key ? String(map[key] || '').trim() : '';
    }

    function resolveDetailRowKind(reportId, detailRowKindFromResult) {
        var fromResult = detailRowKindFromResult ? String(detailRowKindFromResult).trim() : '';
        if (fromResult) return fromResult;
        return getDetailRowKindForReport(reportId);
    }

    function isDateGroupedDetailPreviewRow(row, reportId, detailRowKindFromResult) {
        var kind = resolveDetailRowKind(reportId, detailRowKindFromResult);
        if (kind === 'loadDowntime') return isLoadDowntimeDetailPreviewRow(row);
        if (kind === 'routeAndPauses') return isRouteAndPausesDetailPreviewRow(row);
        if (kind === 'arrivedCompleted') return isArrivedCompletedDetailPreviewRow(row);
        if (kind === 'waitingBeforeAppointment') return isArrivedCompletedDetailPreviewRow(row);
        if (kind === 'appointmentDuration') return isAppointmentDurationDetailPreviewRow(row);
        return false;
    }

    function usesDateRowspanForResult(result) {
        if (result && String(result.tableLayout || '').toLowerCase() === 'daterowspan') return true;
        return usesDateRowspanInPreview((result && result.generatedForReportId) || '');
    }

    function appendPreviewTableRowHtml(parts, row) {
        var rc = row.rowClass;
        var safeClass = typeof rc === 'string' && /^[a-zA-Z0-9 _-]+$/.test(rc) ? rc : '';
        parts.push(safeClass ? '<tr class="' + escapeHtmlAttribute(safeClass) + '">' : '<tr>');
        var cells = row.cells || [];
        var colSpans = row.cellColSpans;
        for (var ci = 0; ci < cells.length; ci++) {
            var cSpan = (colSpans && colSpans.length > ci) ? colSpans[ci] : 1;
            if (cSpan === 0) continue;
            var attr = cSpan > 1 ? ' colspan="' + cSpan + '"' : '';
            parts.push('<td' + attr + '>' + escapeHtml(cells[ci]) + '</td>');
        }
        parts.push('</tr>');
    }

    function appendArrivedCompletedRowspanGroup(parts, rows, iStart, jEnd) {
        var rowSpan = jEnd - iStart;
        for (var k = 0; k < rowSpan; k++) {
            var row = rows[iStart + k];
            var cells = row.cells || [];
            var colSpans = row.cellColSpans;
            var rc = row.rowClass;
            var safeClass = typeof rc === 'string' && /^[a-zA-Z0-9 _-]+$/.test(rc) ? rc : '';
            parts.push(safeClass ? '<tr class="' + escapeHtmlAttribute(safeClass) + '">' : '<tr>');
            if (k === 0) {
                parts.push('<td rowspan="' + rowSpan + '">' + escapeHtml(cells[0] || '') + '</td>');
            }
            for (var ci = 1; ci < cells.length; ci++) {
                var cSpan = (colSpans && colSpans.length > ci) ? colSpans[ci] : 1;
                if (cSpan === 0) continue;
                var attr = cSpan > 1 ? ' colspan="' + cSpan + '"' : '';
                parts.push('<td' + attr + '>' + escapeHtml(cells[ci]) + '</td>');
            }
            parts.push('</tr>');
        }
    }

    function buildPreviewTableHtml(result) {
        var html = '<div class="users-table-wrap report-preview-table"><table class="users-table users-table--report-preview"><thead><tr>';
        result.columnHeaders.forEach(function (h) {
            html += '<th>' + escapeHtml(h) + '</th>';
        });
        html += '</tr></thead><tbody>';
        var rows = result.rows || [];
        var rid = (result.generatedForReportId || '').trim();
        var detailKind = result.detailRowKind;
        if (usesDateRowspanForResult(result)) {
            var parts = [];
            var ii = 0;
            while (ii < rows.length) {
                var r = rows[ii];
                if (!isDateGroupedDetailPreviewRow(r, rid, detailKind) || !String((r.cells && r.cells[0]) || '').trim()) {
                    appendPreviewTableRowHtml(parts, r);
                    ii++;
                    continue;
                }
                var jj = ii + 1;
                while (jj < rows.length && isDateGroupedDetailPreviewRow(rows[jj], rid, detailKind) && !String((rows[jj].cells && rows[jj].cells[0]) || '').trim()) {
                    jj++;
                }
                appendArrivedCompletedRowspanGroup(parts, rows, ii, jj);
                ii = jj;
            }
            html += parts.join('');
        } else {
            var plainParts = [];
            rows.forEach(function (row) {
                appendPreviewTableRowHtml(plainParts, row);
            });
            html += plainParts.join('');
        }
        html += '</tbody></table></div>';
        return html;
    }

    function renderResultPreview(result, deps) {
        deps = deps || {};
        var root = deps.root || document.getElementById('report-preview-content');
        if (!root) return;
        if (typeof deps.destroyCharts === 'function') deps.destroyCharts();

        if (!result || !result.columnHeaders || !result.rows) {
            root.innerHTML = '<p class="report-params__empty">Нет данных для предпросмотра.</p>';
            return;
        }

        var descriptors = extractChartDescriptors(result);
        var chartHtml = buildChartBlocksHtml(descriptors);
        root.innerHTML = chartHtml + buildPreviewTableHtml(result);

        if (chartHtml && descriptors.length) {
            window.requestAnimationFrame(function () {
                var charts = mountChartsFromDescriptors(descriptors);
                window.requestAnimationFrame(function () {
                    (charts || []).forEach(function (ch) {
                        if (ch && typeof ch.resize === 'function') ch.resize();
                    });
                });
                if (typeof deps.onChartsMounted === 'function') deps.onChartsMounted(charts);
            });
        } else if (typeof deps.onChartsMounted === 'function') {
            deps.onChartsMounted([]);
        }
    }

    function formatDateForReportApi(value) {
        if (!value || typeof value !== 'string') return null;
        var trimmed = value.trim();
        if (!trimmed) return null;
        var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(trimmed);
        if (m) return m[1] + '-' + m[2] + '-' + m[3] + ' ' + m[4] + ':' + m[5] + ':00';
        return trimmed;
    }

    function buildWhitelistedCustomParams(reportId, custom, reportCustomConfig) {
        var fields = (reportCustomConfig && reportCustomConfig[reportId]) || [];
        if (!fields.length) return {};
        var allowedKeys = {};
        fields.forEach(function (f) {
            if (f && f.key) allowedKeys[f.key] = f;
        });
        var out = {};
        Object.keys(allowedKeys).forEach(function (key) {
            var field = allowedKeys[key];
            var val = custom && custom[key] != null ? String(custom[key]) : '';
            if (!val && field.type === 'select' && field.options && field.options.length) {
                val = String(field.options[0].value);
            }
            if (val !== '') out[key] = val;
        });
        return out;
    }

    function buildReportRequestPayload(state, context) {
        context = context || {};
        var reportCustomConfig = context.reportCustomConfig || {};
        var sel = state.selectedReportId || '';
        var custom = (state.filters && state.filters.customByReport && state.filters.customByReport[sel])
            ? state.filters.customByReport[sel]
            : {};
        return {
            reportId: sel,
            dateFrom: formatDateForReportApi(state.filters && state.filters.periodFrom),
            dateTo: formatDateForReportApi(state.filters && state.filters.periodTo),
            weekStart: custom.weekStart || null,
            cabinetId: custom.cabinetId ? Number(custom.cabinetId) : null,
            doctorId: custom.doctorId ? Number(custom.doctorId) : null,
            customParams: buildWhitelistedCustomParams(sel, custom, reportCustomConfig)
        };
    }

    function registerChartKind(kind, factory) {
        if (kind && typeof factory === 'function') {
            chartFactories[String(kind).toLowerCase()] = factory;
        }
    }

    global.ReportPreviewCore = {
        escapeHtml: escapeHtml,
        escapeHtmlAttribute: escapeHtmlAttribute,
        buildReportRequestPayload: buildReportRequestPayload,
        renderResultPreview: renderResultPreview,
        registerChartKind: registerChartKind
    };
})(typeof window !== 'undefined' ? window : this);

(function () {
    'use strict';

    /** Контракт: сервер отдаёт ReportResultViewModel; поля формы — reportCustomConfig ниже. */
    var modalId = 'report-workflow-modal';

    var reportCustomConfig = {};

    var state = {
        selectedReportId: '',
        titlesById: {},
        descriptionsById: {},
        filters: {
            periodFrom: '',
            periodTo: '',
            customByReport: {}
        },
        options: { cabinetOptions: [], doctorOptions: [], categoryOptions: [] },
        lastResult: null,
        previewCharts: []
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
        destroyPreviewCharts();
        if (lastFocusedElement && document.contains(lastFocusedElement)) {
            lastFocusedElement.focus();
        }
        lastFocusedElement = null;
    }

    function setDemoDataBadge(isDemo) {
        var badge = document.getElementById('reports-demo-badge');
        if (!badge) return;
        if (isDemo) {
            badge.hidden = false;
            badge.classList.remove('reports-demo-badge--hidden');
        } else {
            badge.hidden = true;
            badge.classList.add('reports-demo-badge--hidden');
        }
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
            setDemoDataBadge(!!payload.usingElectronicQueueMockData);
            reportCustomConfig = payload.reportCustomConfig && typeof payload.reportCustomConfig === 'object'
                ? payload.reportCustomConfig
                : {};
            applyCatalogTitles(payload.catalog);
        } catch (_) {
            state.selectedReportId = '';
        }
    }

    function applyCatalogTitles(catalog) {
        var titlesById = {};
        var descriptionsById = {};
        if (Array.isArray(catalog)) {
            catalog.forEach(function (item) {
                var id = item && item.id ? String(item.id).trim() : '';
                if (!id) return;
                if (item.title) titlesById[id] = String(item.title).trim();
                if (item.description) descriptionsById[id] = String(item.description).trim();
            });
        }
        state.titlesById = titlesById;
        state.descriptionsById = descriptionsById;
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
        var core = window.ReportPreviewCore || {};
        var esc = typeof core.escapeHtml === 'function' ? core.escapeHtml : function (x) { return String(x); };
        var escAttr = typeof core.escapeHtmlAttribute === 'function' ? core.escapeHtmlAttribute : function (x) { return String(x); };
        var html = '';
        fields.forEach(function (f) {
            var value = values[f.key] != null ? String(values[f.key]) : '';
            if (f.type === 'select') {
                var opts = f.options || [];
                if (!value && opts.length > 0) {
                    value = String(opts[0].value);
                    values[f.key] = value;
                }
            }
            html += '<label class="form-field"><span class="form-field__label">' + esc(f.label) + '</span>';
            if (f.type === 'select') {
                html += '<select class="form-input" data-custom-key="' + escAttr(f.key) + '">';
                (f.options || []).forEach(function (opt) {
                    html += '<option value="' + escAttr(opt.value) + '"' + (value === String(opt.value) ? ' selected="selected"' : '') + '>' + esc(opt.label) + '</option>';
                });
                html += '</select>';
            } else if (f.type === 'select-dynamic') {
                var list = state.options[f.source] || [];
                var placeholderLabel = f.placeholderLabel || 'Все';
                html += '<select class="form-input" data-custom-key="' + escAttr(f.key) + '"><option value="">' + esc(placeholderLabel) + '</option>';
                list.forEach(function (opt) {
                    var oid = String(opt.id);
                    html += '<option value="' + escAttr(oid) + '"' + (value === oid ? ' selected="selected"' : '') + '>' + esc(opt.label) + '</option>';
                });
                html += '</select>';
            } else {
                var type = f.type === 'date' ? 'date' : (f.type || 'text');
                var minAttr = f.min ? ' min="' + escAttr(f.min) + '"' : '';
                var pAttr = f.placeholder ? ' placeholder="' + escAttr(f.placeholder) + '"' : '';
                html += '<input class="form-input" type="' + escAttr(type) + '" data-custom-key="' + escAttr(f.key) + '" value="' + escAttr(value) + '"' + minAttr + pAttr + ' />';
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
        if (Object.keys(state.titlesById).length > 0)
            return;
        var node = document.getElementById('reports-page-data');
        if (!node) return;
        try {
            var payload = JSON.parse(node.textContent || '{}');
            applyCatalogTitles(payload.catalog);
        } catch (_) { /* ignore */ }
    }

    function setModalTitle(title) {
        var node = document.getElementById('report-workflow-title');
        if (node) node.textContent = title || 'Отчёт';
        var dialog = getModal() && getModal().querySelector('.app-modal__dialog');
        if (dialog) dialog.setAttribute('aria-label', title || 'Отчёт');
    }

    function setModalPreviewSubtitle(text) {
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

    function resolveCustomFieldDisplayLabel(field, value) {
        if (!field) return '';
        var raw = value != null ? String(value).trim() : '';
        if (!raw && field.type === 'select' && field.options && field.options.length) {
            raw = String(field.options[0].value);
        }
        if (!raw) return '';
        if (field.type === 'select' && field.options && field.options.length) {
            var match = field.options.find(function (opt) {
                return String(opt.value) === raw;
            });
            if (match && match.label) return String(match.label);
        }
        return raw;
    }

    function formatDateTimeLocalForSubtitle(value) {
        if (!value || typeof value !== 'string') return '…';
        var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value.trim());
        if (!m) return value;
        return m[3] + '.' + m[2] + '.' + m[1] + ' ' + m[4] + ':' + m[5];
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

    function formatPreviewSubtitle() {
        return formatPeriodHint();
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
                setModalPreviewSubtitle('');
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

    function destroyPreviewCharts() {
        (state.previewCharts || []).forEach(function (ch) {
            try {
                ch.destroy();
            } catch (_) { /* ignore */ }
        });
        state.previewCharts = [];
    }

    function renderPreviewTable(result) {
        var core = window.ReportPreviewCore;
        if (!core || typeof core.renderResultPreview !== 'function') {
            var root = document.getElementById('report-preview-content');
            if (root) root.innerHTML = '<p class="report-params__empty">Не загружен модуль предпросмотра.</p>';
            return;
        }
        core.renderResultPreview(result, {
            destroyCharts: destroyPreviewCharts,
            showInfoToast: showInfoToast,
            onChartsMounted: function (charts) {
                state.previewCharts = charts || [];
            }
        });
    }

    async function generateReport() {
        // Предпросмотр: POST /Reports/Generate — таблица может быть усечена; диаграммы из previewCharts/previewPieChart (полные агрегаты на сервере).
        if (!state.selectedReportId) return;
        collectFiltersFromUi();
        setGenerating(true);
        var core = window.ReportPreviewCore;
        var payload = core && typeof core.buildReportRequestPayload === 'function'
            ? core.buildReportRequestPayload(state, { reportCustomConfig: reportCustomConfig })
            : { reportId: state.selectedReportId, dateFrom: null, dateTo: null, weekStart: null, cabinetId: null, doctorId: null, customParams: {} };
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
            setDemoDataBadge(!!data.isDemoData);
            state.lastResult = data.result || null;
            var previewTitle = state.lastResult && state.lastResult.title ? state.lastResult.title : (state.titlesById[state.selectedReportId] || 'Предпросмотр');
            setModalTitle(previewTitle);
            setModalPreviewSubtitle(formatPreviewSubtitle());
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
        var core = window.ReportPreviewCore;
        var base = core && typeof core.buildReportRequestPayload === 'function'
            ? core.buildReportRequestPayload(state, { reportCustomConfig: reportCustomConfig })
            : { reportId: state.selectedReportId, dateFrom: null, dateTo: null, weekStart: null, cabinetId: null, doctorId: null, customParams: {} };
        base.format = format;
        return base;
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
        if (n.endsWith('.html')) return 'html';
        if (n.endsWith('.htm')) return 'html';
        if (n.endsWith('.csv')) return 'csv';
        return 'csv';
    }

    function defaultExportBaseName() {
        var id = state.selectedReportId || 'report';
        return String(id).replace(/[^\w.-]+/g, '_');
    }

    async function fetchExportBlob(format) {
        // Файл: POST /Reports/Export — полный пересчёт отчёта на сервере, без лимита строк предпросмотра.
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
                    suggestedName: base + '.pdf',
                    types: [
                        { description: 'PDF', accept: { 'application/pdf': ['.pdf'] } },
                        { description: 'HTML', accept: { 'text/html': ['.html', '.htm'] } },
                        { description: 'CSV', accept: { 'text/csv': ['.csv'] } },
                        { description: 'Excel', accept: { 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': ['.xlsx'] } }
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
                setModalPreviewSubtitle('');
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
        setModalPreviewSubtitle('');
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
