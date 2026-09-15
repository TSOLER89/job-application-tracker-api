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

//lägg till stöd för statiska filer (t.ex. index.html)
app.UseStaticFiles();


//Gör våra controllers tillgängliga via endpoints
app.MapControllers();

app.Run();