var builder = WebApplication.CreateBuilder(args);


//lägg till stöd för controllers
builder.Services.AddControllers();


//lägg till stöd för swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


//API:t tillåter vår React-app 
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy
              .WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//lägg till stöd för statiska filer (t.ex. index.html)
app.UseStaticFiles();


//Gör våra controllers tillgängliga via endpoints
app.MapControllers();

app.Run();