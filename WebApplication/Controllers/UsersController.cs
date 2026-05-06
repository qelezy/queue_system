using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;

namespace WebApplication.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;

        public UsersController(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Управление пользователями";

            var dbUsers = _userManager.Users.ToList();
            var users = new List<UserRowViewModel>(dbUsers.Count);

            foreach (var user in dbUsers)
            {
                var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Registrator";
                var firstName = user.FirstName ?? string.Empty;
                var lastName = user.LastName ?? string.Empty;
                var patronymic = user.Patronymic ?? string.Empty;
                var fullName = string.Join(" ", new[] { lastName, firstName, patronymic }.Where(x => !string.IsNullOrWhiteSpace(x)));

                users.Add(new UserRowViewModel
                {
                    Id = user.Id,
                    LastName = lastName,
                    FirstName = firstName,
                    Patronymic = patronymic,
                    FullName = fullName,
                    Email = user.Email ?? string.Empty,
                    Role = role,
                    RoleName = role switch
                    {
                        "Admin" => "Администратор",
                        "Manager" => "Менеджер",
                        _ => "Регистратор"
                    }
                });
            }

            var model = new UsersPageViewModel
            {
                Users = users,
                RegisterUser = new RegisterUserViewModel(),
                AccessSettings = BuildAccessSettingsMock(),
            };

            return View(model);
        }

        private static AccessSettingsViewModel BuildAccessSettingsMock()
        {
            static Dictionary<string, bool> VizAll() => new()
            {
                ["Admin"] = true,
                ["Manager"] = true,
                ["Registrator"] = true,
            };

            static Dictionary<string, bool> ReportDefault() => new()
            {
                ["Admin"] = true,
                ["Manager"] = true,
                ["Registrator"] = false,
            };

            IReadOnlyList<AccessRoleColumn> roles =
            [
                new("Admin", "Администратор"),
                new("Manager", "Менеджер"),
                new("Registrator", "Регистратор"),
            ];

            var vizItems = new List<AccessItemViewModel>
            {
                new()
                {
                    Key = "dashboard.waiting",
                    Title = "Ожидают сейчас",
                    Description = "Карточка: этапы без вызова, талон не завершён.",
                    RolePermissions = VizAll(),
                },
                new()
                {
                    Key = "dashboard.in-service",
                    Title = "Вызваны / на приёме сейчас",
                    Description = "Карточка: вызов состоялся, приём не завершён.",
                    RolePermissions = VizAll(),
                },
                new()
                {
                    Key = "dashboard.avg-wait",
                    Title = "Время ожидания за сегодня (среднее и макс.)",
                    Description = "Карточка: по завершённым этапам за сегодня.",
                    RolePermissions = VizAll(),
                },
                new()
                {
                    Key = "dashboard.avg-service",
                    Title = "Длительность приёма за сегодня (среднее и макс.)",
                    Description = "Карточка: по завершённым приёмам за сегодня.",
                    RolePermissions = VizAll(),
                },
                new()
                {
                    Key = "dashboard.chart-wait-serve",
                    Title = "Почасовой график ожидания и приёма за сегодня",
                    Description = "Линия: средние минуты по часам рабочего дня (корзина по часу вызова).",
                    RolePermissions = VizAll(),
                },
                new()
                {
                    Key = "dashboard.chart-cabinets-load",
                    Title = "Загруженность кабинетов/врачей за сегодня",
                    Description = "Столбцы: переключатель кабинет/врач и метрика (число приёмов / доля занятости).",
                    RolePermissions = VizAll(),
                },
                new()
                {
                    Key = "dashboard.queue-table",
                    Title = "Таблица текущей очереди",
                    Description = "Незавершённые талоны, сортировка по приоритету и ожиданию.",
                    RolePermissions = VizAll(),
                },
                new()
                {
                    Key = "dashboard.manager.queue-by-hour",
                    Title = "Пациенты в очередях по часу дня (период)",
                    Description = "Менеджер: линии по дням и среднее; фильтры кабинет/врач/категория.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = "dashboard.manager.wait-histogram",
                    Title = "Гистограмма времени ожидания",
                    Description = "Менеджер: корзины минут за период.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = "dashboard.manager.avg-wait-doctors",
                    Title = "Среднее ожидание по врачам",
                    Description = "Менеджер: горизонтальные столбцы, топ-N.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = "dashboard.manager.avg-service-doctors",
                    Title = "Средняя длительность приёма по врачам",
                    Description = "Менеджер: горизонтальные столбцы, топ-N.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = "dashboard.manager.heatmap",
                    Title = "Heatmap нагрузки (часы × кабинет/врач)",
                    Description = "Менеджер: среднее число завершённых приёмов в слот за день периода.",
                    RolePermissions = ReportDefault(),
                },
            };

            var reportItems = new List<AccessItemViewModel>
            {
                new()
                {
                    Key = $"report.{ReportIds.DoctorCabinetLoadDowntime}",
                    Title = "Загрузка врачей и кабинетов с оценкой простоев",
                    Description = "Доля занятого времени, число завершенных приемов, суммарный и средний простой.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = $"report.{ReportIds.WaitTimeDistribution}",
                    Title = "Время ожидания с распределением по времени",
                    Description = "Гистограмма ожидания, среднее, максимум, p50/p90/p95 и срез по часам дня.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = $"report.{ReportIds.ServiceDurationDistribution}",
                    Title = "Длительность приема с распределением по времени",
                    Description = "Распределение длительности обслуживания со срезами по врачу, кабинету и специальности.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = $"report.{ReportIds.FullCycleStageDelays}",
                    Title = "Полный цикл обслуживания и задержки между этапами",
                    Description = "Суммарное время прохождения, порядок этапов и межэтапные задержки.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = $"report.{ReportIds.UnservedChainBreaks}",
                    Title = "Необслуженные и обрывы цепочки",
                    Description = "Не вызваны, вызваны но не завершены, отменены/прерваны и доля неполных событий.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = $"report.{ReportIds.MultiStageService}",
                    Title = "Многоэтапное обслуживание",
                    Description = "Доля многоэтапных Appointment, время на этап и полное прохождение.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = $"report.{ReportIds.FlowBalanceArrivedVsCompleted}",
                    Title = "Баланс потока: поступило vs обслужено",
                    Description = "Поставлено в очередь, вызвано, начато, завершено, в работе на конец периода.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = $"report.{ReportIds.ServiceCategoriesPerformance}",
                    Title = "Показатели по категориям обслуживания",
                    Description = "Объем, ожидание avg/p90, длительность приема и доля многоэтапных.",
                    RolePermissions = ReportDefault(),
                },
                new()
                {
                    Key = $"report.{ReportIds.BottlenecksLongQueuesRanking}",
                    Title = "Узкие места и рейтинг проблемных зон",
                    Description = "Ранжирование врачей/кабинетов по p90 ожидания или доле превышений SLA.",
                    RolePermissions = ReportDefault(),
                },
            };

            IReadOnlyList<AccessGroupViewModel> groups =
            [
                new()
                {
                    Key = "viz",
                    Title = "Визуализации мониторинга очереди",
                    Icon = "bi-graph-up-arrow",
                    Items = vizItems,
                },
                new()
                {
                    Key = "report",
                    Title = "Отчёты",
                    Icon = "bi-journal-text",
                    Items = reportItems,
                },
            ];

            return new AccessSettingsViewModel
            {
                Roles = roles,
                Groups = groups,
            };
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterUserViewModel());
        }
    }
}