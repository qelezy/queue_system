(function () {
    const form = document.querySelector("form.profile-form .btn-primary")?.closest("form.profile-form");
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    const toastManager = window.AppToasts?.getManager("profile-toast-stack");
    const fieldSelectors = {
        firstName: "input[name='Profile.FirstName']",
        lastName: "input[name='Profile.LastName']",
        patronymic: "input[name='Profile.Patronymic']",
        email: "input[name='Profile.Email']",
        currentPassword: "input[name='Password.CurrentPassword']",
        newPassword: "input[name='Password.NewPassword']"
    };

    const inputs = {
        firstName: form.querySelector(fieldSelectors.firstName),
        lastName: form.querySelector(fieldSelectors.lastName),
        patronymic: form.querySelector(fieldSelectors.patronymic),
        email: form.querySelector(fieldSelectors.email),
        currentPassword: form.querySelector(fieldSelectors.currentPassword),
        newPassword: form.querySelector(fieldSelectors.newPassword)
    };

    function valueOf(input) {
        return (input instanceof HTMLInputElement ? input.value : "").trim();
    }

    function showToast(message, type) {
        if (!message || !toastManager) return;
        toastManager.show(message, type);
    }

    toastManager?.flushInitialSuccess();

    const initial = {
        firstName: valueOf(inputs.firstName),
        lastName: valueOf(inputs.lastName),
        patronymic: valueOf(inputs.patronymic),
        email: valueOf(inputs.email)
    };

    function hasProfileChanges() {
        return (
            valueOf(inputs.firstName) !== initial.firstName ||
            valueOf(inputs.lastName) !== initial.lastName ||
            valueOf(inputs.patronymic) !== initial.patronymic ||
            valueOf(inputs.email).toLowerCase() !== initial.email.toLowerCase()
        );
    }

    function validateForm() {
        const errors = [];
        const firstName = valueOf(inputs.firstName);
        const lastName = valueOf(inputs.lastName);
        const email = valueOf(inputs.email);
        const currentPassword = valueOf(inputs.currentPassword);
        const newPassword = valueOf(inputs.newPassword);

        if (!lastName) errors.push("Поле «Фамилия» обязательно для заполнения");
        if (!firstName) errors.push("Поле «Имя» обязательно для заполнения");
        if (!email) {
            errors.push("Поле «Email» обязательно для заполнения");
        } else {
            const emailValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
            if (!emailValid) errors.push("Введите корректный Email");
        }

        if ((currentPassword && !newPassword) || (!currentPassword && newPassword)) {
            errors.push("Для смены пароля заполните оба поля");
        }

        if (newPassword && newPassword.length < 6) {
            errors.push("Новый пароль должен быть не короче 6 символов");
        }

        return errors;
    }

    form.addEventListener("submit", (event) => {
        const errors = validateForm();
        const passwordRequested = valueOf(inputs.currentPassword) || valueOf(inputs.newPassword);
        const changed = hasProfileChanges() || Boolean(passwordRequested);

        if (!changed) {
            event.preventDefault();
            return;
        }

        if (errors.length > 0) {
            event.preventDefault();
            errors.forEach((error) => showToast(error, "error"));
        }
    });
})();
