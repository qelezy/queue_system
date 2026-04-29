document.addEventListener("DOMContentLoaded", function () {

    const tabs = document.querySelectorAll("[data-tab]");
    const panels = {
        list: document.getElementById("panel-list"),
        access: document.getElementById("panel-access")
    };

    tabs.forEach(btn => {
        btn.addEventListener("click", function () {

            const tab = btn.dataset.tab;

            tabs.forEach(t => t.classList.remove("is-active"));
            btn.classList.add("is-active");

            Object.values(panels).forEach(p => p?.classList.remove("is-visible"));
            panels[tab]?.classList.add("is-visible");
        });
    });

});