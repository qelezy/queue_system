(function () {
    const root = document.querySelector("[data-dashboard-live]");
    if (!root || typeof signalR === "undefined") return;

    const ui = {
        waiting: root.dataset.uiWaiting === "true",
        inService: root.dataset.uiInService === "true",
        acceptedToday: root.dataset.uiAcceptedToday === "true",
        avgWait: root.dataset.uiAvgWait === "true",
        avgService: root.dataset.uiAvgService === "true",
        queueTable: root.dataset.uiQueueTable === "true",
        doctorLoad: root.dataset.uiDoctorLoad === "true",
    };

    function escapeHtml(text) {
        return String(text ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function setStatValue(statKey, value, unit) {
        const card = root.querySelector('[data-stat="' + statKey + '"]');
        if (!card) return;
        const valueEl = card.querySelector(".stat-card-value");
        if (!valueEl) return;
        valueEl.textContent = "";
        valueEl.appendChild(document.createTextNode(String(value)));
        if (unit) {
            const span = document.createElement("span");
            span.className = "stat-card-value-unit";
            span.textContent = unit;
            valueEl.appendChild(span);
        }
    }

    function setStatSubValue(statKey, subValue, subUnit) {
        const card = root.querySelector('[data-stat="' + statKey + '"]');
        if (!card) return;
        const subValueEl = card.querySelector(".stat-card-sub-value");
        if (!subValueEl) return;
        subValueEl.textContent = "";
        subValueEl.appendChild(document.createTextNode(String(subValue)));
        if (subUnit) {
            const span = document.createElement("span");
            span.className = "stat-card-sub-unit";
            span.textContent = subUnit;
            subValueEl.appendChild(span);
        }
    }

    function updateStats(dto) {
        if (ui.waiting) setStatValue("waiting", dto.waitingCount, "");
        if (ui.inService) setStatValue("in-service", dto.inServiceCount, "");
        if (ui.acceptedToday) setStatValue("accepted-today", dto.acceptedTodayCount, "");
        if (ui.avgWait) {
            setStatValue("avg-wait", dto.avgWaitMinutes, "мин");
            setStatSubValue("avg-wait", dto.maxWaitMinutes, "мин");
        }
        if (ui.avgService) {
            setStatValue("avg-service", dto.avgServiceMinutes, "мин");
            setStatSubValue("avg-service", dto.maxServiceMinutes, "мин");
        }
    }

    function buildQueueRowHtml(r) {
        return (
            '<tr data-queue-row' +
            ' data-specialty="' + escapeHtml(r.specialty) + '"' +
            ' data-status="' + escapeHtml(r.statusCode) + '"' +
            ' data-wait="' + escapeHtml(r.waitingMinutes) + '">' +
            "<td>" + escapeHtml(r.patient) + "</td>" +
            '<td><div class="queue-row__doctor-name">' + escapeHtml(r.currentDoctor) + "</div>" +
            '<div class="queue-row__doctor-specialty">' + escapeHtml(r.specialty) + "</div></td>" +
            "<td>" + escapeHtml(r.currentCabinet) + "</td>" +
            "<td>" + escapeHtml(r.arrivalTime) + "</td>" +
            "<td>" + escapeHtml(r.waitingMinutes) + " мин</td>" +
            '<td><span class="queue-status-badge queue-status-badge--' + escapeHtml(r.statusCode) + '">' +
            escapeHtml(r.statusLabel) + "</span></td>" +
            "</tr>"
        );
    }

    function updateQueueTable(rows) {
        if (!ui.queueTable) return;
        const table = document.querySelector(".queue-table-scroll .users-table");
        if (!(table instanceof HTMLTableElement)) return;
        const tbody = table.tBodies[0];
        if (!tbody) return;

        if (!rows || rows.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6">Нет записей в очереди</td></tr>';
        } else {
            tbody.innerHTML = rows.map(buildQueueRowHtml).join("");
        }

        window.QueueTable?.rebind?.();
    }

    function buildDoctorRowHtml(d) {
        const hasCurrent = d.isInService && d.currentServiceMinutes != null;
        const hasNorm = d.normServiceMinutes != null;
        const norm = d.normServiceMinutes ?? 0;
        const cur = d.currentServiceMinutes ?? 0;
        const over = hasCurrent && hasNorm && cur > norm;
        const specialtyLine =
            escapeHtml(d.specialty) + (d.cabinet ? " · " + escapeHtml(d.cabinet) : "");

        let statusBadge;
        if (d.isInService) {
            statusBadge =
                '<span class="queue-status-badge queue-status-badge--in-service">На приёме</span>';
        } else {
            statusBadge = '<span class="queue-status-badge queue-status-badge--free">Свободен</span>';
        }

        return (
            '<tr data-doctor-load-row data-doctor-id="' + escapeHtml(d.idDoctor) + '">' +
            '<td><div class="queue-row__doctor-name">' + escapeHtml(d.fullName) + "</div>" +
            '<div class="queue-row__doctor-specialty">' + specialtyLine + "</div></td>" +
            "<td>" + statusBadge + "</td>" +
            '<td class="' + (over ? "doctor-load-table__cell--over" : "") + '">' +
            (hasCurrent ? escapeHtml(cur) + " мин" : "—") +
            "</td>" +
            "<td>" + (hasNorm ? escapeHtml(norm) + " мин" : "—") + "</td>" +
            "<td>" + escapeHtml(d.queueLength) + "</td>" +
            "</tr>"
        );
    }

    function updateDelaysBadge(cards) {
        const badge = document.querySelector(".doctor-load-delays-badge");
        if (!badge || !cards) return;
        const delaysCount = cards.filter(function (d) {
            return (
                d.isInService &&
                d.normServiceMinutes != null &&
                d.currentServiceMinutes != null &&
                d.currentServiceMinutes > d.normServiceMinutes
            );
        }).length;
        badge.textContent = "Задержек: " + delaysCount;
        badge.setAttribute("aria-label", "Задержек: " + delaysCount);
    }

    function updateDoctorLoad(cards) {
        if (!ui.doctorLoad) return;
        const table = document.querySelector(".doctor-load-table");
        if (!(table instanceof HTMLTableElement)) return;
        const tbody = table.tBodies[0];
        if (!tbody) return;

        if (!cards || cards.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5">Нет активных врачей</td></tr>';
        } else {
            tbody.innerHTML = cards.map(buildDoctorRowHtml).join("");
        }

        updateDelaysBadge(cards);
        window.DoctorLoadTable?.rebind?.();
    }

    function onDashboardUpdated(dto) {
        if (!dto) return;
        updateStats(dto);
        updateQueueTable(dto.activeQueue);
        updateDoctorLoad(dto.doctorLoadCards);
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/dashboard")
        .withAutomaticReconnect()
        .build();

    connection.on("DashboardUpdated", onDashboardUpdated);

    connection.start().catch(function () { /* live updates unavailable */ });
})();
