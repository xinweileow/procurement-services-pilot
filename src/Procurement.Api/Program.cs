using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procurement.Api.Common;
using Procurement.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// docs/kb/technical_kb.md Conventions: validation failures are 422, not the framework's
// default 400 — override [ApiController]'s automatic invalid-ModelState response.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "One or more validation errors occurred.",
            Type = "https://httpstatuses.io/422",
        };
        return new UnprocessableEntityObjectResult(problemDetails);
    };
});

builder.Services.AddDbContext<ProcurementDbContext>(options => options.UseInMemoryDatabase("procurement"));
builder.Services.AddScoped<Procurement.Api.Services.IRequestSubmissionService, Procurement.Api.Services.RequestSubmissionService>();

// RFC 7807 ProblemDetails for every non-2xx response (docs/kb/technical_kb.md Conventions).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler(_ => { }); // registered before anything else so it wraps the whole pipeline

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
