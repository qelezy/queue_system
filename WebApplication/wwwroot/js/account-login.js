(function () {
    const form = document.getElementById("loginForm");
    if (!form) {
        return;
    }

    const submitButton = form.querySelector('button[type="submit"]');
    const passwordInput = form.querySelector("#loginPassword");
    const passwordToggle = form.querySelector("#loginPasswordToggle");
    const toastManager = window.AppToasts?.getManager("global-toast-stack");
    const loginUrl = form.dataset.loginUrl || "/api/auth/login";
    const postLoginRedirectPath = form.dataset.postLoginRedirectPath || "/dashboard/index";
    let isSubmitting = false;

    function setFormSubmittingState(inProgress) {
        isSubmitting = inProgress;
        if (!submitButton) {
            return;
        }

        submitButton.disabled = inProgress;
        submitButton.classList.toggle("is-loading", inProgress);
        if (inProgress) {
            submitButton.setAttribute("aria-busy", "true");
        } else {
            submitButton.removeAttribute("aria-busy");
        }
    }

    function showError(message) {
        toastManager?.show(message, "error");
    }

    function initPasswordVisibilityToggle() {
        if (!(passwordInput instanceof HTMLInputElement) || !(passwordToggle instanceof HTMLButtonElement)) {
            return;
        }

        const icon = passwordToggle.querySelector(".bi");
        if (!(icon instanceof HTMLElement)) {
            return;
        }

        passwordToggle.addEventListener("click", function () {
            const isVisible = passwordInput.type === "text";
            passwordInput.type = isVisible ? "password" : "text";
            const nowVisible = passwordInput.type === "text";
            icon.classList.toggle("bi-eye", nowVisible);
            icon.classList.toggle("bi-eye-slash", !nowVisible);
            passwordToggle.setAttribute("aria-pressed", nowVisible ? "true" : "false");
            passwordToggle.setAttribute("aria-label", nowVisible ? "Скрыть пароль" : "Показать пароль");
        });
    }

    initPasswordVisibilityToggle();

    function extractErrorMessage(payload, fallbackMessage) {
        if (!payload) {
            return fallbackMessage;
        }

        if (Array.isArray(payload.errors) && payload.errors.length > 0) {
            return payload.errors.join(" ");
        }

        if (typeof payload.message === "string" && payload.message.trim()) {
            return payload.message.trim();
        }

        return fallbackMessage;
    }

    form.addEventListener("submit", async function (event) {
        event.preventDefault();

        const email = form.querySelector('input[name="email"]')?.value?.trim() ?? "";
        const password = form.querySelector('input[name="password"]')?.value ?? "";
        const rememberMe = Boolean(form.querySelector('input[name="rememberMe"]')?.checked);

        if (!email || !password) {
            showError("Введите email и пароль.");
            return;
        }

        if (isSubmitting) {
            return;
        }

        setFormSubmittingState(true);

        try {
            const loginResponse = await fetch(loginUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({ email, password, rememberMe })
            });

            const loginPayload = await loginResponse.json().catch(function () { return null; });
            if (!loginResponse.ok || !loginPayload?.success) {
                throw new Error(extractErrorMessage(loginPayload, "Не удалось выполнить вход. Проверьте email и пароль."));
            }

            window.location.assign(postLoginRedirectPath);
        } catch (error) {
            showError(error?.message || "Ошибка авторизации. Попробуйте еще раз.");
            setFormSubmittingState(false);
        }
    });
})();
