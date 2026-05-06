# Промпт-спека: переиспользуемые UI-компоненты (этот репозиторий)

Используй этот файл как **контекст и инструкцию** для Cursor: новые блоки UI должны повторять уже принятые в проекте паттерны, а не общие туториалы ASP.NET Core.

## Контекст проекта (факты)

- **Стек**: ASP.NET Core MVC, Razor Views, стили `wwwroot/css/site.css` и `additions.css`, иконки Bootstrap Icons (CDN в `_Layout`).
- **Каркас**: [Views/Shared/_Layout.cshtml](Views/Shared/_Layout.cshtml) — `app-shell`, глобально `ToastStack`, `Sidebar` через `@await Component.InvokeAsync(...)`.
- **Импорты Razor**: [Views/_ViewImports.cshtml](Views/_ViewImports.cshtml) — `WebApplication`, `WebApplication.Models`, `WebApplication.ViewComponents`, tag helpers MVC.
- **Скрипты страницы**: секция `@section Scripts { <script src="~/js/....js" asp-append-version="true"></script> }`; для отдельных layout (например авторизация) — свой layout и свои скрипты.

## Как в проекте делают переиспользование (два основных механизма)

### 1. View Component

**Когда брать:** изолированный блок с чётким контрактом, возможны вложенные VC, нужен вызов из layout или нескольких страниц одной строкой.

**Файлы (обязательная связка):**

| Часть | Путь |
|--------|------|
| Класс | `ViewComponents/{Имя}ViewComponent.cs` |
| Razor | `Views/Shared/Components/{Имя}/Default.cshtml` |

**Имя в вызове:** без суффикса `ViewComponent`, например `"SearchBox"`, `"TableToolbar"`, `"UserRegistrationModal"`.

**Вызов из Razor:**

```cshtml
@await Component.InvokeAsync("SearchBox", Model.Search)
```

Для компонентов без аргументов (как боковая панель):

```cshtml
@await Component.InvokeAsync("Sidebar")
```

**Эталоны в репозитории (смотри код как шаблон):**

- Простой VC + VM: [ViewComponents/SearchBoxViewComponent.cs](ViewComponents/SearchBoxViewComponent.cs) + [Views/Shared/Components/SearchBox/Default.cshtml](Views/Shared/Components/SearchBox/Default.cshtml) + модель [Models/SearchBoxViewModel.cs](Models/SearchBoxViewModel.cs).
- Вложенный вызов VC из другого VC: [Views/Shared/Components/TableToolbar/Default.cshtml](Views/Shared/Components/TableToolbar/Default.cshtml) (`SearchBox` внутри).
- Модалка = VC + `PartialAsync` + `ViewData` для контекста partial: [ViewComponents/UserRegistrationModalViewComponent.cs](ViewComponents/UserRegistrationModalViewComponent.cs) + [Views/Shared/Components/UserRegistrationModal/Default.cshtml](Views/Shared/Components/UserRegistrationModal/Default.cshtml) (передаётся `ModalCloseId` в форму).
- Предпросмотр отчёта (модалка с таблицей + скачивание): [ViewComponents/ReportPreviewModalViewComponent.cs](ViewComponents/ReportPreviewModalViewComponent.cs) + [Views/Shared/Components/ReportPreviewModal/Default.cshtml](Views/Shared/Components/ReportPreviewModal/Default.cshtml) + [Models/ReportPreviewModalViewModel.cs](Models/ReportPreviewModalViewModel.cs); вызов из [Views/Reports/Index.cshtml](Views/Reports/Index.cshtml), скрипт [wwwroot/js/reports-index.js](wwwroot/js/reports-index.js) (`data-auto-open`, закрытие как у других `app-modal`).
- Глобальные уведомления: [ViewComponents/ToastStackViewComponent.cs](ViewComponents/ToastStackViewComponent.cs) + [Views/Shared/Components/ToastStack/Default.cshtml](Views/Shared/Components/ToastStack/Default.cshtml).
- **VC с DI и без параметров `Invoke`:** [ViewComponents/SidebarViewComponent.cs](ViewComponents/SidebarViewComponent.cs) — данные из `UserManager`, `RouteData`; модель собирается внутри; Razor: [Views/Shared/Components/Sidebar/Default.cshtml](Views/Shared/Components/Sidebar/Default.cshtml).

**Особый случай (есть в коде, вызовов в Views пока нет):** [ViewComponents/FormPanelViewComponent.cs](ViewComponents/FormPanelViewComponent.cs) принимает `(string title, string formView)`, заголовок кладёт в `ViewBag.Title`, в [Views/Shared/Components/FormPanel/Default.cshtml](Views/Shared/Components/FormPanel/Default.cshtml) `@model string` — это **путь к partial** для `Html.PartialAsync(Model)`. Если используешь этот паттерн — передавай надёжное имя/путь представления, как в существующем `Default.cshtml`.

**Методы:** в проекте есть и `Invoke`, и `async InvokeAsync` — выбирай по необходимости (как в `SidebarViewComponent`).

### 2. Partial view

**Когда брать:** фрагмент разметки/формы/таблицы без отдельного класса `ViewComponent`; модель передаётся с страницы или создаётся inline.

**Соглашения по именам:** префикс `_`, например `_UsersTable.cshtml`, `_StatCard.cshtml`.

**Подключение:**

```cshtml
<partial name="_UsersTable" model="Model.Users" />
```

или `@await Html.PartialAsync("~/Views/Users/_RegisterUserForm.cshtml", model, viewData)` из VC с полным путём, если partial не в стандартном discoverable location по короткому имени.

**Эталоны:**

- Таблица + вызов VC тулбара внутри: [Views/Users/_UsersTable.cshtml](Views/Users/_UsersTable.cshtml).
- Карточки с inline-моделью: [Views/Dashboard/Index.cshtml](Views/Dashboard/Index.cshtml) (`StatCardViewModel`, `ChartCardViewModel`).
- Общий кусок поля (редкий универсальный partial): [Views/Shared/_FormField.cshtml](Views/Shared/_FormField.cshtml) + [Models/FormFieldModel.cs](Models/FormFieldModel.cs).
- Типичная форма с `asp-for`: [Views/Users/_RegisterUserForm.cshtml](Views/Users/_RegisterUserForm.cshtml).

**Важно:** в формах уже смешаны стили — **`form-field`** (админка пользователей) и **`form-input`** (например вход в [Views/Account/Login.cshtml](Views/Account/Login.cshtml)). Новый код в существующих экранах **подстраивай под тот же файл/экран**, к которому добавляешь разметку.

## View-модели UI

- Живут в [Models/](Models/) с суффиксом `ViewModel` или осмысленным именем фрагмента (`FormFieldModel`).
- Поля вроде `OnInput`, `onclick`, `onchange` в VM — это **строки для атрибутов HTML**; допустимо только для **доверенных** значений (имена своих JS-функций, литералы из кода). Пользовательский ввод туда не подставлять (XSS).
- Допускается `return View(model ?? new SomeViewModel());` в VC для необязательной модели.

## Визуальный слой

- Классы: `register-panel`, `app-modal`, `table-toolbar`, `table-toolbar__left`, `btn`, `btn-primary`, `form-field`, `tabs-bar`, `tab-panel` и т.д. — смотри соседнюю разметку и [wwwroot/css](wwwroot/css).
- Иконки: `bi bi-*` (Bootstrap Icons).

## Доступность

- Модалки: `role="dialog"`, `aria-modal`, `aria-label`, `aria-hidden` — как в существующих `UserRegistrationModal` / `UserEditModal`.
- Toast: `aria-live`, `role` для типа сообщения — как в `ToastStack/Default.cshtml`.
- Кнопки без текста: `aria-label`.

## Инструкция для Cursor (копируй блоком в чат)

1. Определи: нужен **View Component** (отдельный контракт, вызов с нескольких мест, вложенность) или **partial** (фрагмент страницы).
2. Для VC: создай `{Имя}ViewComponent.cs`, `Views/Shared/Components/{Имя}/Default.cshtml`, при необходимости тип в `Models/`.
3. Скопируй структуру ближайшего **эталона** из списка выше (модалка / поиск / тулбар / sidebar).
4. Вызови с страницы через `Component.InvokeAsync("Имя", ...)`.
5. Стили — переиспользуй классы с того же экрана; новые — в духе BEM, как `table-toolbar__left`.
6. JS: вынеси в `wwwroot/js/...`, подключи через `@section Scripts`; для форм — `data-*` и существующие скрипты (`admin-users-tabs.js`, `users-modal.js` и т.д.) как образец.
7. Проверь a11y для интерактива.

## Мини-чеклист перед PR

- [ ] VC: папка `Components/{Имя}` совпадает с именем в `InvokeAsync`.
- [ ] Модель UI в `Models/`, при необходимости — запись в `_ViewImports` не дублирует уже существующие `@using`.
- [ ] Нет сырого пользовательского текста в `onclick` / `oninput` / `onchange`.
- [ ] Скрипты с `asp-append-version="true"` где уже так делают соседи.

---

Если поведение в коде и этот файл разошлись — **приоритет у кода**; обнови этот файл под новую договорённость.
