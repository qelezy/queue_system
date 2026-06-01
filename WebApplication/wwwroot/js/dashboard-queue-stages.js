(function () {
    const modalId = "queue-completed-stages-modal";
    const modal = document.getElementById(modalId);
    const dialog = modal?.querySelector(".app-modal__dialog");
    const titleEl = document.getElementById("queue-completed-stages-title");
    const tbody = document.getElementById("queue-completed-stages-tbody");
    const tableWrap = document.querySelector(".queue-table-scroll");
    const tbodyQueue = tableWrap?.querySelector("tbody");

    if (!modal || !tbody || !tbodyQueue) return;

    let lastFocusedElement = null;

    function escapeHtml(text) {
        return String(text ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function setModalHiddenState(isHidden) {
        modal.setAttribute("aria-hidden", isHidden ? "true" : "false");
        if ("inert" in modal) {
            modal.inert = isHidden;
        }
    }

    function openModal() {
        lastFocusedElement =
            document.activeElement instanceof HTMLElement ? document.activeElement : null;
        modal.classList.add("is-open");
        setModalHiddenState(false);
        const closeBtn = modal.querySelector('[data-modal-close="' + modalId + '"]');
        if (closeBtn instanceof HTMLElement) {
            closeBtn.focus();
        } else if (dialog instanceof HTMLElement) {
            dialog.focus();
        }
    }

    function closeModal() {
        const active = document.activeElement;
        if (active instanceof HTMLElement && modal.contains(active)) {
            active.blur();
        }
        modal.classList.remove("is-open");
        setModalHiddenState(true);
        if (lastFocusedElement && document.contains(lastFocusedElement)) {
            lastFocusedElement.focus();
        }
    }

    function buildStatusBadge(label, code) {
        const safeCode = code ? String(code).trim() : "";
        const modifier = safeCode ? " queue-status-badge--" + safeCode : "";
        return (
            '<span class="queue-status-badge' +
            modifier +
            '">' +
            escapeHtml(label) +
            "</span>"
        );
    }

    function renderStages(stages) {
        if (!stages || stages.length === 0) {
            tbody.innerHTML = '<tr><td colspan="6">Нет этапов</td></tr>';
            return;
        }
        tbody.innerHTML = stages
            .map(
                (s) =>
                    "<tr>" +
                    "<td>" +
                    escapeHtml(s.specialty) +
                    "</td>" +
                    "<td>" +
                    escapeHtml(s.cabinet) +
                    "</td>" +
                    "<td>" +
                    buildStatusBadge(s.statusLabel, s.statusCode) +
                    "</td>" +
                    "<td>" +
                    escapeHtml(s.timeCall) +
                    "</td>" +
                    "<td>" +
                    escapeHtml(s.timeStart) +
                    "</td>" +
                    "<td>" +
                    escapeHtml(s.timeEnd) +
                    "</td>" +
                    "</tr>"
            )
            .join("");
    }

    function renderLoading() {
        tbody.innerHTML = '<tr><td colspan="6">Загрузка…</td></tr>';
    }

    function renderError(message) {
        tbody.innerHTML =
            '<tr><td colspan="6">' + escapeHtml(message || "Не удалось загрузить этапы") + "</td></tr>";
    }

    async function openForAppointment(appointmentId) {
        const id = Number(appointmentId);
        if (!Number.isFinite(id) || id <= 0) return;

        titleEl.textContent = "Этапы маршрута";
        renderLoading();
        openModal();

        try {
            const response = await fetch(
                "/dashboard/appointments/" + encodeURIComponent(id) + "/route-stages",
                { credentials: "same-origin", headers: { Accept: "application/json" } }
            );

            if (response.status === 404) {
                renderError("Талон не найден");
                return;
            }
            if (!response.ok) {
                renderError("Сервис очереди недоступен");
                return;
            }

            const data = await response.json();
            const ticket = data.ticketNumber ? String(data.ticketNumber).trim() : "";
            titleEl.textContent = ticket
                ? "Этапы маршрута — талон " + ticket
                : "Этапы маршрута";
            renderStages(data.stages);
        } catch {
            renderError("Не удалось загрузить этапы");
        }
    }

    function getRowFromEvent(event) {
        const target = event.target;
        if (!(target instanceof Element)) return null;
        const row = target.closest("tr[data-queue-row]");
        if (!(row instanceof HTMLTableRowElement)) return null;
        if (row.style.display === "none") return null;
        return row;
    }

    tbodyQueue.addEventListener("click", (event) => {
        const row = getRowFromEvent(event);
        if (!row) return;
        const appointmentId = row.dataset.appointmentId;
        if (!appointmentId) return;
        openForAppointment(appointmentId);
    });

    tbodyQueue.addEventListener("keydown", (event) => {
        if (event.key !== "Enter" && event.key !== " ") return;
        const row = getRowFromEvent(event);
        if (!row) return;
        event.preventDefault();
        const appointmentId = row.dataset.appointmentId;
        if (!appointmentId) return;
        openForAppointment(appointmentId);
    });

    modal.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) return;
        if (target.closest('[data-modal-close="' + modalId + '"]')) {
            closeModal();
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && modal.classList.contains("is-open")) {
            closeModal();
        }
    });
})();
