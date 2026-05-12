(function () {
    const table = document.querySelector(".doctor-load-table");
    if (!(table instanceof HTMLTableElement)) return;
    const tbody = table.tBodies[0];
    if (!tbody) return;

    const dataRows = Array.from(tbody.querySelectorAll("tr[data-doctor-load-row]"));
    if (dataRows.length === 0) return;

    const noResultsClass = "doctor-load-no-results-row";
    const colspan = table.tHead?.rows[0]?.cells.length ?? 5;

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

    function filter(query) {
        const normalized = (query ?? "").trim().toLowerCase();
        let visibleCount = 0;
        for (const row of dataRows) {
            const text = row.textContent?.toLowerCase() ?? "";
            const match = !normalized || text.includes(normalized);
            row.style.display = match ? "" : "none";
            if (match) visibleCount++;
        }
        setNoResultsVisible(visibleCount === 0);
    }

    window.DoctorLoadTable = { filter };
})();
