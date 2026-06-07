(function () {
    const modalId = "service-category-modal";
    const modal = document.getElementById(modalId);
    const tbody = document.getElementById("service-categories-tbody");
    const form = document.getElementById("serviceCategoryForm");
    const showArchivedCheckbox = document.getElementById("service-categories-show-archived");
    const sharedHint = document.getElementById("service-category-shared-hint");
    const modalTitle = document.getElementById("service-category-modal-title");
    const paginationContainer = document.getElementById("service-categories-pagination");
    const createModeBlock = document.getElementById("service-category-create-mode");
    const routeFieldsBlock = document.getElementById("service-category-route-fields");
    const linkField = document.getElementById("service-category-link-field");
    const existingSettingSelect = document.getElementById("ServiceCategoryExistingSetting");
    const settingPreview = document.getElementById("service-category-setting-preview");
    const letterInput = document.getElementById("ServiceCategoryLetter");
    const letterWarning = document.getElementById("service-category-letter-warning");
    const submitBtn = document.getElementById("service-category-submit");
    const settingNameField = document.getElementById("service-category-setting-name-field");
    const settingNameInput = document.getElementById("ServiceCategorySettingName");
    const categoryNameInput = document.getElementById("ServiceCategoryName");
    const sortableHeaders = Array.from(document.querySelectorAll("#service-categories-table thead th[data-sort-key]"));
    const toastManager = window.AppToasts?.getManager("global-toast-stack");
    const TABLE_COLSPAN = 7;

    if (!tbody || !form || !modal) {
        return;
    }

    let specialties = [];
    let settings = [];
    let categories = [];
    let loaded = false;
    let pendingSharedConfirm = false;
    let settingNameTouched = false;
    let lastFocusedElement = null;

    const pendingArchive = { timer: null, button: null };
    const pendingRestore = { timer: null, button: null };

    const tableList = window.TableListUi?.create({
        tableBody: tbody,
        paginationContainer,
        sortableHeaders,
        pageSize: 10,
        getDataRows,
        matchRow(row, normalizedSearch) {
            const text = row.textContent?.toLowerCase() ?? "";
            const matchesSearch = !normalizedSearch || text.includes(normalizedSearch);
            const showArchived = showArchivedCheckbox instanceof HTMLInputElement && showArchivedCheckbox.checked;
            if (!showArchived && row.hasAttribute("data-archived")) {
                return false;
            }
            return matchesSearch;
        },
        getSortValue(row, field) {
            if (field === "name") return row.dataset.sortName ?? "";
            if (field === "priority") return row.dataset.sortPriority ?? "";
            if (field === "startSpecialty") return row.dataset.sortStartSpecialty ?? "";
            if (field === "endSpecialty") return row.dataset.sortEndSpecialty ?? "";
            if (field === "timePause") return row.dataset.sortTimePause ?? "";
            if (field === "criticalNumPause") return row.dataset.sortCriticalNumPause ?? "";
            return "";
        },
        noResultsColspan: TABLE_COLSPAN,
        noResultsText: "Категории не найдены",
        paginationOnClick: "ServiceCategoriesUI.goToPage",
        numericSortKeys: ["priority", "timePause", "criticalNumPause"],
        initialSortField: "priority",
        initialSortDirection: "desc"
    });

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    function formatStartSpecialty(item) {
        return item.startSpecialtyName?.trim() || "—";
    }

    function formatEndSpecialty(item) {
        return item.endSpecialtyName?.trim() || "—";
    }

    function formatRouteTypeFromSetting(setting) {
        return setting?.endSpecialtyName?.trim() ? "Полный" : "Один приём";
    }

    function formatEndSpecialtyFromSetting(setting) {
        return setting?.endSpecialtyName?.trim() || "—";
    }

    function formatStartSpecialtyFromSetting(setting) {
        return setting?.startSpecialtyName?.trim() || "—";
    }

    function getSharedCategoryNames(idSetting, excludeCategoryId) {
        return categories
            .filter(c => c.idSetting === idSetting && !c.isArchived && c.idCategory !== excludeCategoryId)
            .map(c => c.name.trim())
            .filter(Boolean);
    }

    function getEditingCategoryId() {
        const idRaw = document.getElementById("ServiceCategoryId")?.value.trim() ?? "";
        const id = Number(idRaw);
        return id > 0 ? id : null;
    }

    function findLetterConflict(letter, excludeCategoryId) {
        const normalized = letter.trim().toLowerCase();
        if (!normalized) {
            return null;
        }

        return categories.find(c =>
            !c.isArchived &&
            c.letter.trim().toLowerCase() === normalized &&
            c.idCategory !== excludeCategoryId) ?? null;
    }

    function clearLetterWarning() {
        if (letterWarning instanceof HTMLElement) {
            letterWarning.hidden = true;
            letterWarning.textContent = "";
        }
        if (letterInput instanceof HTMLInputElement) {
            letterInput.removeAttribute("aria-invalid");
        }
    }

    function updateLetterWarning() {
        if (!(letterInput instanceof HTMLInputElement)) {
            return null;
        }

        const letter = letterInput.value.trim();
        if (letter.length !== 1) {
            clearLetterWarning();
            return null;
        }

        const conflict = findLetterConflict(letter, getEditingCategoryId());
        if (!conflict) {
            clearLetterWarning();
            return null;
        }

        if (letterWarning instanceof HTMLElement) {
            letterWarning.hidden = false;
            letterWarning.textContent = `Буква «${letter}» уже используется категорией «${conflict.name.trim()}».`;
        }
        letterInput.setAttribute("aria-invalid", "true");
        return conflict;
    }

    function hasLetterConflict() {
        return updateLetterWarning() !== null;
    }

    function getCreateMode() {
        const checked = form.querySelector('input[name="ServiceCategoryCreateMode"]:checked');
        return checked instanceof HTMLInputElement ? checked.value : "link";
    }

    function isEditMode() {
        const idRaw = document.getElementById("ServiceCategoryId")?.value.trim() ?? "";
        return !!idRaw;
    }

    function setModalHidden(isHidden) {
        modal.setAttribute("aria-hidden", isHidden ? "true" : "false");
        if ("inert" in modal) {
            modal.inert = isHidden;
        }
    }

    function openModal() {
        lastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        modal.classList.add("is-open");
        setModalHidden(false);
        const first = modal.querySelector("input, select, button, textarea");
        if (first instanceof HTMLElement) {
            first.focus();
        }
    }

    function closeModal() {
        modal.classList.remove("is-open");
        setModalHidden(true);
        pendingSharedConfirm = false;
        if (sharedHint) {
            sharedHint.hidden = true;
            sharedHint.textContent = "";
        }
        if (settingPreview instanceof HTMLElement) {
            settingPreview.hidden = true;
            settingPreview.innerHTML = "";
        }
        clearLetterWarning();
        if (lastFocusedElement instanceof HTMLElement) {
            lastFocusedElement.focus();
        }
    }

    function populateSpecialtySelect() {
        const startSelect = document.getElementById("ServiceCategoryStartSpecialty");
        const endSelect = document.getElementById("ServiceCategoryEndSpecialty");
        const optionsHtml = ['<option value="">—</option>']
            .concat(specialties.map(s => `<option value="${s.id}">${escapeHtml(s.label)}</option>`))
            .join("");

        if (startSelect instanceof HTMLSelectElement) {
            startSelect.innerHTML = optionsHtml;
        }
        if (endSelect instanceof HTMLSelectElement) {
            endSelect.innerHTML = optionsHtml;
        }
    }

    function populateSettingsSelect() {
        if (!(existingSettingSelect instanceof HTMLSelectElement)) {
            return;
        }

        const optionsHtml = ['<option value="">—</option>']
            .concat(settings.map(s => `<option value="${s.id}">${escapeHtml(s.label)}</option>`))
            .join("");

        existingSettingSelect.innerHTML = optionsHtml;
    }

    function renderSettingPreview(setting) {
        if (!(settingPreview instanceof HTMLElement)) {
            return;
        }

        if (!setting) {
            settingPreview.hidden = true;
            settingPreview.innerHTML = "";
            return;
        }

        settingPreview.hidden = false;
        settingPreview.innerHTML = `
            <dl class="service-category-setting-preview__list">
                <div class="service-category-setting-preview__row">
                    <dt>Начальная специальность</dt>
                    <dd>${escapeHtml(formatStartSpecialtyFromSetting(setting))}</dd>
                </div>
                <div class="service-category-setting-preview__row">
                    <dt>Конечная специальность</dt>
                    <dd>${escapeHtml(formatEndSpecialtyFromSetting(setting))}</dd>
                </div>
                <div class="service-category-setting-preview__row">
                    <dt>Время паузы, мин</dt>
                    <dd>${setting.timePause}</dd>
                </div>
                <div class="service-category-setting-preview__row">
                    <dt>Критическое количество пауз</dt>
                    <dd>${setting.criticalNumPause}</dd>
                </div>
            </dl>`;
    }

    function updateSettingPreview() {
        if (!(existingSettingSelect instanceof HTMLSelectElement)) {
            return;
        }

        const id = Number(existingSettingSelect.value);
        if (!id) {
            renderSettingPreview(null);
            return;
        }

        const setting = settings.find(s => s.id === id);
        renderSettingPreview(setting ?? null);
    }

    function updateSubmitLabel() {
        if (!(submitBtn instanceof HTMLButtonElement)) {
            return;
        }

        submitBtn.textContent = isEditMode() ? "Сохранить" : "Добавить";
    }

    function updateSharedHint(item) {
        if (!sharedHint) {
            return;
        }

        if (item?.sharedCategoryCount > 1) {
            const names = getSharedCategoryNames(item.idSetting, item.idCategory);
            const namesText = names.length ? names.join(", ") : "связанные категории";
            sharedHint.hidden = false;
            sharedHint.textContent = `Изменения настроек затронут следующие категории обслуживания: ${namesText}`;
        } else {
            sharedHint.hidden = true;
            sharedHint.textContent = "";
        }
    }

    function syncSettingNameFromCategory() {
        if (!(settingNameInput instanceof HTMLInputElement) || settingNameTouched) {
            return;
        }

        if (categoryNameInput instanceof HTMLInputElement) {
            settingNameInput.value = categoryNameInput.value;
        }
    }

    function updateFormMode() {
        const editing = isEditMode();
        const createMode = getCreateMode();

        if (createModeBlock instanceof HTMLElement) {
            createModeBlock.hidden = editing;
        }

        if (routeFieldsBlock instanceof HTMLElement) {
            if (editing) {
                routeFieldsBlock.hidden = false;
            } else {
                routeFieldsBlock.hidden = createMode === "link";
            }
        }

        if (linkField instanceof HTMLElement) {
            linkField.hidden = editing || createMode !== "link";
        }

        if (createMode !== "link" && existingSettingSelect instanceof HTMLSelectElement) {
            existingSettingSelect.value = "";
        }

        if (existingSettingSelect instanceof HTMLSelectElement) {
            existingSettingSelect.required = !editing && createMode === "link";
        }

        if (settingNameField instanceof HTMLElement) {
            settingNameField.hidden = !editing && createMode !== "new";
        }

        if (!editing && createMode === "new") {
            syncSettingNameFromCategory();
        }

        const timePauseInput = document.getElementById("ServiceCategoryTimePause");
        const criticalInput = document.getElementById("ServiceCategoryCriticalNumPause");
        const startSelect = document.getElementById("ServiceCategoryStartSpecialty");
        const endSelect = document.getElementById("ServiceCategoryEndSpecialty");
        const routeRequired = editing || createMode === "new";

        if (timePauseInput instanceof HTMLInputElement) {
            timePauseInput.required = routeRequired;
        }
        if (criticalInput instanceof HTMLInputElement) {
            criticalInput.required = routeRequired;
        }
        if (startSelect instanceof HTMLSelectElement) {
            startSelect.disabled = !routeRequired;
        }
        if (endSelect instanceof HTMLSelectElement) {
            endSelect.disabled = !routeRequired;
        }

        if (!editing && createMode === "link") {
            updateSettingPreview();
        } else if (settingPreview instanceof HTMLElement) {
            settingPreview.hidden = true;
            settingPreview.innerHTML = "";
        }
    }

    function getDataRows() {
        return Array.from(tbody.querySelectorAll("tr[data-category-id]"));
    }

    function renderTable() {
        tbody.querySelectorAll(".service-categories-placeholder").forEach(row => row.remove());

        if (!categories.length) {
            tbody.innerHTML = "";
            tableList?.applyFilters();
            return;
        }

        tbody.innerHTML = categories.map(item => {
            const startSpecialty = formatStartSpecialty(item);
            const endSpecialty = formatEndSpecialty(item);
            const archivedBadge = item.isArchived
                ? ' <span class="service-category-archived-badge">Архив</span>'
                : "";
            const actions = item.isArchived
                ? `<button type="button" class="icon-btn restore-btn" data-id="${item.idCategory}" title="Восстановить" aria-label="Восстановить">
                        <i class="bi bi-arrow-counterclockwise" aria-hidden="true"></i>
                   </button>`
                : `<button type="button" class="icon-btn edit-btn" data-action="edit" data-id="${item.idCategory}" title="Редактировать" aria-label="Редактировать">
                        <i class="bi bi-pencil" aria-hidden="true"></i>
                   </button>
                   <button type="button" class="icon-btn archive-btn" data-id="${item.idCategory}" title="Архивировать" aria-label="Архивировать">
                        <i class="bi bi-archive" aria-hidden="true"></i>
                   </button>`;

            return `<tr data-category-id="${item.idCategory}"
                        class="${item.isArchived ? "service-category-row--archived" : ""}"
                        data-sort-name="${escapeHtml(item.name.trim().toLowerCase())}"
                        data-sort-priority="${String(item.priority).padStart(6, "0")}"
                        data-sort-start-specialty="${escapeHtml(startSpecialty.toLowerCase())}"
                        data-sort-end-specialty="${escapeHtml(endSpecialty.toLowerCase())}"
                        data-sort-time-pause="${String(item.timePause).padStart(6, "0")}"
                        data-sort-critical-num-pause="${String(item.criticalNumPause).padStart(6, "0")}"
                        ${item.isArchived ? 'data-archived="1"' : ""}>
                <td>${escapeHtml(item.name)}${archivedBadge}</td>
                <td>${item.priority}</td>
                <td>${escapeHtml(startSpecialty)}</td>
                <td>${escapeHtml(endSpecialty)}</td>
                <td>${item.timePause}</td>
                <td>${item.criticalNumPause}</td>
                <td class="actions-cell">${actions}</td>
            </tr>`;
        }).join("");

        tableList?.reorderByCurrentSort();
        tableList?.applyFilters();
    }

    async function loadSpecialties() {
        const response = await fetch("/api/service-categories/specialties");
        const result = await response.json().catch(() => ({}));
        if (!response.ok || !result.success) {
            throw new Error(result.message || "Не удалось загрузить специальности.");
        }
        specialties = Array.isArray(result.data) ? result.data : [];
        populateSpecialtySelect();
    }

    async function loadSettings() {
        const response = await fetch("/api/service-categories/settings");
        const result = await response.json().catch(() => ({}));
        if (!response.ok || !result.success) {
            throw new Error(result.message || "Не удалось загрузить настройки обслуживания.");
        }
        settings = Array.isArray(result.data) ? result.data : [];
        populateSettingsSelect();
    }

    async function loadCategories() {
        const response = await fetch("/api/service-categories?includeArchived=true");
        const result = await response.json().catch(() => ({}));
        if (!response.ok || !result.success) {
            throw new Error(result.message || "Не удалось загрузить категории.");
        }
        categories = Array.isArray(result.data) ? result.data : [];
        renderTable();
    }

    async function ensureLoaded() {
        if (loaded) {
            await Promise.all([loadSettings(), loadCategories()]);
            return;
        }

        tbody.innerHTML = `<tr class="service-categories-placeholder"><td colspan="${TABLE_COLSPAN}">Загрузка…</td></tr>`;
        try {
            await Promise.all([loadSpecialties(), loadSettings(), loadCategories()]);
            loaded = true;
            tableList?.applyInitialSort();
        } catch (err) {
            console.error(err);
            tbody.innerHTML = `<tr class="service-categories-placeholder"><td colspan="${TABLE_COLSPAN}">Ошибка загрузки</td></tr>`;
            toastManager?.show(err.message || "Ошибка загрузки категорий.", "error");
        }
    }

    function fillForm(item) {
        document.getElementById("ServiceCategoryId").value = item?.idCategory ? String(item.idCategory) : "";
        document.getElementById("ServiceCategoryName").value = item?.name ?? "";
        document.getElementById("ServiceCategoryLetter").value = item?.letter ?? "";
        document.getElementById("ServiceCategoryPriority").value = item?.priority != null ? String(item.priority) : "0";
        document.getElementById("ServiceCategoryTimePause").value = item?.timePause != null ? String(item.timePause) : "5";
        document.getElementById("ServiceCategoryCriticalNumPause").value = item?.criticalNumPause != null ? String(item.criticalNumPause) : "3";

        const startSelect = document.getElementById("ServiceCategoryStartSpecialty");
        if (startSelect instanceof HTMLSelectElement) {
            startSelect.value = item?.startSpecialtyId != null ? String(item.startSpecialtyId) : "";
        }

        const endSelect = document.getElementById("ServiceCategoryEndSpecialty");
        if (endSelect instanceof HTMLSelectElement) {
            endSelect.value = item?.endSpecialtyId != null ? String(item.endSpecialtyId) : "";
        }

        if (existingSettingSelect instanceof HTMLSelectElement) {
            existingSettingSelect.value = item?.idSetting != null ? String(item.idSetting) : "";
        }

        if (settingNameInput instanceof HTMLInputElement) {
            settingNameInput.value = item?.settingName ?? "";
        }
        settingNameTouched = !!item;

        const linkRadio = form.querySelector('input[name="ServiceCategoryCreateMode"][value="link"]');
        if (linkRadio instanceof HTMLInputElement) {
            linkRadio.checked = true;
        }

        updateSharedHint(item ?? null);
        updateLetterWarning();
        updateFormMode();
        updateSubmitLabel();
    }

    function openCreate() {
        pendingSharedConfirm = false;
        modalTitle.textContent = "Новая категория";
        fillForm(null);
        openModal();
    }

    function openEdit(id) {
        const item = categories.find(c => c.idCategory === id);
        if (!item) {
            toastManager?.show("Категория не найдена.", "error");
            return;
        }
        pendingSharedConfirm = false;
        modalTitle.textContent = "Редактирование категории";
        fillForm(item);
        openModal();
    }

    function readFormPayload(confirmSharedSettingUpdate) {
        const startSelect = document.getElementById("ServiceCategoryStartSpecialty");
        const endSelect = document.getElementById("ServiceCategoryEndSpecialty");
        const startValue = startSelect instanceof HTMLSelectElement ? startSelect.value : "";
        const endValue = endSelect instanceof HTMLSelectElement ? endSelect.value : "";
        const editing = isEditMode();
        const createMode = getCreateMode();

        const payload = {
            name: document.getElementById("ServiceCategoryName")?.value.trim() ?? "",
            letter: document.getElementById("ServiceCategoryLetter")?.value.trim() ?? "",
            priority: Number(document.getElementById("ServiceCategoryPriority")?.value ?? 0),
            confirmSharedSettingUpdate: !!confirmSharedSettingUpdate
        };

        if (!editing && createMode === "link") {
            const settingValue = existingSettingSelect instanceof HTMLSelectElement ? existingSettingSelect.value : "";
            payload.idSetting = settingValue ? Number(settingValue) : null;
            return payload;
        }

        payload.startSpecialtyId = startValue ? Number(startValue) : null;
        payload.endSpecialtyId = endValue ? Number(endValue) : null;
        payload.timePause = Number(document.getElementById("ServiceCategoryTimePause")?.value ?? 0);
        payload.criticalNumPause = Number(document.getElementById("ServiceCategoryCriticalNumPause")?.value ?? 0);

        if (settingNameInput instanceof HTMLInputElement && (editing || createMode === "new")) {
            payload.settingName = settingNameInput.value.trim();
        }

        return payload;
    }

    async function saveCategory(confirmSharedSettingUpdate) {
        if (hasLetterConflict()) {
            toastManager?.show("Буква талона уже используется другой активной категорией.", "error");
            return;
        }

        const idRaw = document.getElementById("ServiceCategoryId")?.value.trim() ?? "";
        const payload = readFormPayload(confirmSharedSettingUpdate);
        const isEdit = !!idRaw;
        const url = isEdit ? `/api/service-categories/${encodeURIComponent(idRaw)}` : "/api/service-categories";
        const method = isEdit ? "PUT" : "POST";

        const response = await fetch(url, {
            method,
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const result = await response.json().catch(() => ({}));

        if (response.status === 409 && result.code === "sharedSettingConfirmationRequired") {
            const names = Array.isArray(result.sharedCategoryNames) ? result.sharedCategoryNames.join(", ") : "";
            const message = names
                ? `Изменения настроек затронут следующие категории обслуживания: ${names}`
                : (result.message || "Подтвердите изменение настройки обслуживания.");
            if (window.confirm(message)) {
                pendingSharedConfirm = true;
                return saveCategory(true);
            }
            return;
        }

        if (!response.ok || !result.success) {
            const errors = Array.isArray(result.errors) ? result.errors.join("\n") : "";
            toastManager?.show((result.message || "Ошибка сохранения.") + (errors ? `\n${errors}` : ""), "error");
            return;
        }

        toastManager?.show(isEdit ? "Изменения сохранены." : "Категория успешно создана.", "success");
        closeModal();
        await Promise.all([loadSettings(), loadCategories()]);
    }

    function resetArchiveButton(btn) {
        if (!(btn instanceof HTMLElement)) {
            return;
        }
        btn.classList.remove("archive-btn--confirm");
        btn.innerHTML = '<i class="bi bi-archive" aria-hidden="true"></i>';
        btn.title = "Архивировать";
        btn.setAttribute("aria-label", "Архивировать");
    }

    function resetRestoreButton(btn) {
        if (!(btn instanceof HTMLElement)) {
            return;
        }
        btn.classList.remove("restore-btn--confirm");
        btn.innerHTML = '<i class="bi bi-arrow-counterclockwise" aria-hidden="true"></i>';
        btn.title = "Восстановить";
        btn.setAttribute("aria-label", "Восстановить");
    }

    function cancelPendingArchive() {
        if (pendingArchive.timer != null) {
            clearTimeout(pendingArchive.timer);
            pendingArchive.timer = null;
        }
        if (pendingArchive.button) {
            resetArchiveButton(pendingArchive.button);
            pendingArchive.button = null;
        }
    }

    function cancelPendingRestore() {
        if (pendingRestore.timer != null) {
            clearTimeout(pendingRestore.timer);
            pendingRestore.timer = null;
        }
        if (pendingRestore.button) {
            resetRestoreButton(pendingRestore.button);
            pendingRestore.button = null;
        }
    }

    function cancelPendingRowActions() {
        cancelPendingArchive();
        cancelPendingRestore();
    }

    function armArchiveConfirm(btn) {
        cancelPendingRowActions();
        pendingArchive.button = btn;
        btn.classList.add("archive-btn--confirm");
        btn.innerHTML = '<i class="bi bi-check-lg" aria-hidden="true"></i>';
        btn.title = "Нажмите ещё раз для архивирования";
        btn.setAttribute("aria-label", "Подтвердить архивирование");
        pendingArchive.timer = window.setTimeout(() => {
            pendingArchive.timer = null;
            if (pendingArchive.button === btn) {
                resetArchiveButton(btn);
                pendingArchive.button = null;
            }
        }, 2000);
    }

    function armRestoreConfirm(btn) {
        cancelPendingRowActions();
        pendingRestore.button = btn;
        btn.classList.add("restore-btn--confirm");
        btn.innerHTML = '<i class="bi bi-check-lg" aria-hidden="true"></i>';
        btn.title = "Нажмите ещё раз для восстановления";
        btn.setAttribute("aria-label", "Подтвердить восстановление");
        pendingRestore.timer = window.setTimeout(() => {
            pendingRestore.timer = null;
            if (pendingRestore.button === btn) {
                resetRestoreButton(btn);
                pendingRestore.button = null;
            }
        }, 2000);
    }

    async function executeArchive(id, btn) {
        if (!id || !(btn instanceof HTMLButtonElement)) {
            return;
        }

        if (pendingArchive.timer != null) {
            clearTimeout(pendingArchive.timer);
            pendingArchive.timer = null;
        }
        pendingArchive.button = null;

        btn.disabled = true;

        try {
            const response = await fetch(`/api/service-categories/${encodeURIComponent(id)}/archive`, { method: "POST" });
            const result = await response.json().catch(() => ({}));
            if (!response.ok || !result.success) {
                const errors = Array.isArray(result.errors) ? result.errors.join("\n") : "";
                toastManager?.show((result.message || "Не удалось архивировать.") + (errors ? `\n${errors}` : ""), "error");
                resetArchiveButton(btn);
                btn.disabled = false;
                return;
            }

            toastManager?.show(result.message || "Категория архивирована.", "success");
            await Promise.all([loadSettings(), loadCategories()]);
        } catch (err) {
            console.error(err);
            toastManager?.show("Сетевая ошибка при архивации.", "error");
            resetArchiveButton(btn);
            btn.disabled = false;
        }
    }

    async function executeRestore(id, btn) {
        if (!id || !(btn instanceof HTMLButtonElement)) {
            return;
        }

        if (pendingRestore.timer != null) {
            clearTimeout(pendingRestore.timer);
            pendingRestore.timer = null;
        }
        pendingRestore.button = null;

        btn.disabled = true;

        try {
            const response = await fetch(`/api/service-categories/${encodeURIComponent(id)}/restore`, { method: "POST" });
            const result = await response.json().catch(() => ({}));
            if (!response.ok || !result.success) {
                const errors = Array.isArray(result.errors) ? result.errors.join("\n") : "";
                toastManager?.show((result.message || "Не удалось восстановить.") + (errors ? `\n${errors}` : ""), "error");
                resetRestoreButton(btn);
                btn.disabled = false;
                return;
            }

            toastManager?.show(result.message || "Категория восстановлена.", "success");
            await Promise.all([loadSettings(), loadCategories()]);
        } catch (err) {
            console.error(err);
            toastManager?.show("Сетевая ошибка при восстановлении.", "error");
            resetRestoreButton(btn);
            btn.disabled = false;
        }
    }

    function toggleArchived(checked) {
        if (showArchivedCheckbox instanceof HTMLInputElement) {
            showArchivedCheckbox.checked = !!checked;
        }
        tableList?.search(tableList.getSearchQuery());
    }

    form.addEventListener("submit", async (event) => {
        event.preventDefault();
        try {
            await saveCategory(pendingSharedConfirm);
        } catch (err) {
            console.error(err);
            toastManager?.show("Сетевая ошибка при сохранении.", "error");
        }
    });

    form.addEventListener("change", (event) => {
        const target = event.target;
        if (target instanceof HTMLInputElement && target.name === "ServiceCategoryCreateMode") {
            updateFormMode();
            return;
        }
        if (target instanceof HTMLSelectElement && target.id === "ServiceCategoryExistingSetting") {
            updateSettingPreview();
        }
    });

    if (letterInput instanceof HTMLInputElement) {
        letterInput.addEventListener("input", updateLetterWarning);
    }

    if (categoryNameInput instanceof HTMLInputElement) {
        categoryNameInput.addEventListener("input", () => {
            if (!isEditMode() && getCreateMode() === "new") {
                syncSettingNameFromCategory();
            }
        });
    }

    if (settingNameInput instanceof HTMLInputElement) {
        settingNameInput.addEventListener("input", () => {
            settingNameTouched = true;
        });
    }

    function handleArchiveButtonClick(event) {
        const btn = event.target instanceof Element ? event.target.closest("button.archive-btn") : null;
        if (!(btn instanceof HTMLButtonElement) || !tbody.contains(btn) || btn.disabled) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();

        const row = btn.closest("tr[data-category-id]");
        const id = Number(row?.getAttribute("data-category-id"));
        if (!id) {
            return;
        }

        if (btn.classList.contains("archive-btn--confirm")) {
            void executeArchive(id, btn);
            return;
        }

        armArchiveConfirm(btn);
    }

    function handleRestoreButtonClick(event) {
        const btn = event.target instanceof Element ? event.target.closest("button.restore-btn") : null;
        if (!(btn instanceof HTMLButtonElement) || !tbody.contains(btn) || btn.disabled) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();

        const row = btn.closest("tr[data-category-id]");
        const id = Number(row?.getAttribute("data-category-id"));
        if (!id) {
            return;
        }

        if (btn.classList.contains("restore-btn--confirm")) {
            void executeRestore(id, btn);
            return;
        }

        armRestoreConfirm(btn);
    }

    function handleEditButtonClick(event) {
        const btn = event.target instanceof Element ? event.target.closest("button.edit-btn[data-action='edit']") : null;
        if (!(btn instanceof HTMLElement) || !tbody.contains(btn)) {
            return;
        }

        const id = Number(btn.dataset.id);
        if (!id) {
            return;
        }

        openEdit(id);
    }

    tbody.addEventListener("click", handleArchiveButtonClick);
    tbody.addEventListener("click", handleRestoreButtonClick);
    tbody.addEventListener("click", handleEditButtonClick);

    modal.addEventListener("click", (event) => {
        const target = event.target instanceof Element ? event.target.closest(`[data-modal-close="${modalId}"]`) : null;
        if (target) {
            closeModal();
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            cancelPendingRowActions();
            if (modal.classList.contains("is-open")) {
                closeModal();
            }
        }
    });

    window.ServiceCategoriesUI = {
        ensureLoaded,
        openCreate,
        search: (query) => tableList?.search(query),
        sortBy: (field) => tableList?.sortBy(field),
        goToPage: (page) => tableList?.goToPage(page),
        toggleArchived
    };
})();
