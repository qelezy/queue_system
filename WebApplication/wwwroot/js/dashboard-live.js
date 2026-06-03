(function () {
    const root = document.querySelector("[data-dashboard-live]");
    if (!root) return;

    function showErrorToast(message) {
        window.AppToasts?.getManager("global-toast-stack")?.show(message, "error");
    }

    if (typeof signalR === "undefined") {
        showErrorToast("Не загружена библиотека SignalR");
        return;
    }

    const ui = {
        waiting: root.dataset.uiWaiting === "true",
        inService: root.dataset.uiInService === "true",
        acceptedToday: root.dataset.uiAcceptedToday === "true",
        ticketsIssued: root.dataset.uiTicketsIssued === "true",
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

    function toDisplayNumber(value) {
        const n = Number(value);
        return String(Number.isFinite(n) ? n : 0);
    }

    function setStatValue(statKey, value, unit) {
        const card = root.querySelector('[data-stat="' + statKey + '"]');
        if (!card) return;
        const valueEl = card.querySelector(".stat-card-value");
        if (!valueEl) return;
        valueEl.textContent = "";
        valueEl.appendChild(document.createTextNode(toDisplayNumber(value)));
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
        subValueEl.appendChild(document.createTextNode(toDisplayNumber(subValue)));
        if (subUnit) {
            const span = document.createElement("span");
            span.className = "stat-card-sub-unit";
            span.textContent = subUnit;
            subValueEl.appendChild(span);
        }
    }

    function updateStats(dto) {
        if (ui.waiting) setStatValue("waiting", dto.waitingCount ?? 0, "");
        if (ui.inService) setStatValue("in-service", dto.inServiceCount ?? 0, "");
        if (ui.acceptedToday) setStatValue("accepted-today", dto.acceptedTodayCount ?? 0, "");
        if (ui.ticketsIssued) setStatValue("tickets-issued", dto.ticketsIssuedTodayCount ?? 0, "");
    }

    function buildQueueStatusBadge(label, code) {
        const mod = code === "called" ? "called" : "waiting";
        return (
            '<span class="queue-status-badge queue-status-badge--' +
            mod +
            '">' +
            escapeHtml(label ?? "") +
            "</span>"
        );
    }

    function buildQueueRowHtml(r) {
        return (
            '<tr class="queue-row--clickable" data-queue-row' +
            ' data-appointment-id="' + escapeHtml(r.idAppointment) + '"' +
            ' data-specialty-id="' + escapeHtml(r.idSpecialty) + '"' +
            ' data-status-code="' + escapeHtml(r.statusCode) + '"' +
            ' data-wait="' + escapeHtml(toDisplayNumber(r.waitingMinutes)) + '"' +
            ' tabindex="0" role="button">' +
            "<td>" + escapeHtml(r.ticketNumber) + "</td>" +
            '<td><div class="queue-row__doctor-name">' + escapeHtml(r.currentDoctor) + "</div>" +
            '<div class="queue-row__doctor-specialty">' + escapeHtml(r.specialty) + "</div></td>" +
            "<td>" + escapeHtml(r.currentCabinet) + "</td>" +
            "<td>" + buildQueueStatusBadge(r.statusLabel, r.statusCode) + "</td>" +
            "<td>" + escapeHtml(toDisplayNumber(r.waitingMinutes)) + " мин</td>" +
            "</tr>"
        );
    }

    function updateQueueCountBadge(count) {
        if (!ui.queueTable) return;

        const countValue = toDisplayNumber(count);
        const label = "Ожидающих: " + countValue;
        let badge = document.querySelector(".queue-list-count-badge");

        if (!badge) {
            const title = document.querySelector(".queue-panel__title");
            const titleText = title?.querySelector(".queue-panel__title-text");
            if (!(title instanceof HTMLElement) || !(titleText instanceof HTMLElement)) return;

            badge = document.createElement("span");
            badge.className = "queue-list-count-badge";
            titleText.insertAdjacentElement("afterend", badge);
        }

        badge.textContent = label;
        badge.setAttribute("aria-label", label);
        badge.hidden = false;
    }

    function updateQueueTable(rows) {
        if (!ui.queueTable) return;
        const table = document.querySelector(".queue-table-scroll .users-table");
        if (!(table instanceof HTMLTableElement)) return;
        const tbody = table.tBodies[0];
        if (!tbody) return;

        if (!rows || rows.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5">Лист ожидания пуст</td></tr>';
        } else {
            tbody.innerHTML = rows.map(buildQueueRowHtml).join("");
        }

        updateQueueCountBadge(rows?.length ?? 0);
        window.QueueTable?.rebind?.();
    }

    function buildDoctorRowHtml(d) {
        const hasCurrent =
            d.isInService &&
            d.currentServiceMinutes != null &&
            Number.isFinite(Number(d.currentServiceMinutes));
        const cur = hasCurrent ? Number(d.currentServiceMinutes) : null;
        const hasNorm = d.normServiceMinutes != null && Number.isFinite(Number(d.normServiceMinutes));
        const norm = hasNorm ? Number(d.normServiceMinutes) : 0;
        const over = hasCurrent && hasNorm && cur > norm;
        const specialtyLine =
            escapeHtml(d.specialty) + (d.cabinet ? " · " + escapeHtml(d.cabinet) : "");

        let statusBadge;
        if (d.isInService) {
            statusBadge =
                '<span class="queue-status-badge queue-status-badge--in-service">Принимает</span>';
        } else {
            statusBadge = '<span class="queue-status-badge queue-status-badge--free">Ожидает пациента</span>';
        }

        const doctorStatus = d.isInService ? "in-service" : "free";
        return (
            '<tr data-doctor-load-row data-doctor-id="' + escapeHtml(d.idDoctor) + '"' +
            ' data-doctor-status="' + doctorStatus + '"' +
            ' data-specialty-id="' + escapeHtml(d.idSpecialty) + '">' +
            '<td><div class="queue-row__doctor-name">' + escapeHtml(d.fullName) + "</div>" +
            '<div class="queue-row__doctor-specialty">' + specialtyLine + "</div></td>" +
            "<td>" + statusBadge + "</td>" +
            '<td class="' + (over ? "doctor-load-table__cell--over" : "") + '">' +
            (hasCurrent ? escapeHtml(toDisplayNumber(cur)) + " мин" : "—") +
            "</td>" +
            "<td>" + (hasNorm ? escapeHtml(toDisplayNumber(norm)) + " мин" : "—") + "</td>" +
            "<td>" + escapeHtml(toDisplayNumber(d.queueLength)) + "</td>" +
            "</tr>"
        );
    }

    function updateShiftBadge(onShift, total) {
        if (!ui.doctorLoad) return;

        const onShiftValue = toDisplayNumber(onShift);
        const totalValue = toDisplayNumber(total);
        const label = "На смене: " + onShiftValue + "/" + totalValue;
        const ariaLabel = "На смене: " + onShiftValue + " из " + totalValue;
        let badge = document.querySelector(".doctor-load-shift-badge");

        if (!badge) {
            const title = document.querySelector(".doctor-load-panel__title");
            const titleText = title?.querySelector(".doctor-load-panel__title-text");
            if (!(title instanceof HTMLElement) || !(titleText instanceof HTMLElement)) return;

            badge = document.createElement("span");
            badge.className = "doctor-load-shift-badge";
            titleText.insertAdjacentElement("afterend", badge);
        }

        badge.textContent = label;
        badge.setAttribute("aria-label", ariaLabel);
        badge.hidden = false;
    }

    function updateDelaysBadge(cards) {
        if (!cards) return;

        const delaysCount = cards.filter(function (d) {
            const cur = d.currentServiceMinutes;
            const norm = d.normServiceMinutes;
            return (
                d.isInService &&
                cur != null &&
                norm != null &&
                Number.isFinite(Number(cur)) &&
                Number.isFinite(Number(norm)) &&
                Number(cur) > Number(norm)
            );
        }).length;

        let badge = document.querySelector(".doctor-load-delays-badge");
        const label = "Задержек: " + delaysCount;

        if (delaysCount === 0) {
            badge?.remove();
            return;
        }

        if (!badge) {
            const title = document.querySelector(".doctor-load-panel__title");
            const titleText = title?.querySelector(".doctor-load-panel__title-text");
            if (!(title instanceof HTMLElement) || !(titleText instanceof HTMLElement)) return;

            badge = document.createElement("span");
            badge.className = "doctor-load-delays-badge";
            const anchor = document.querySelector(".doctor-load-shift-badge") ?? titleText;
            anchor.insertAdjacentElement("afterend", badge);
        }

        badge.textContent = label;
        badge.setAttribute("aria-label", label);
        badge.hidden = false;
    }

    function updateDoctorLoad(cards, total) {
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

        updateShiftBadge(cards?.length ?? 0, total ?? 0);
        updateDelaysBadge(cards);
        window.DoctorLoadTable?.rebind?.();
    }

    function onDashboardUpdated(dto) {
        if (!dto) return;
        updateStats(dto);
        updateQueueTable(dto.activeQueue);
        updateDoctorLoad(dto.doctorLoadCards, dto.doctorsTotalCount);
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/dashboard", { withCredentials: true })
        .withAutomaticReconnect()
        .build();

    let hadSuccessfulConnection = false;

    connection.on("DashboardUpdated", onDashboardUpdated);

    connection.onclose(function () {
        if (hadSuccessfulConnection) {
            showErrorToast("Соединение с live-обновлениями прервано");
        }
    });

    connection
        .start()
        .then(function () {
            hadSuccessfulConnection = true;
        })
        .catch(function () {
            showErrorToast("Ошибка подключения к live-обновлениям");
        });
})();
