(function () {
    const table = document.querySelector(".queue-table-scroll .users-table");
    if (!(table instanceof HTMLTableElement)) return;
    const tbody = table.tBodies[0];
    if (!tbody) return;

    let dataRows = Array.from(tbody.querySelectorAll("tr[data-queue-row]"));

    const noResultsClass = "queue-no-results-row";
    const colspan = table.tHead?.rows[0]?.cells.length ?? 6;

    let searchQuery = "";
    let category = "";
    let statusCode = "";
    let waitMinThreshold = null;

    function setNoResultsVisible(visible) {
        let row = tbody.querySelector("." + noResultsClass);
        if (visible) {
            if (!row) {
                row = document.createElement("tr");
                row.className = noResultsClass;
                row.innerHTML = `<td colspan="${colspan}">Записи не найдены</td>`;
                tbody.appendChild(row);
            }
        } else if (row) {
            row.remove();
        }
    }

    function apply() {
        let visibleCount = 0;
        for (const row of dataRows) {
            const text = row.textContent?.toLowerCase() ?? "";
            const rowCategoryId = row.dataset.categoryId ?? "";
            const rowStatusCode = row.dataset.statusCode ?? "";
            const rowWait = Number(row.dataset.wait ?? "0");

            const matchesSearch = !searchQuery || text.includes(searchQuery);
            const matchesCategory = !category || rowCategoryId === category;
            const matchesStatus = !statusCode || rowStatusCode === statusCode;
            const matchesWait = waitMinThreshold === null || rowWait > waitMinThreshold;

            const show = matchesSearch && matchesCategory && matchesStatus && matchesWait;
            row.style.display = show ? "" : "none";
            if (show) visibleCount++;
        }
        setNoResultsVisible(dataRows.length > 0 && visibleCount === 0);
    }

    function search(query) {
        searchQuery = (query ?? "").trim().toLowerCase();
        apply();
    }

    function filterCategory(value) {
        category = value ?? "";
        apply();
    }

    function filterStatus(value) {
        statusCode = value ?? "";
        apply();
    }

    function filterWait(raw) {
        const trimmed = (raw ?? "").trim();
        if (trimmed === "") {
            waitMinThreshold = null;
        } else {
            const parsed = Number(trimmed);
            waitMinThreshold =
                Number.isFinite(parsed) && parsed > 0 ? parsed : null;
        }
        apply();
    }

    function rebind() {
        dataRows = Array.from(tbody.querySelectorAll("tr[data-queue-row]"));
        apply();
    }

    window.QueueTable = { search, filterCategory, filterStatus, filterWait, rebind };
})();
