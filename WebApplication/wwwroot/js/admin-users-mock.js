(function () {

    const mockUsers = [
        {
            id: "1",
            fullName: "Иванова Е.С.",
            email: "e.ivanova@medcenter.local",
            roleName: "Диспетчер"
        },
        {
            id: "2",
            fullName: "Смирнов П.А.",
            email: "p.smirnov@medcenter.local",
            roleName: "Менеджер"
        },
        {
            id: "3",
            fullName: "Козлов Д.C.",
            email: "d.kozlov@medcenter.local",
            roleName: "Диспетчер"
        }
    ];

    function renderTable(users) {
        const tbody = document.querySelector(".users-table tbody");
        if (!tbody) return;

        tbody.innerHTML = "";

        if (!users || users.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="3">Пользователи не найдены</td>
                </tr>`;
            return;
        }

        users.forEach(u => {
            const row = document.createElement("tr");

            row.innerHTML = `
                <td>${u.fullName}</td>
                <td>${u.email}</td>
                <td>${u.roleName}</td>
            `;

            tbody.appendChild(row);
        });
    }

    // initial render
    document.addEventListener("DOMContentLoaded", function () {
        renderTable(mockUsers);
    });

    // expose for future API replace
    window.UsersTable = {
        setData: renderTable
    };

})();