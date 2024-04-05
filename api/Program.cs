using Amazon.S3;
using Amazon.S3.Model;
using api.Models;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<MinioAccessConfiguration>(builder.Configuration.GetSection("Minio:AccessConfiguration"));

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var accessCredentials = sp.GetService<IOptions<MinioAccessConfiguration>>()?.Value;

    var minioEndpoint = accessCredentials?.Endpoint;

    var accessKey = accessCredentials?.AccessKey;

    var secretKey = accessCredentials?.SecretKey;

    var config = new AmazonS3Config
    {
        ServiceURL = minioEndpoint,

        ForcePathStyle = true
    };

    return new AmazonS3Client(accessKey, secretKey, config);
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

builder.Services.AddAntiforgery();

var app = builder.Build();

app.UseAntiforgery();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.MapGet("/api/file/{bucketName}/{objectKey}", (IAmazonS3 client, string bucketName, string objectKey)
    => client.GetPreSignedURL(new GetPreSignedUrlRequest()
    {
        BucketName = bucketName,
        Key = objectKey,
        Verb = HttpVerb.GET,
        Protocol = Protocol.HTTP,
        Expires = DateTime.UtcNow.AddHours(6)
    }));


app.MapPost("/api/file/{bucketName}/{objectKey}", async (IFormFile file, IAmazonS3 client, HttpContext context, string bucketName, string objectKey) =>
{
    if (file == null || file.Length == 0)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("No file uploaded.");
        return;
    }

    var path = Path.Combine(Directory.GetCurrentDirectory(), file.FileName);// "uploads",

    using (var stream = new FileStream(path, FileMode.Create))
    {
        await file.CopyToAsync(stream);

        var putObjectRequest = new PutObjectRequest
        {
            BucketName = "teste", // Your bucket name
            Key = "2024-02-101337-275-72300-" + file.GetType().ToString(), //Guid.NewGuid().ToString(), // Unique key for the file
            InputStream = stream,
            ContentType = file.ContentType
        };

        try
        {
            await client.PutObjectAsync(putObjectRequest);
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync("File uploaded successfully.");
        }
        catch (AmazonS3Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync($"Error uploading file: {ex.Message}");
        }

    }
})
   .DisableAntiforgery();

app.Run();