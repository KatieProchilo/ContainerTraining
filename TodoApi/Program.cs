using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
  EnvironmentName = Environments.Development, // "ASPNETCORE_ENVIRONMENT": "Development",
});

builder.WebHost.UseKestrel(serverOptions =>
{
  serverOptions.ListenAnyIP(5000); // Listen for incoming HTTP connection on port 5001.
});

// Add services to the container. Learn more about configuring Swagger/OpenAPI
// at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<TodoDbContext>(opt => opt.UseInMemoryDatabase("TodoList"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.MapFallback(() => Results.Redirect("/swagger"));

app.MapGet("/todos", async (TodoDbContext db) =>
    await db.Todos.Select(x => new TodoItemDTO(x)).ToListAsync());

app.MapPost("/todos", async (TodoItemDTO todoItemDto, TodoDbContext db) =>
{
  var todoItem = new Todo
  {
    IsComplete = todoItemDto.IsComplete,
    Name = todoItemDto.Name
  };

  db.Todos.Add(todoItem);
  await db.SaveChangesAsync();

  return Results.Created($"/todos/{todoItem.Id}", new TodoItemDTO(todoItem));
});

app.MapDelete("/todos/{id}", async (int id, TodoDbContext db) =>
{
  if (await db.Todos.FindAsync(id) is Todo todo)
  {
    db.Todos.Remove(todo);
    await db.SaveChangesAsync();
    return Results.Ok(new TodoItemDTO(todo));
  }

  return Results.NotFound();
});

app.Run();
