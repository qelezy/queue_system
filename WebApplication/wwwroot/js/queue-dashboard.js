(function () {
    if (typeof Chart === "undefined") {
        return;
    }

    Chart.defaults.font.family = "'Poppins', sans-serif";
    Chart.defaults.color = "#555";

    const teal = "#00b3b8";
    const amber = "#c9a227";

    const waitServeCtx = document.getElementById("chartWaitServe");
    if (waitServeCtx) {
        new Chart(waitServeCtx, {
            type: "line",
            data: {
                labels: ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"],
                datasets: [
                    {
                        label: "Ожидание, мин",
                        data: [24, 19, 22, 18, 21, 16, 14],
                        borderColor: teal,
                        backgroundColor: "rgba(0, 179, 184, 0.12)",
                        fill: true,
                        tension: 0.35,
                        pointRadius: 4
                    },
                    {
                        label: "Прием, мин",
                        data: [16, 17, 15, 18, 17, 14, 13],
                        borderColor: amber,
                        backgroundColor: "rgba(201, 162, 39, 0.08)",
                        fill: true,
                        tension: 0.35,
                        pointRadius: 4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false
            }
        });
    }

    const cabinetsCtx = document.getElementById("chartCabinets");
    if (cabinetsCtx) {
        new Chart(cabinetsCtx, {
            type: "bar",
            data: {
                labels: ["Каб. 101", "Каб. 102", "Каб. 103", "Каб. 201", "Каб. 202", "Каб. 203"],
                datasets: [{
                    label: "Загрузка, %",
                    data: [88, 76, 82, 91, 69, 74],
                    backgroundColor: [
                        "rgba(0, 179, 184, 0.75)",
                        "rgba(0, 179, 184, 0.55)",
                        "rgba(0, 179, 184, 0.65)",
                        "rgba(13, 61, 64, 0.85)",
                        "rgba(0, 179, 184, 0.5)",
                        "rgba(13, 61, 64, 0.65)"
                    ],
                    borderRadius: 6
                }]
            },
            options: {
                indexAxis: "y",
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } }
            }
        });
    }
})();
