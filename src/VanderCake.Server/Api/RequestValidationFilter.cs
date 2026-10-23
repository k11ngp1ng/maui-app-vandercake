using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace VanderCake.Server.Api;

public sealed class RequestValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();
        void Visit(object? value, string path)
        {
            if (value is null) return;
            if (value is IEnumerable items && value is not string)
            {
                var index = 0;
                foreach (var item in items)
                {
                    if (item is null) errors[$"{path}[{index}]"] = ["Item is required."];
                    else Visit(item, $"{path}[{index}]");
                    index++;
                }
                return;
            }
            if (value.GetType().Namespace != "VanderCake.Contracts.Api") return;
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(value, new ValidationContext(value), results, true);
            foreach (var result in results)
                foreach (var member in result.MemberNames.DefaultIfEmpty(""))
                {
                    var key = path + member;
                    if (!errors.TryGetValue(key, out var messages)) errors[key] = messages = [];
                    messages.Add(result.ErrorMessage ?? "Invalid input.");
                }
            foreach (var property in value.GetType().GetProperties())
                Visit(property.GetValue(value), path + property.Name + ".");
        }
        foreach (var argument in context.Arguments) Visit(argument, "");
        return errors.Count > 0
            ? Results.ValidationProblem(errors.ToDictionary(x => x.Key, x => x.Value.ToArray()))
            : await next(context);
    }
}
