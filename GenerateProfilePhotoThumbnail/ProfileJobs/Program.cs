using Hangfire;
using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using ProfileJobs.Application;
using ProfileJobs.Infrastructure.Persistence;
using ProfileJobs.Jobs;
using ProfileJobs.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var cs = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options => { options.UseSqlServer(cs); });

var hangfireConnection = builder.Configuration.GetConnectionString("Hangfire");
builder.Services.AddHangfire(x => x.UseSqlServerStorage(hangfireConnection));
builder.Services.AddHangfireServer();

builder.Services.AddSingleton<IMinioService, MinioService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHangfireDashboard();

var recurring = app.Services.GetRequiredService<IRecurringJobManager>();

recurring.AddOrUpdate(
    recurringJobId: "CreateThumbnailJob",
    job: Job.FromExpression<CreateThumbnailJob>(job =>
        job.ProcessProfilesWithoutThumbnails(CancellationToken.None)),
    cronExpression: "*/5 * * * *",
    options: new RecurringJobOptions());

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
