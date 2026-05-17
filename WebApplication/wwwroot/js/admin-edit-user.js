(function () {
    const form = document.getElementById("editUserForm");
    if (!form) {
        return;
    }

    const updateUrlTemplate = form.dataset.urlTemplate || "";
    const toastManager = window.AppToasts?.getManager("global-toast-stack");
    if (!updateUrlTemplate) {
        return;
    }

    const roleTextByValue = {
        Admin: "Администратор",
        Manager: "Менеджер",
        Dispatcher: "Диспетчер"
    };

    function getFullName(lastName, firstName, patronymic) {
        const parts = [lastName, firstName, patronymic].filter(Boolean);
        return parts.join(" ").trim();
    }

    form.addEventListener("submit", async (e) => {
        e.preventDefault();

        const id = form.querySelector("#EditUserId")?.value.trim() ?? "";
        const firstName = form.querySelector("#EditFirstName")?.value.trim() ?? "";
        const lastName = form.querySelector("#EditLastName")?.value.trim() ?? "";
        const patronymic = form.querySelector("#EditPatronymic")?.value.trim() ?? "";
        const email = form.querySelector("#EditEmail")?.value.trim() ?? "";
        const role = form.querySelector("#EditRole")?.value ?? "";

        if (!id) {
            toastManager?.show("Не удалось определить пользователя для редактирования.", "error");
            return;
        }

        try {
            const response = await fetch(updateUrlTemplate.replace("__id__", encodeURIComponent(id)), {
                method: "PATCH",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ firstName, lastName, patronymic, email, role })
            });

            const result = await response.json().catch(() => ({}));
            if (response.ok && result.success) {
                const row = document.querySelector(`.users-table tbody tr[data-user-id="${id}"]`);
                if (row instanceof HTMLTableRowElement) {
                    row.dataset.firstName = firstName;
                    row.dataset.lastName = lastName;
                    row.dataset.patronymic = patronymic;
                    row.dataset.email = email;
                    row.dataset.role = role;

                    if (row.children[0]) row.children[0].textContent = getFullName(lastName, firstName, patronymic);
                    if (row.children[1]) row.children[1].textContent = email;
                    if (row.children[2]) row.children[2].textContent = roleTextByValue[role] || role;
                }

                toastManager?.show("Данные пользователя обновлены.", "success");
                window.UsersUI?.closeEdit?.();
                return;
            }

            const errors = result.errors?.join("\n") || "";
            toastManager?.show((result.message || "Ошибка при обновлении пользователя") + (errors ? "\n" + errors : ""), "error");
        } catch (err) {
            console.error(err);
            toastManager?.show("Сетевая ошибка при обновлении пользователя.", "error");
        }
    });
})();
