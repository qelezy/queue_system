(function () {
    const modalId = "doctor-patients-modal";
    const modal = document.getElementById(modalId);
    const dialog = modal?.querySelector(".app-modal__dialog");
    const titleEl = document.getElementById("doctor-patients-modal-title");
    const tbody = document.getElementById("doctor-patients-tbody");
    const tableWrap = document.querySelector(".doctor-load-table-scroll");
    const tbodyDoctors = tableWrap?.querySelector("tbody");

    if (!modal || !tbody || !tbodyDoctors) return;

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

    function renderPatients(patients) {
        if (!patients || patients.length === 0) {
            tbody.innerHTML = '<tr class="users-table__empty-row"><td colspan="4">Нет потенциальных пациентов</td></tr>';
            return;
        }
        tbody.innerHTML = patients
            .map(
                (p) =>
                    "<tr>" +
                    '<td>' + escapeHtml(p.ticketNumber) + "</td>" +
                    '<td>' + escapeHtml(p.categoryName) + "</td>" +
                    '<td>' + escapeHtml(p.priority ?? 0) + "</td>" +
                    '<td>' + escapeHtml(p.waitingMinutes ?? 0) + " мин</td>" +
                    "</tr>"
            )
            .join("");
    }

    function renderLoading() {
        tbody.innerHTML = '<tr class="users-table__empty-row"><td colspan="4">Загрузка…</td></tr>';
    }

    function renderError(message) {
        tbody.innerHTML =
            '<tr class="users-table__empty-row"><td colspan="4">' + escapeHtml(message || "Не удалось загрузить данные") + "</td></tr>";
    }

    async function openForDoctor(doctorId) {
        const id = Number(doctorId);
        if (!Number.isFinite(id) || id <= 0) return;

        titleEl.textContent = "Потенциальные пациенты";
        renderLoading();
        openModal();

        try {
            const response = await fetch(
                "/dashboard/doctors/" + encodeURIComponent(id) + "/potential-patients",
                { credentials: "same-origin", headers: { Accept: "application/json" } }
            );

            if (response.status === 404) {
                renderError("Врач не найден");
                return;
            }
            if (!response.ok) {
                renderError("Сервис очереди недоступен");
                return;
            }

            const data = await response.json();
            const doctorName = data.doctorName ? String(data.doctorName).trim() : "";
            titleEl.textContent = doctorName
                ? "Потенциальные пациенты — " + doctorName
                : "Потенциальные пациенты";
            renderPatients(data.patients);
        } catch {
            renderError("Не удалось загрузить данные");
        }
    }

    function getRowFromEvent(event) {
        const target = event.target;
        if (!(target instanceof Element)) return null;
        const row = target.closest("tr[data-doctor-load-row]");
        if (!(row instanceof HTMLTableRowElement)) return null;
        if (row.style.display === "none") return null;
        return row;
    }

    tbodyDoctors.addEventListener("click", (event) => {
        const row = getRowFromEvent(event);
        if (!row) return;
        const doctorId = row.dataset.doctorId;
        if (!doctorId) return;
        openForDoctor(doctorId);
    });

    tbodyDoctors.addEventListener("keydown", (event) => {
        if (event.key !== "Enter" && event.key !== " ") return;
        const row = getRowFromEvent(event);
        if (!row) return;
        event.preventDefault();
        const doctorId = row.dataset.doctorId;
        if (!doctorId) return;
        openForDoctor(doctorId);
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
