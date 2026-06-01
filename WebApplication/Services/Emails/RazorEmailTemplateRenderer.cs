using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using WebApplication.Models.Emails;

namespace WebApplication.Services.Emails;

public sealed class RazorEmailTemplateRenderer : IEmailTemplateRenderer
{
    private const string RegistrationView = "/Views/Shared/Components/RegistrationEmail/Default.cshtml";
    private const string PasswordResetView = "/Views/Shared/Components/PasswordResetEmail/Default.cshtml";

    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RazorEmailTemplateRenderer(
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<string> RenderRegistrationAsync(RegistrationEmailViewModel model) =>
        RenderViewAsync(RegistrationView, model);

    public Task<string> RenderPasswordResetAsync(PasswordResetEmailViewModel model) =>
        RenderViewAsync(PasswordResetView, model);

    private async Task<string> RenderViewAsync<TModel>(string viewPath, TModel model)
    {
        var actionContext = CreateActionContext();
        var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: true);
        if (!viewResult.Success)
            throw new InvalidOperationException($"Не найден шаблон письма: {viewPath}");

        await using var writer = new StringWriter();
        var viewDictionary = new ViewDataDictionary<TModel>(
            metadataProvider: new EmptyModelMetadataProvider(),
            modelState: new ModelStateDictionary())
        {
            Model = model
        };

        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewDictionary,
            new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }

    private ActionContext CreateActionContext()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? new DefaultHttpContext { RequestServices = _serviceProvider };

        var routeData = httpContext.GetRouteData() ?? new RouteData();
        var actionDescriptor = new ActionDescriptor();

        return new ActionContext(httpContext, routeData, actionDescriptor);
    }
}
