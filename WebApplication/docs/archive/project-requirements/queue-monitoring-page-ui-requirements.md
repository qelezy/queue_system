# Спецификация UI страницы «Мониторинг»

Используй этот документ как **единый источник требований к интерфейсу** при доработке или переработке страницы мониторинга очереди (Razor + JS, см. также [reusable-ui-components-requirements.md](reusable-ui-components-requirements.md)). Страница доступна по маршруту `/Dashboard` и является точкой входа для роли «Диспетчер».

## Цель экрана

Диспетчер и менеджер видят **live-картину очереди** в реальном времени:

- сколько пациентов сейчас ожидает приёма;
- сколько находится на приёме;
- сколько уже принято за сегодня;
- средние и максимальные тайминги (ожидание, длительность приёма) за сегодня;
- детальный список текущей очереди с пациентами, врачами, кабинетами и статусами;
- загрузку каждого врача в реальном времени (длительность текущего приёма с нормой, размер личной очереди врача).

Страница ориентирована на оперативное управление потоком пациентов на месте, а не на глубокую ретроспективную аналитику.

## Принципы UX (обязательные)

1. **Один экран без вкладок.** Контент идёт сверху вниз тремя смысловыми блоками: верхняя панель карточек метрик → таблица текущей очереди → загрузка врачей.
2. **Live-обновление.** Данные на странице периодически обновляются без действий пользователя; транспорт и серверный контур — [dashboard-signalr-live-spec.md](dashboard-signalr-live-spec.md) (SignalR, snapshot `DashboardViewModel`). Краткий обзор — [dashboard-live-monitoring-minimum.md](dashboard-live-monitoring-minimum.md).
3. **Единый формат карточек метрик.** Все карточки верхней панели используют общий partial [Views/Shared/_StatCard.cshtml](Views/Shared/_StatCard.cshtml) и стили `stat-card*`; новые карточки добавляются той же раскладкой.
4. **Фильтры только для таблиц очереди и врачей.** Нет фильтров по периоду, кабинету, категории талона и т. п. (это домен «Отчёты»). В «Лист ожидания» — поиск, специальность, **статус** (`waiting` / `called`), порог ожидания (мин). В «Состояние врачей» — поиск, **специальность** (`data-specialty-id`, тот же справочник, что в листе), **статус** (`in-service` / `free`). Верхние метрики **не** пересчитываются по фильтрам.
5. **Статус как бейдж.** В таблице «Лист ожидания» — колонка «Статус» (`StatusLabel` / `StatusCode`: «Ожидает» / `waiting`, «Вызван» / `called`). В блоке «Состояние врачей» — бейджи «Принимает» / «Ожидает пациента».
6. **Минуты как единая единица времени.** Длительности и ожидания во всех блоках — в целых минутах с подписью «мин». Не смешивать `HH:mm` и минуты в одной таблице/карточке.
7. **Пустые состояния.** В каждом списке (таблица очереди, сетка врачей) предусмотреть осмысленное пустое состояние с короткой подписью.

## Структура страницы (целевой макет)

Сверху вниз:

1. Заголовок страницы: **«Мониторинг»** (через `ViewData["Title"]` в [Controllers/DashboardController.cs](Controllers/DashboardController.cs)).
2. **Верхняя панель метрик** — `stats-row`, 5 карточек.
3. **Таблица «Лист ожидания»** — `dashboard-panel` с таблицей.
4. **Блок «Загрузка врачей»** — `dashboard-panel` с сеткой карточек по врачу.

### 1. Верхняя панель метрик (5 карточек)

Контейнер `stats-row` (см. [Views/Dashboard/Index.cshtml](Views/Dashboard/Index.cshtml)). Слева направо:

| # | Заголовок | Значение | Sub-блок | Hint |
|---|-----------|----------|----------|------|
| 1 | Ожидают сейчас | `WaitingCount` | — | только статус «Ожидает» (`IsWaitingQueueStep`), без вызванных |
| 2 | На приёме сейчас | `InServiceCount` | — | пациенты, находящиеся на приёме |
| 3 | **Обслужено** | `AcceptedTodayCount` | — | пациенты с завершённым маршрутом (`time_complete` или все этапы с `time_end_servicing`) |
| 4 | Среднее время ожидания | `AvgWaitMinutes`, ед. «мин» | `Максимум` = `MaxWaitMinutes` мин | по завершённым ожиданиям за сегодня |
| 5 | Средняя длительность приёма | `AvgServiceMinutes`, ед. «мин» | `Максимум` = `MaxServiceMinutes` мин | по завершённым приёмам за сегодня |

**Правила оформления карточек:**

- Карточки 1–3 — без `SubLabel`/`SubValue` (используется модификатор `stat-card--no-sub`, как в текущем `_StatCard`).
- Карточки 4–5 — с `SubLabel = "Максимум"` и значением максимума в минутах.
- Разметка одной карточки не меняется — переиспользуется существующий partial.
- Поле `AcceptedTodayCount` в [Models/DashboardViewModel.cs](Models/DashboardViewModel.cs) заполняется в [Services/QueueDashboardService.cs](Services/QueueDashboardService.cs).

Сетка должна корректно умещать 5 карточек: при необходимости в `additions.css` поправить `stats-row` (например, `grid-template-columns: repeat(auto-fit, minmax(180px, 1fr))`), не ломая остальные страницы, использующие тот же класс.

### 2. Таблица «Лист ожидания»

Заменяет текущий [Views/Dashboard/_DashboardQueueTable.cshtml](Views/Dashboard/_DashboardQueueTable.cshtml).

**Контейнер:** `dashboard-panel` с заголовком **«Лист ожидания»**. В таблицу попадают пациенты с текущим этапом «Ожидает» или «Вызван» (`QueueDashboardStatusMapper.IsWaitingListStep`). На приёме — в метрике и блоке врачей, не в листе.

**Колонки (слева направо):**

| Колонка | Содержимое | Примечание |
|---------|-----------|------------|
| № талона | `Appointment.number` | NOT NULL в БД; при пустой строке в UI — `—` |
| Врач | Двухстрочная ячейка: ФИО врача (основная строка), специальность (вторая строка, серым) | `queue-row__doctor-name`, `queue-row__doctor-specialty` |
| Кабинет | номер кабинета | Если неизвестен — `—` |
| Статус | бейдж `StatusLabel` | CSS-модификатор `queue-status-badge--{StatusCode}` (`waiting`, `called`) |
| Время ожидания | `{N} мин` | «Ожидает»: от прибытия талона (МСК); «Вызван»: от `time_call` до текущего момента (МСК) |

**Сортировка** — приоритет талона → приоритет категории → время ожидания.

**Пустое состояние:** «Лист ожидания пуст»; при активных фильтрах без совпадений — «Записи не найдены».

**Toolbar (клиентская фильтрация, [wwwroot/js/dashboard-queue.js](wwwroot/js/dashboard-queue.js)):**

| Элемент | Источник опций | Сопоставление строк |
|---------|----------------|---------------------|
| Специальность | все строки `Specialty` (`QueueFilters.Specialties`, SSR) | `data-specialty-id` = `id_specialty` этапа |
| Статус | фиксированный список: Ожидает / Вызван | `data-status-code` = `waiting` / `called` |
| Поиск | — | текст строки |
| Время ожидания &gt; N мин | — | `data-wait` |

Справочники фильтров задаются при первой отрисовке страницы и **не** входят в SignalR snapshot; при live-обновлении tbody пересобирается, выбранные значения select сохраняются.

**Клик по строке** — модальное окно «Этапы маршрута» ([`_DashboardCompletedStagesModal.cshtml`](Views/Dashboard/_DashboardCompletedStagesModal.cshtml), [wwwroot/js/dashboard-queue-stages.js](wwwroot/js/dashboard-queue-stages.js)): `GET /dashboard/appointments/{id}/route-stages`, все этапы маршрута за сегодня (без «неяв»), по `id_list_item`. Колонки: специальность, кабинет, **статус** (бейдж), время вызова / начала / окончания (`HH:mm:ss`, `—` если нет). Высота диалога по контенту; внутренний скролл таблицы при переполнении `max-height` ([`additions.css`](wwwroot/css/additions.css), `.queue-route-stages-panel`).

**ViewModel:** [Models/ViewModels/Dashboard/DashboardViewModel.cs](Models/ViewModels/Dashboard/DashboardViewModel.cs):

- `DashboardQueueRowViewModel`: `Specialty`, `IdSpecialty`, `IdStatusItem`, `StatusLabel`, `StatusCode`;
- `DashboardQueueFilterViewModel` / `DashboardFilterOption`: справочники для select;
- partial `_DashboardQueueTable` — модель `DashboardQueueTableViewModel` (строки + фильтры).

Сами поля приоритета (`TicketPriority`, `CategoryPriority`) остаются для сортировки, но в Razor не выводятся.

### 3. Блок «Загрузка врачей» (нижний)

**Полностью заменяет** существующий блок «Загруженность за сегодня» (тот, в котором сейчас переключатели «Кабинеты/Врачи» и «Завершённые приёмы / Доля занятого времени» с графиком `chartLoadToday` в [Views/Dashboard/Index.cshtml](Views/Dashboard/Index.cshtml)). Старый блок и связанная с ним логика на странице мониторинга больше не нужны.

**Контейнер:** `dashboard-panel` с заголовком **«Состояние врачей»** (таблица в [_DashboardDoctorLoad.cshtml](Views/Dashboard/_DashboardDoctorLoad.cshtml)).

**Toolbar ([wwwroot/js/dashboard-doctor-load.js](wwwroot/js/dashboard-doctor-load.js)):**

| Элемент | Сопоставление строк |
|---------|---------------------|
| Поиск | текст строки |
| Специальность | `Model.Filters.Specialties` (как в листе) | `data-specialty-id` |
| Статус | фиксированный список: Принимает / Ожидает пациента | `data-doctor-status` = `in-service` / `free` |

**Раскладка (устаревший макет карточек):** адаптивная сетка `doctor-load-grid`:

```css
.doctor-load-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 12px;
}
```

(Точные значения уточняются по визуалу проекта; новые классы — в `additions.css`, BEM-стиль: `doctor-load-card`, `doctor-load-card__header`, `doctor-load-card__bar`, `doctor-load-card__bar--over`, `doctor-load-card__queue` и т. п.)

**Карточка одного врача (`doctor-load-card`)** содержит:

1. **Шапка**:
   - ФИО врача (основная строка);
   - специальность (вторая строка, серым);
   - бейдж состояния: `queue-status-badge--in-service` (**«Принимает»** — этап с кодом `in-service`, `QueueDashboardStatusMapper.IsInServiceStep`) или `queue-status-badge--free` (**«Ожидает пациента»** — есть очередь ожидания/вызова, активного приёма нет). Вызванный, но ещё не начатый приём → «Ожидает пациента».
2. **Длительность текущего приёма**:
   - подпись формата `{currentMin} мин из {normMin} мин нормы`;
   - прогресс-бар, заполненный пропорционально `currentMin / normMin`;
   - если `currentMin <= normMin` — бар нейтрального акцентного цвета (`doctor-load-card__bar`);
   - если `currentMin > normMin` — бар окрашен в акцент превышения (`doctor-load-card__bar--over`), процент заполнения ограничен 100 %, рядом допускается короткая подпись «превышение нормы»;
   - норма берётся из `Specialty.time_servicing` для специальности текущего этапа врача;
   - если врач **свободен** (нет активного приёма) — прогресс-бар не отображается, вместо него выводится прочерк `—` или строка «Сейчас не на приёме».
3. **В очереди**:
   - крупное число — **число талонов** (не этапов маршрута): открытые талоны за сегодня, у которых **текущий** этап (`List_item` без `time_end_servicing`, минимальный `id_list_item`) назначен на этого врача и в статусе «Ожидает»/«Вызван» — та же логика, что для строк листа ожидания по колонке «Врач» ([`QueueDashboardDoctorQueueCount`](../../../Services/Dashboard/QueueDashboardDoctorQueueCount.cs));
   - подпись «в очереди» под числом;
   - 0 — допустимое значение, отображается как «0».

**Сортировка карточек** (в порядке убывания приоритета):

1. Сначала врачи со статусом «Принимает».
2. Потом «Ожидает пациента».
3. Внутри каждой группы — по ФИО (по возрастанию).

**Пустое состояние:** если нет ни одного врача с активной очередью или текущим приёмом за сегодня — короткая подпись «Нет активных врачей».

**ViewModel:** контракт нового блока (детали реализации — задача `QueueDashboardService`):

- список объектов вида `DoctorLoadCardViewModel` с полями:
  - `FullName` (строка);
  - `Specialty` (строка);
  - `IdSpecialty` (int — для фильтра; текущий этап при приёме или очередь при «Ожидает пациента»);
  - `IsInService` (bool);
  - `CurrentServiceMinutes` (int? — null если свободен);
  - `NormServiceMinutes` (int? — null если у специальности норма не задана);
  - `QueueLength` (int).
- сама структура хранится в [Models/DashboardViewModel.cs](Models/DashboardViewModel.cs) (новый класс) и подключается полем в `DashboardViewModel` (например, `DoctorLoad`).

## Чего избегать

- Возвращать на эту страницу графики кабинетов / часов и переключатели «Кабинеты/Врачи» — место для них в менеджерской аналитике / отчётах, а не в live-мониторинге.
- Смешивать форматы времени в одной таблице/карточке (`HH:mm` и минуты одновременно).
- Считать «На приёме сейчас» и бейдж «Принимает» по разным правилам: оба — открытые талоны, **текущий** этап с кодом `in-service` (`IsInServiceStep`, есть `time_start_servicing`, нет `time_end_servicing`).
- Скрывать колонку «Статус» под иконку без текстовой подписи — статус должен читаться без наведения курсора.
- Заменять live-данные снапшотом, который не обновляется без перезагрузки страницы.

## Связь с текущей реализацией (ориентиры для разработки)

Точки входа в коде (актуализировать при рефакторинге):

- Страница: [Views/Dashboard/Index.cshtml](Views/Dashboard/Index.cshtml)
- Подвиды:
  - таблица очереди: [Views/Dashboard/_DashboardQueueTable.cshtml](Views/Dashboard/_DashboardQueueTable.cshtml);
  - загрузка врачей: [Views/Dashboard/_DashboardDoctorLoad.cshtml](Views/Dashboard/_DashboardDoctorLoad.cshtml).
- Карточки метрик: [Views/Shared/_StatCard.cshtml](Views/Shared/_StatCard.cshtml) + [Models/StatCardViewModel.cs](Models/StatCardViewModel.cs)
- ViewModel страницы: [Models/DashboardViewModel.cs](Models/DashboardViewModel.cs)
- Контроллер: [Controllers/DashboardController.cs](Controllers/DashboardController.cs)
- Сервис live-данных: [Services/Dashboard/QueueDashboardService.cs](Services/Dashboard/QueueDashboardService.cs); маппинг статусов: [Services/Dashboard/QueueDashboardStatusMapper.cs](Services/Dashboard/QueueDashboardStatusMapper.cs); только live БД (без mock)
- Скрипты страницы: [wwwroot/js/dashboard-queue.js](../wwwroot/js/dashboard-queue.js), [wwwroot/js/dashboard-doctor-load.js](../wwwroot/js/dashboard-doctor-load.js); live-контур (при реализации): `wwwroot/js/dashboard-live.js` — см. [dashboard-signalr-live-spec.md](dashboard-signalr-live-spec.md)
- Стили: [wwwroot/css/site.css](wwwroot/css/site.css), [wwwroot/css/additions.css](wwwroot/css/additions.css)

Переработка визуала и потока должна **сохранять или улучшать** описанные выше сценарии; при изменении путей — обновить раздел «Связь с реализацией» в этом файле.

---

Если поведение в коде и этот файл разошлись — **приоритет у кода**; обнови этот файл под новую договорённость.
