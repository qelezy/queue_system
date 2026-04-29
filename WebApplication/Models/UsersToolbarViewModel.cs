namespace WebApplication.Models
{
    public class RoleFilterOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class UsersToolbarViewModel
    {
        public SearchBoxViewModel Search { get; set; } = new()
        {
            Placeholder = "Поиск по ФИО, email, роли",
            OnInput = "UsersUI.search(this.value)"
        };
        public string RoleFilterAllLabel { get; set; } = "Все роли";
        public string RoleFilterOnChange { get; set; } = "UsersUI.filterByRole(this.value)";
        public IReadOnlyList<RoleFilterOptionViewModel> RoleFilterOptions { get; set; } = new List<RoleFilterOptionViewModel>
        {
            new() { Value = "Администратор", Label = "Администраторы" },
            new() { Value = "Менеджер", Label = "Менеджеры" },
            new() { Value = "Регистратор", Label = "Регистраторы" }
        };
        public string CreateButtonText { get; set; } = "Регистрация пользователя";
        public string CreateButtonOnClick { get; set; } = "UsersUI.openCreate()";
    }
}
