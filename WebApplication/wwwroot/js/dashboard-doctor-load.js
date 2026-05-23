(function () {
    const table = document.querySelector(".doctor-load-table");
    if (!(table instanceof HTMLTableElement)) return;
    const tbody = table.tBodies[0];
    if (!tbody) return;

    let dataRows = Array.from(tbody.querySelectorAll("tr[data-doctor-load-row]"));

    const noResultsClass = "doctor-load-no-results-row";
    const colspan = table.tHead?.rows[0]?.cells.length ?? 5;

    let searchQuery = "";

    function setNoResultsVisible(visible) {
        let row = tbody.querySelector("." + noResultsClass);
        if (visible) {
            if (!row) {
                row = document.createElement("tr");
                row.className = noResultsClass;
                row.innerHTML = `<td colspan="${colspan}">Врачи не найдены</td>`;
                tbody.appendChild(row);
            }
        } else if (row) {
            row.remove();
        }
    }

    function apply() {
        const normalized = searchQuery.trim().toLowerCase();
        let visibleCount = 0;
        for (const row of dataRows) {
            const text = row.textContent?.toLowerCase() ?? "";
            const match = !normalized || text.includes(normalized);
            row.style.display = match ? "" : "none";
            if (match) visibleCount++;
        }
        setNoResultsVisible(dataRows.length > 0 && visibleCount === 0);
    }

    function filter(query) {
        searchQuery = (query ?? "").trim().toLowerCase();
        apply();
    }

    function rebind() {
        dataRows = Array.from(tbody.querySelectorAll("tr[data-doctor-load-row]"));
        apply();
    }

    window.DoctorLoadTable = { filter, rebind };
})();
