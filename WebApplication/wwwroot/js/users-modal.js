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
    const tableBody = document.querySelector("#panel-list .users-table tbody");
    const paginationContainer = document.getElementById("users-pagination");
    const sortableHeaders = Array.from(document.querySelectorAll("#panel-list .users-table thead th[data-sort-key]"));
    let activeModalKey = "";
    let lastFocusedElement = null;
    let selectedRole = "";

    const pendingDelete = { timer: null, button: null };

    const tableList = window.TableListUi?.create({
        tableBody,
        paginationContainer,
        sortableHeaders,
        pageSize: 10,
        getDataRows,
        matchRow(row, normalizedSearch) {
            const text = row.textContent?.toLowerCase() ?? "";
            const roleCellText = row.children[2]?.textContent?.trim().toLowerCase() ?? "";
            const normalizedRole = selectedRole.trim().toLowerCase();
            const matchesSearch = !normalizedSearch || text.includes(normalizedSearch);
            const matchesRole = !normalizedRole || roleCellText === normalizedRole;
            return matchesSearch && matchesRole;
        },
        getSortValue(row, field) {
            if (field === "fullName") return row.children[0]?.textContent?.trim().toLowerCase() ?? "";
            if (field === "email") return row.children[1]?.textContent?.trim().toLowerCase() ?? "";
            if (field === "role") return row.children[2]?.textContent?.trim().toLowerCase() ?? "";
            return "";
        },
        noResultsColspan: 4,
        noResultsText: "Пользователи не найдены",
        paginationOnClick: "UsersUI.goToPage",
        initialSortField: "fullName",
        initialSortDirection: "asc"
    });

    function resetDeleteButton(btn) {
        if (!(btn instanceof HTMLElement)) return;
        btn.classList.remove("delete-btn--confirm");
        btn.innerHTML = '<i class="bi bi-trash" aria-hidden="true"></i>';
        btn.title = "Удалить";
        btn.setAttribute("aria-label", "Удалить пользователя");
    }

    function cancelPendingDelete() {
        if (pendingDelete.timer != null) {
            clearTimeout(pendingDelete.timer);
            pendingDelete.timer = null;
        }
        if (pendingDelete.button) {
            resetDeleteButton(pendingDelete.button);
            pendingDelete.button = null;
        }
    }

    function armDeleteConfirm(btn) {
        cancelPendingDelete();
        pendingDelete.button = btn;
        btn.classList.add("delete-btn--confirm");
        btn.innerHTML = '<i class="bi bi-check-lg" aria-hidden="true"></i>';
        btn.title = "Нажмите ещё раз для удаления";
        btn.setAttribute("aria-label", "Подтвердить удаление пользователя");
        pendingDelete.timer = window.setTimeout(() => {
            pendingDelete.timer = null;
            if (pendingDelete.button === btn) {
                resetDeleteButton(btn);
                pendingDelete.button = null;
            }
        }, 2000);
    }

    async function executeDelete(id, btn) {
        const trimmed = (id || "").trim();
        if (!trimmed || !(btn instanceof HTMLElement)) return;

        if (pendingDelete.timer != null) {
            clearTimeout(pendingDelete.timer);
            pendingDelete.timer = null;
        }
        pendingDelete.button = null;

        const toastManager = window.AppToasts?.getManager("global-toast-stack");
        btn.disabled = true;

        try {
            const response = await fetch(`/api/users/${encodeURIComponent(trimmed)}`, { method: "DELETE" });
            const result = await response.json().catch(() => ({}));
            if (response.ok && result.success) {
                const row = Array.from(document.querySelectorAll("#panel-list .users-table tbody tr[data-user-id]")).find(
                    (r) => r.getAttribute("data-user-id") === trimmed
                );
                if (row instanceof HTMLTableRowElement) {
                    row.remove();
                }
                tableList?.applyFilters();
                toastManager?.show(result.message || "Пользователь удалён.", "success");
                return;
            }

            const errors = Array.isArray(result.errors) ? result.errors.join("\n") : "";
            toastManager?.show((result.message || "Не удалось удалить пользователя.") + (errors ? "\n" + errors : ""), "error");
            resetDeleteButton(btn);
        } catch (err) {
            console.error(err);
            toastManager?.show("Сетевая ошибка при удалении пользователя.", "error");
            resetDeleteButton(btn);
        } finally {
            btn.disabled = false;
        }
    }

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

    function getFullName(lastName, firstName, patronymic) {
        const parts = [lastName, firstName, patronymic].filter(Boolean);
        return parts.join(" ").trim();
    }

    const roleTextByValue = {
        Admin: "Администратор",
        Manager: "Менеджер",
        Dispatcher: "Диспетчер"
    };

    function removeEmptyPlaceholderRows() {
        if (!tableBody) return;
        tableBody.querySelectorAll("tr:not([data-user-id])").forEach((row) => {
            if (!row.querySelector(".actions-cell")) {
                row.remove();
            }
        });
    }

    function createUserRow({ id, firstName, lastName, patronymic, email, role }) {
        const row = document.createElement("tr");
        row.setAttribute("data-user-id", id);
        row.dataset.firstName = firstName;
        row.dataset.lastName = lastName;
        row.dataset.patronymic = patronymic ?? "";
        row.dataset.email = email;
        row.dataset.role = role;

        const fullName = getFullName(lastName, firstName, patronymic);
        const roleName = roleTextByValue[role] || role;

        row.innerHTML = `
            <td></td>
            <td></td>
            <td></td>
            <td class="actions-cell">
                <button class="icon-btn edit-btn"
                        title="Редактировать"
                        onclick="UsersUI.openEdit('${id.replace(/'/g, "\\'")}')">
                    <i class="bi bi-pencil"></i>
                </button>
                <button type="button"
                        class="icon-btn delete-btn"
                        title="Удалить"
                        aria-label="Удалить пользователя">
                    <i class="bi bi-trash" aria-hidden="true"></i>
                </button>
            </td>`;
        row.children[0].textContent = fullName;
        row.children[1].textContent = email;
        row.children[2].textContent = roleName;
        return row;
    }

    function addRegisteredUser(user) {
        if (!tableBody || !user?.id || !tableList) return;

        removeEmptyPlaceholderRows();
        const row = createUserRow(user);
        tableBody.appendChild(row);
        tableList.reorderByCurrentSort();

        const normalizedSearch = tableList.getSearchQuery().trim().toLowerCase();
        const normalizedRole = selectedRole.trim().toLowerCase();
        const roleName = roleTextByValue[user.role] || user.role;
        const text = row.textContent?.toLowerCase() ?? "";
        const matchesSearch = !normalizedSearch || text.includes(normalizedSearch);
        const matchesRole = !normalizedRole || roleName.trim().toLowerCase() === normalizedRole;

        if (matchesSearch && matchesRole) {
            const matchedRows = getDataRows().filter((dataRow) => {
                const rowText = dataRow.textContent?.toLowerCase() ?? "";
                const roleCellText = dataRow.children[2]?.textContent?.trim().toLowerCase() ?? "";
                const rowMatchesSearch = !normalizedSearch || rowText.includes(normalizedSearch);
                const rowMatchesRole = !normalizedRole || roleCellText === normalizedRole;
                return rowMatchesSearch && rowMatchesRole;
            });
            const index = matchedRows.findIndex((dataRow) => dataRow.getAttribute("data-user-id") === user.id);
            if (index >= 0) {
                tableList.setCurrentPage(Math.floor(index / 10) + 1);
            }
        }

        tableList.applyFilters();
    }

    function getDataRows() {
        return Array.from(document.querySelectorAll("#panel-list .users-table tbody tr")).filter((row) =>
            row.querySelector(".actions-cell") !== null
        );
    }

    function openEdit(id) {
        if (!id) return;
        const row = document.querySelector(`#panel-list .users-table tbody tr[data-user-id="${id}"]`);
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
        if (roleInput instanceof HTMLSelectElement) roleInput.value = row.dataset.role ?? "Dispatcher";

        openModal("edit");
    }

    function handleDeleteButtonClick(event) {
        const btn = event.target instanceof Element ? event.target.closest("button.delete-btn") : null;
        if (!(btn instanceof HTMLButtonElement) || !tableBody || !tableBody.contains(btn)) return;
        if (btn.disabled) return;

        const row = btn.closest("tr[data-user-id]");
        if (!(row instanceof HTMLTableRowElement)) return;
        const id = row.getAttribute("data-user-id") || "";

        if (btn.classList.contains("delete-btn--confirm")) {
            event.preventDefault();
            void executeDelete(id, btn);
            return;
        }

        event.preventDefault();
        armDeleteConfirm(btn);
    }

    if (tableBody) {
        tableBody.addEventListener("click", handleDeleteButtonClick);
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
        if (event.key === "Escape") {
            cancelPendingDelete();
            if (activeModalKey) {
                closeModal(activeModalKey);
            }
        }
    });

    window.UsersUI = {
        openCreate,
        closeCreate,
        closeEdit: () => closeModal("edit"),
        search: (query) => tableList?.search(query),
        filterByRole(role) {
            selectedRole = role || "";
            tableList?.search(tableList.getSearchQuery());
        },
        goToPage: (page) => tableList?.goToPage(page),
        sortBy: (field) => tableList?.sortBy(field),
        openEdit,
        addRegisteredUser
    };

    setModalHiddenState("register", true);
    setModalHiddenState("edit", true);
    if (tableList) {
        tableList.applyInitialSort();
    }
})();
