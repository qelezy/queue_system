(function () {
    const form = document.getElementById("registerForm");
    if (!form) {
        return;
    }

    const registerUserUrl = form.dataset.url || "";
    const toastManager = window.AppToasts?.getManager("global-toast-stack");
    if (!registerUserUrl) {
        return;
    }

    form.addEventListener("submit", async (e) => {
        e.preventDefault();

        const firstName = form.querySelector("#FirstName")?.value.trim() ?? "";
        const lastName = form.querySelector("#LastName")?.value.trim() ?? "";
        const patronymic = form.querySelector("#Patronymic")?.value.trim() ?? "";
        const email = form.querySelector("#Email")?.value.trim() ?? "";
        const role = form.querySelector("#Role")?.value ?? "";

        try {
            const response = await fetch(registerUserUrl, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ firstName, lastName, patronymic, email, role })
            });

            const result = await response.json().catch(() => ({}));
            if (response.ok && result.success) {
                window.UsersUI?.addRegisteredUser?.({
                    id: result.data?.userId ?? "",
                    firstName,
                    lastName,
                    patronymic,
                    email,
                    role
                });
                toastManager?.show("Пользователь успешно создан.", "success");
                form.reset();
                window.UsersUI?.closeCreate();
                return;
            }

            const errors = result.errors?.join("\n") || "";
            toastManager?.show((result.message || "Ошибка при создании пользователя") + (errors ? "\n" + errors : ""), "error");
        } catch (err) {
            console.error(err);
            toastManager?.show("Сетевая ошибка при создании пользователя.", "error");
        }
    });
})();
