var builder = WebApplication.CreateBuilder(args);


//lägg till stöd för controllers
builder.Services.AddControllers();


//lägg till stöd för swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


//Gör våra controllers tillgängliga via endpoints
app.MapControllers();

app.Run();