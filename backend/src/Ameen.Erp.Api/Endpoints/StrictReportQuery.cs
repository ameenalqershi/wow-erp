using System.Reflection;
using Ameen.Erp.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Ameen.Erp.Api;

internal static class StrictReportQuery
{
    // Derive the accepted query shape from the handler itself. A new supported
    // filter must be explicitly bound; unknown parameters must never silently
    // produce a different accounting report or extraction audit.
    internal static RouteHandlerBuilder WithStrictReportQuery(this RouteHandlerBuilder route)
        => route.ProducesProblem(StatusCodes.Status422UnprocessableEntity).AddEndpointFilterFactory((factory, next) =>
        {
            var parameters = factory.MethodInfo.GetParameters()
                .Select(p => (Parameter: p, Binding: p.GetCustomAttribute<FromQueryAttribute>()))
                .Where(x => x.Binding is not null)
                .ToDictionary(x => x.Binding!.Name ?? x.Parameter.Name!,
                    x => x.Parameter.ParameterType.IsArray, StringComparer.OrdinalIgnoreCase);
            return invocation =>
            {
                foreach (var query in invocation.HttpContext.Request.Query)
                {
                    if (!parameters.TryGetValue(query.Key, out var repeated))
                        throw new BusinessException("unsupported_report_filter",
                            "أحد مرشحات التقرير غير مدعوم في هذا المسار.");
                    if (!repeated && query.Value.Count != 1)
                        throw new BusinessException("ambiguous_report_filter",
                            "يجب إرسال قيمة واحدة لكل مرشح مفرد.");
                }
                return next(invocation);
            };
        });
}
