var builder = WebApplication.CreateBuilder(args);
var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
var key = Environment.GetEnvironmentVariable("SUPABASE_KEY");

var options = new Supabase.SupabaseOptions
{
  AutoConnectRealtime = true
};

var supabase = new Supabase.Client(url!, key, options);

await supabase.InitializeAsync();

builder.Services.AddSingleton(supabase);

builder.Services.AddControllers();

builder.Services.AddCors(options => 
{
    options.AddPolicy("AllowDev",
        policy => policy.WithOrigins("https://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod());
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowDev");

app.MapControllers();

app.Run();
