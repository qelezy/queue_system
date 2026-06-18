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

    function updateRowFallback(user) {
        const rows = Array.from(document.querySelectorAll("#panel-list .users-table tbody tr[data-user-id]"));
        const row = rows.find((item) => item.getAttribute("data-user-id") === user.id);
        if (!(row instanceof HTMLTableRowElement)) {
            return;
        }

        row.dataset.firstName = user.firstName;
        row.dataset.lastName = user.lastName;
        row.dataset.patronymic = user.patronymic;
        row.dataset.email = user.email;
        row.dataset.role = user.role;

        if (row.children[0]) row.children[0].textContent = getFullName(user.lastName, user.firstName, user.patronymic);
        if (row.children[1]) row.children[1].textContent = user.email;
        if (row.children[2]) row.children[2].textContent = roleTextByValue[user.role] || user.role;
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
                const updated = result.data || {};
                const savedFirstName = updated.firstName ?? updated.FirstName ?? firstName;
                const savedLastName = updated.lastName ?? updated.LastName ?? lastName;
                const savedPatronymic = updated.patronymic ?? updated.Patronymic ?? patronymic;
                const savedEmail = updated.email ?? updated.Email ?? email;
                const savedRole = updated.role ?? updated.Role ?? role;
                const updatedUser = {
                    id,
                    firstName: savedFirstName,
                    lastName: savedLastName,
                    patronymic: savedPatronymic,
                    email: savedEmail,
                    role: savedRole
                };
                const updatedInTable = window.UsersUI?.updateUserRow?.(updatedUser) === true;
                if (!updatedInTable) {
                    updateRowFallback(updatedUser);
                }

                toastManager?.show(result.message || "Данные пользователя обновлены.", "success");
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
