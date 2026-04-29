(function () {
    const modalIds = {
        register: "register-user-modal",
        edit: "edit-user-modal"
    };
    const modals = {
        register: document.getElementById(modalIds.register),
        edit: document.getElementById(modalIds.edit)
    };
    const dialogs = {
        register: modals.register?.querySelector(".app-modal__dialog"),
        edit: modals.edit?.querySelector(".app-modal__dialog")
    };
    const tableSelector = ".users-table tbody tr";
    const tableBody = document.querySelector(".users-table tbody");
    const paginationContainer = document.getElementById("users-pagination");
    const sortableHeaders = Array.from(document.querySelectorAll(".users-table thead th[data-sort-key]"));
    const pageSize = 10;
    let activeModalKey = "";
    let lastFocusedElement = null;
    let searchQuery = "";
    let selectedRole = "";
    let sortField = "";
    let sortDirection = "asc";
    let currentPage = 1;

    function getFirstFocusableInModal(modalKey) {
        return modals[modalKey]?.querySelector("button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])");
    }

    function setModalHiddenState(modalKey, isHidden) {
        const modal = modals[modalKey];
        if (!modal) return;
        modal.setAttribute("aria-hidden", isHidden ? "true" : "false");
        if ("inert" in modal) {
            modal.inert = isHidden;
        }
    }

    function openModal(modalKey) {
        const modal = modals[modalKey];
        const dialog = dialogs[modalKey];
        if (!modal) return;
        activeModalKey = modalKey;
        lastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        modal.classList.add("is-open");
        setModalHiddenState(modalKey, false);
        const firstFocusable = getFirstFocusableInModal(modalKey);
        if (firstFocusable instanceof HTMLElement) {
            firstFocusable.focus();
        } else if (dialog instanceof HTMLElement) {
            dialog.focus();
        }
    }

    function closeModal(modalKey) {
        const modal = modals[modalKey];
        if (!modal) return;
        const activeElement = document.activeElement;
        if (activeElement instanceof HTMLElement && modal.contains(activeElement)) {
            activeElement.blur();
        }
        modal.classList.remove("is-open");
        setModalHiddenState(modalKey, true);
        activeModalKey = "";
        if (lastFocusedElement && document.contains(lastFocusedElement)) {
            lastFocusedElement.focus();
        }
    }

    function openCreate() {
        openModal("register");
    }

    function closeCreate() {
        closeModal("register");
    }

    function getDataRows() {
        return Array.from(document.querySelectorAll(tableSelector)).filter((row) =>
            row.querySelector(".actions-cell") !== null
        );
    }

    function renderNoResultsRow(show) {
        if (!tableBody) return;
        const existing = tableBody.querySelector(".users-no-results-row");
        if (show) {
            if (!existing) {
                const row = document.createElement("tr");
                row.className = "users-no-results-row";
                row.innerHTML = "<td colspan=\"4\">Пользователи не найдены</td>";
                tableBody.appendChild(row);
            }
        } else if (existing) {
            existing.remove();
        }
    }

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

        let html = `<button type="button" class="users-page-btn users-page-nav${prevDisabled ? " disabled" : ""}" onclick="UsersUI.goToPage(${currentPage - 1})" aria-label="Предыдущая страница"${prevDisabled ? " disabled aria-disabled=\"true\"" : ""}><i class="bi bi-chevron-left" aria-hidden="true"></i></button>`;
        pages.forEach((page) => {
            if (page === "...") {
                html += `<span class="users-page-ellipsis" aria-hidden="true">…</span>`;
                return;
            }

            const isActive = page === currentPage ? " is-active" : "";
            html += `<button type="button" class="users-page-btn${isActive}" onclick="UsersUI.goToPage(${page})" aria-label="Страница ${page}" aria-current="${page === currentPage ? "page" : "false"}">${page}</button>`;
        });
        html += `<button type="button" class="users-page-btn users-page-nav${nextDisabled ? " disabled" : ""}" onclick="UsersUI.goToPage(${currentPage + 1})" aria-label="Следующая страница"${nextDisabled ? " disabled aria-disabled=\"true\"" : ""}><i class="bi bi-chevron-right" aria-hidden="true"></i></button>`;

        paginationContainer.innerHTML = html;
    }

    function applyFilters() {
        const normalizedSearch = searchQuery.trim().toLowerCase();
        const normalizedRole = selectedRole.trim().toLowerCase();
        const rows = getDataRows();
        const matchedRows = rows.filter((row) => {
            const text = row.textContent?.toLowerCase() ?? "";
            const roleCellText = row.children[2]?.textContent?.trim().toLowerCase() ?? "";
            const matchesSearch = !normalizedSearch || text.includes(normalizedSearch);
            const matchesRole = !normalizedRole || roleCellText === normalizedRole;
            return matchesSearch && matchesRole;
        });

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

    function getSortValue(row, field) {
        if (field === "fullName") return row.children[0]?.textContent?.trim().toLowerCase() ?? "";
        if (field === "email") return row.children[1]?.textContent?.trim().toLowerCase() ?? "";
        if (field === "role") return row.children[2]?.textContent?.trim().toLowerCase() ?? "";
        return "";
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
                        ? "bi bi-arrow-down users-sort-icon"
                        : "bi bi-arrow-up users-sort-icon";
                }
            }
        });
    }

    function sortBy(field) {
        if (!tableBody || !field) return;
        if (sortField === field) {
            sortDirection = sortDirection === "asc" ? "desc" : "asc";
        } else {
            sortField = field;
            sortDirection = "asc";
        }

        const rows = getDataRows();
        rows.sort((a, b) => {
            const aValue = getSortValue(a, field);
            const bValue = getSortValue(b, field);
            const comparison = aValue.localeCompare(bValue, "ru", { sensitivity: "base" });
            return sortDirection === "asc" ? comparison : -comparison;
        });

        rows.forEach((row) => tableBody.appendChild(row));
        updateSortIndicators();
        currentPage = 1;
        applyFilters();
    }

    function search(query) {
        searchQuery = query || "";
        currentPage = 1;
        applyFilters();
    }

    function filterByRole(role) {
        selectedRole = role || "";
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

    function openEdit(id) {
        if (!id) return;
        const row = document.querySelector(`.users-table tbody tr[data-user-id="${id}"]`);
        if (!(row instanceof HTMLTableRowElement)) return;

        const idInput = document.getElementById("EditUserId");
        const firstNameInput = document.getElementById("EditFirstName");
        const lastNameInput = document.getElementById("EditLastName");
        const patronymicInput = document.getElementById("EditPatronymic");
        const emailInput = document.getElementById("EditEmail");
        const roleInput = document.getElementById("EditRole");

        if (idInput instanceof HTMLInputElement) idInput.value = id;
        if (firstNameInput instanceof HTMLInputElement) firstNameInput.value = row.dataset.firstName ?? "";
        if (lastNameInput instanceof HTMLInputElement) lastNameInput.value = row.dataset.lastName ?? "";
        if (patronymicInput instanceof HTMLInputElement) patronymicInput.value = row.dataset.patronymic ?? "";
        if (emailInput instanceof HTMLInputElement) emailInput.value = row.dataset.email ?? "";
        if (roleInput instanceof HTMLSelectElement) roleInput.value = row.dataset.role ?? "Registrator";

        openModal("edit");
    }

    function remove(id) {
        console.log("Delete user", id);
    }

    document.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof HTMLElement)) return;
        const modalCloseId = target.dataset.modalClose;
        if (!modalCloseId) return;
        if (modalCloseId === modalIds.register) {
            closeModal("register");
            return;
        }
        if (modalCloseId === modalIds.edit) {
            closeModal("edit");
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && activeModalKey) {
            closeModal(activeModalKey);
        }
    });

    window.UsersUI = {
        openCreate,
        closeCreate,
        closeEdit: () => closeModal("edit"),
        search,
        filterByRole,
        goToPage,
        sortBy,
        openEdit,
        delete: remove
    };

    setModalHiddenState("register", true);
    setModalHiddenState("edit", true);
    sortBy("fullName");
    applyFilters();
})();
