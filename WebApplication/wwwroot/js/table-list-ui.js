(function () {
    function getVisiblePageNumbers(totalPages, page) {
        if (totalPages <= 7) {
            return Array.from({ length: totalPages }, (_, idx) => idx + 1);
        }

        if (page <= 4) {
            return [1, 2, 3, 4, 5, "...", totalPages];
        }

        if (page >= totalPages - 3) {
            return [1, "...", totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages];
        }

        return [1, "...", page - 1, page, page + 1, "...", totalPages];
    }

    function create(config) {
        const tableBody = config.tableBody;
        const paginationContainer = config.paginationContainer;
        const sortableHeaders = config.sortableHeaders ?? [];
        const pageSize = config.pageSize ?? 10;
        const getDataRows = config.getDataRows;
        const matchRow = config.matchRow;
        const getSortValue = config.getSortValue;
        const numericSortKeys = new Set(config.numericSortKeys ?? []);
        const noResultsColspan = config.noResultsColspan ?? 4;
        const noResultsText = config.noResultsText ?? "Ничего не найдено";
        const paginationOnClick = config.paginationOnClick ?? "TableListUi.goToPage";
        const noResultsRowClass = config.noResultsRowClass ?? "users-no-results-row";

        let searchQuery = "";
        let sortField = config.initialSortField ?? "";
        let sortDirection = config.initialSortDirection ?? "asc";
        let currentPage = 1;

        function compareRows(a, b, field, direction) {
            const aValue = getSortValue(a, field);
            const bValue = getSortValue(b, field);
            let comparison;

            if (numericSortKeys.has(field)) {
                comparison = Number(aValue) - Number(bValue);
            } else {
                comparison = String(aValue).localeCompare(String(bValue), "ru", { sensitivity: "base", numeric: true });
            }

            return direction === "asc" ? comparison : -comparison;
        }

        function sortRows(field, direction) {
            const rows = getDataRows();
            rows.sort((a, b) => compareRows(a, b, field, direction));
            rows.forEach((row) => tableBody.appendChild(row));
        }

        function renderNoResultsRow(show) {
            if (!tableBody) return;
            const existing = tableBody.querySelector(`.${noResultsRowClass}`);
            if (show) {
                if (!existing) {
                    const row = document.createElement("tr");
                    row.className = noResultsRowClass;
                    row.innerHTML = `<td colspan="${noResultsColspan}">${noResultsText}</td>`;
                    tableBody.appendChild(row);
                }
            } else if (existing) {
                existing.remove();
            }
        }

        function renderPagination(totalPages) {
            if (!(paginationContainer instanceof HTMLElement)) return;
            paginationContainer.innerHTML = "";

            if (totalPages <= 1) {
                paginationContainer.classList.add("is-hidden");
                return;
            }

            paginationContainer.classList.remove("is-hidden");

            const pages = getVisiblePageNumbers(totalPages, currentPage);
            const prevDisabled = currentPage <= 1;
            const nextDisabled = currentPage >= totalPages;

            let html = `<button type="button" class="users-page-btn users-page-nav${prevDisabled ? " disabled" : ""}" onclick="${paginationOnClick}(${currentPage - 1})" aria-label="Предыдущая страница"${prevDisabled ? " disabled aria-disabled=\"true\"" : ""}><i class="bi bi-chevron-left" aria-hidden="true"></i></button>`;
            pages.forEach((page) => {
                if (page === "...") {
                    html += `<span class="users-page-ellipsis" aria-hidden="true">…</span>`;
                    return;
                }

                const isActive = page === currentPage ? " is-active" : "";
                html += `<button type="button" class="users-page-btn${isActive}" onclick="${paginationOnClick}(${page})" aria-label="Страница ${page}" aria-current="${page === currentPage ? "page" : "false"}">${page}</button>`;
            });
            html += `<button type="button" class="users-page-btn users-page-nav${nextDisabled ? " disabled" : ""}" onclick="${paginationOnClick}(${currentPage + 1})" aria-label="Следующая страница"${nextDisabled ? " disabled aria-disabled=\"true\"" : ""}><i class="bi bi-chevron-right" aria-hidden="true"></i></button>`;

            paginationContainer.innerHTML = html;
        }

        function updateSortIndicators() {
            sortableHeaders.forEach((header) => {
                const key = header.dataset.sortKey ?? "";
                const icon = header.querySelector(".users-sort-icon");
                header.removeAttribute("data-sort-direction");
                if (icon instanceof HTMLElement) {
                    icon.className = "bi users-sort-icon";
                }
                if (key === sortField) {
                    header.setAttribute("data-sort-direction", sortDirection);
                    if (icon instanceof HTMLElement) {
                        icon.className = sortDirection === "asc"
                            ? "bi bi-arrow-up users-sort-icon"
                            : "bi bi-arrow-down users-sort-icon";
                    }
                }
            });
        }

        function applyFilters() {
            const normalizedSearch = searchQuery.trim().toLowerCase();
            const rows = getDataRows();
            const matchedRows = rows.filter((row) => matchRow(row, normalizedSearch));

            const totalMatched = matchedRows.length;
            const totalPages = Math.ceil(totalMatched / pageSize);

            if (totalPages === 0) {
                currentPage = 1;
            } else if (currentPage > totalPages) {
                currentPage = totalPages;
            }

            const start = (currentPage - 1) * pageSize;
            const end = start + pageSize;
            const pageRows = new Set(matchedRows.slice(start, end));

            rows.forEach((row) => {
                row.style.display = pageRows.has(row) ? "" : "none";
            });

            renderNoResultsRow(totalMatched === 0);
            renderPagination(totalPages);
        }

        function sortBy(field) {
            if (!tableBody || !field) return;
            if (sortField === field) {
                sortDirection = sortDirection === "asc" ? "desc" : "asc";
            } else {
                sortField = field;
                sortDirection = "asc";
            }

            sortRows(sortField, sortDirection);
            updateSortIndicators();
            currentPage = 1;
            applyFilters();
        }

        function applyInitialSort() {
            if (!tableBody || !sortField) return;
            sortRows(sortField, sortDirection);
            updateSortIndicators();
            currentPage = 1;
            applyFilters();
        }

        function search(query) {
            searchQuery = query || "";
            currentPage = 1;
            applyFilters();
        }

        function goToPage(page) {
            if (!Number.isFinite(page)) return;
            const nextPage = Math.max(1, Math.trunc(page));
            if (nextPage === currentPage) return;
            currentPage = nextPage;
            applyFilters();
        }

        function reorderByCurrentSort() {
            if (!tableBody || !sortField) return;
            sortRows(sortField, sortDirection);
        }

        return {
            search,
            sortBy,
            applyInitialSort,
            goToPage,
            applyFilters,
            updateSortIndicators,
            reorderByCurrentSort,
            getSearchQuery: () => searchQuery,
            setCurrentPage: (page) => { currentPage = page; }
        };
    }

    window.TableListUi = { create };
})();
