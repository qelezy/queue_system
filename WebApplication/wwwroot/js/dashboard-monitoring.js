(function () {
    "use strict";

    function readPayload() {
        var el = document.getElementById("dashboard-chart-data");
        if (!el || !el.textContent) return null;
        try {
            return JSON.parse(el.textContent);
        } catch (e) {
            return null;
        }
    }

    function baseChartOptions() {
        return {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: "top", align: "end" } }
        };
    }

    document.addEventListener("DOMContentLoaded", function () {
        if (typeof Chart === "undefined") return;

        Chart.defaults.font.family = "'Poppins', sans-serif";
        Chart.defaults.color = "#555";

        var teal = "#00b3b8";
        var amber = "#c9a227";
        var payload = readPayload();
        if (!payload) return;

        var hourlyCtx = document.getElementById("chartWaitServeHourly");
        if (hourlyCtx && payload.hourly) {
            new Chart(hourlyCtx, {
                type: "line",
                data: {
                    labels: payload.hourly.labels,
                    datasets: [
                        {
                            label: "Ожидание, мин",
                            data: payload.hourly.wait,
                            borderColor: teal,
                            backgroundColor: "rgba(0,179,184,0.12)",
                            fill: true,
                            tension: 0.35,
                            pointRadius: 3
                        },
                        {
                            label: "Приём, мин",
                            data: payload.hourly.service,
                            borderColor: amber,
                            backgroundColor: "rgba(201,162,39,0.08)",
                            fill: true,
                            tension: 0.35,
                            pointRadius: 3
                        }
                    ]
                },
                options: Object.assign({}, baseChartOptions(), {
                    scales: {
                        y: { beginAtZero: true, title: { display: true, text: "Минуты" }, grid: { color: "rgba(0,0,0,0.06)" } },
                        x: { grid: { display: false } }
                    }
                })
            });
        }

        var loadCtx = document.getElementById("chartLoadToday");
        var loadChart = null;
        if (loadCtx && payload.load) {
            function buildLoadConfig(mode) {
                var isCabinet = mode === "cabinet";
                var metric = document.querySelector(".load-metric-toggle .is-active");
                var m = metric && metric.getAttribute("data-metric") === "busy" ? "busy" : "completed";
                var labels = isCabinet ? payload.load.cabinetLabels : payload.load.doctorLabels;
                var data =
                    m === "busy"
                        ? isCabinet
                            ? payload.load.cabinetBusy
                            : payload.load.doctorBusy
                        : isCabinet
                          ? payload.load.cabinetCompleted
                          : payload.load.doctorCompleted;
                var label = m === "busy" ? "Занятость, %" : "Завершённых приёмов";
                return {
                    type: "bar",
                    data: {
                        labels: labels,
                        datasets: [
                            {
                                label: label,
                                data: data,
                                backgroundColor: "rgba(0,179,184,0.72)",
                                borderRadius: 6
                            }
                        ]
                    },
                    options: {
                        indexAxis: "y",
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: { legend: { display: false } },
                        scales: {
                            x:
                                m === "busy"
                                    ? { max: 100, beginAtZero: true, title: { display: true, text: "%" }, grid: { color: "rgba(0,0,0,0.06)" } }
                                    : { beginAtZero: true, ticks: { stepSize: 1 }, grid: { color: "rgba(0,0,0,0.06)" } },
                            y: { grid: { display: false } }
                        }
                    }
                };
            }

            function renderLoad(mode) {
                var cfg = buildLoadConfig(mode);
                if (loadChart) loadChart.destroy();
                loadChart = new Chart(loadCtx, cfg);
            }

            renderLoad("cabinet");

            document.querySelectorAll(".load-entity-toggle [data-entity]").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    document.querySelectorAll(".load-entity-toggle [data-entity]").forEach(function (b) {
                        b.classList.remove("is-active");
                    });
                    btn.classList.add("is-active");
                    renderLoad(btn.getAttribute("data-entity"));
                });
            });

            document.querySelectorAll(".load-metric-toggle [data-metric]").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    document.querySelectorAll(".load-metric-toggle [data-metric]").forEach(function (b) {
                        b.classList.remove("is-active");
                    });
                    btn.classList.add("is-active");
                    var ent = document.querySelector(".load-entity-toggle .is-active");
                    renderLoad(ent ? ent.getAttribute("data-entity") : "cabinet");
                });
            });
        }

        if (!payload.manager) return;

        var m = payload.manager;
        var palette = ["#00b3b8", "#0d3d40", "#c9a227", "#5c8f82", "#8b6f47", "#6b7c93", "#a8556b"];

        var qhCtx = document.getElementById("chartQueueByHour");
        var qhChart = null;
        if (qhCtx && m.queueByDay && m.queueHourLabels) {
            function buildQueueHourDatasets(showPerDay) {
                var ds = [];
                if (showPerDay && m.queueByDay.length) {
                    m.queueByDay.forEach(function (day, i) {
                        ds.push({
                            label: day.dayLabel,
                            data: day.values,
                            borderColor: palette[i % palette.length],
                            backgroundColor: "transparent",
                            tension: 0.25,
                            pointRadius: 2,
                            fill: false
                        });
                    });
                    ds.push({
                        label: "Среднее по дням",
                        data: m.queueDailyAvg,
                        borderColor: "#111",
                        borderDash: [6, 4],
                        backgroundColor: "transparent",
                        tension: 0.25,
                        pointRadius: 3,
                        fill: false
                    });
                } else {
                    ds.push({
                        label: "Среднее по дням",
                        data: m.queueDailyAvg,
                        borderColor: teal,
                        backgroundColor: "rgba(0,179,184,0.08)",
                        tension: 0.25,
                        pointRadius: 4,
                        fill: true
                    });
                }
                return ds;
            }

            function renderQueueHour(showPerDay) {
                var cfg = {
                    type: "line",
                    data: {
                        labels: m.queueHourLabels,
                        datasets: buildQueueHourDatasets(showPerDay)
                    },
                    options: Object.assign({}, baseChartOptions(), {
                        scales: {
                            y: { beginAtZero: true, title: { display: true, text: "Пациентов" }, grid: { color: "rgba(0,0,0,0.06)" } },
                            x: { grid: { display: false } }
                        }
                    })
                };
                if (qhChart) qhChart.destroy();
                qhChart = new Chart(qhCtx, cfg);
            }

            renderQueueHour(true);

            document.querySelectorAll(".manager-queue-mode [data-queue-mode]").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    document.querySelectorAll(".manager-queue-mode [data-queue-mode]").forEach(function (b) {
                        b.classList.remove("is-active");
                    });
                    btn.classList.add("is-active");
                    renderQueueHour(btn.getAttribute("data-queue-mode") === "per-day");
                });
            });
        }

        var histCtx = document.getElementById("chartWaitHistogram");
        if (histCtx && m.histogram && m.histogram.length) {
            new Chart(histCtx, {
                type: "bar",
                data: {
                    labels: m.histogram.map(function (x) {
                        return x.label;
                    }),
                    datasets: [
                        {
                            label: "Количество этапов",
                            data: m.histogram.map(function (x) {
                                return x.count;
                            }),
                            backgroundColor: "rgba(13,61,64,0.75)",
                            borderRadius: 6
                        }
                    ]
                },
                options: Object.assign({}, baseChartOptions(), {
                    plugins: { legend: { display: false } },
                    scales: {
                        y: { beginAtZero: true, ticks: { stepSize: 1 }, grid: { color: "rgba(0,0,0,0.06)" } },
                        x: { grid: { display: false } }
                    }
                })
            });
        }

        function horizontalBar(canvasId, rows, label) {
            var ctx = document.getElementById(canvasId);
            if (!ctx || !rows || !rows.length) return;
            new Chart(ctx, {
                type: "bar",
                data: {
                    labels: rows.map(function (r) {
                        return r.name;
                    }),
                    datasets: [
                        {
                            label: label,
                            data: rows.map(function (r) {
                                return Math.round(r.valueMinutes * 10) / 10;
                            }),
                            backgroundColor: "rgba(0,179,184,0.7)",
                            borderRadius: 6
                        }
                    ]
                },
                options: {
                    indexAxis: "y",
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { beginAtZero: true, title: { display: true, text: "Мин" }, grid: { color: "rgba(0,0,0,0.06)" } },
                        y: { grid: { display: false } }
                    }
                }
            });
        }

        horizontalBar("chartAvgWaitDoctors", m.avgWaitByDoctor, "Среднее ожидание, мин");
        horizontalBar("chartAvgServiceDoctors", m.avgServiceByDoctor, "Средний приём, мин");
    });
})();
