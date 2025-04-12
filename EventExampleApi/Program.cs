using Amazon;
using Amazon.Runtime;
using Amazon.Extensions.NETCore.Setup;
using EventChannelLib;
using EventExampleApi.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure AWS options based on environment
var awsOptions = new AWSOptions
{
    Region = RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"] ?? "us-east-1"),
};

// Configure LocalStack if enabled
if (builder.Configuration.GetValue<bool>("AWS:UseLocalStack"))
{
    awsOptions.Credentials = new BasicAWSCredentials(
        builder.Configuration["AWS:AccessKey"],
        builder.Configuration["AWS:SecretKey"]);

    var localStackUrl = builder.Configuration["AWS:LocalStackUrl"];

    awsOptions.DefaultClientConfig.ServiceURL = localStackUrl;
    builder.Services.AddEventChannel<OrderCreatedEvent>(options =>
    {
        options.TopicArn = builder.Configuration["EventChannel:TopicArn"];
        options.MaxRetryAttempts = 3;
        options.BoundedCapacity = 1_000_000;
        options.ServiceUrl = localStackUrl;
    });
    builder.Services.AddEventChannel<OrderCreatedEvent2>(options =>
    {
        options.TopicArn = builder.Configuration["EventChannel:TopicArn"];
        options.MaxRetryAttempts = 3;
        options.BoundedCapacity = 1_000_000;
        options.ServiceUrl = localStackUrl;
    });
    builder.Services.AddEventRaiser();

}
else
{
    // Standard AWS configuration for production
    builder.Services.AddDefaultAWSOptions(awsOptions);
    builder.Services.AddSingleton<EventRaiser>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
