using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication.Models;

public class DashboardController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Мониторинг очереди";

        // В реальном проекте — из сервиса/БД
        var model = new DashboardViewModel
        {
            WaitingCount = 14,
            InServiceCount = 6,
            AvgWaitMinutes = 21,
            AvgServiceMinutes = 17,

            // Данные для графика (последние 7 дней)
            DailyLabels = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"],
            DailyWaitMinutes = [24, 19, 22, 18, 21, 16, 14],
            DailyServiceMinutes = [16, 17, 15, 18, 17, 14, 13],

            // Данные для графика загрузки кабинетов
            Cabinets = [
                new("Каб. 101", 88),
                new("Каб. 102", 76),
                new("Каб. 103", 82),
                new("Каб. 201", 91),
                new("Каб. 202", 69),
                new("Каб. 203", 74),
            ],
        };

        return View(model);
    }
}