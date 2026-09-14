var builder = WebApplication.CreateBuilder(args);


//lägg till stöd för controllers
builder.Services.AddControllers();

//OpenAPI används för att beskriva API:et
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


//Gör våra controllers tillgängliga via endpoints
app.MapControllers();

app.Run();