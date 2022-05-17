using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

app.MapGet("/todos/complete", async (TodoDbContext db) =>
    await db.Todos.Where(t => t.IsComplete).ToListAsync());

app.MapGet("/todos/{id}", async (int id, TodoDbContext db) =>
    await db.Todos.FindAsync(id)
        is Todo todo
            ? Results.Ok(new TodoItemDTO(todo))
            : Results.NotFound());

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

app.MapPut("/todos/{id}", async (int id, TodoItemDTO inputTodoItemDTO, TodoDbContext db) =>
{
  var todo = await db.Todos.FindAsync(id);

  if (todo is null) return Results.NotFound();

  todo.Name = inputTodoItemDTO.Name;
  todo.IsComplete = inputTodoItemDTO.IsComplete;

  await db.SaveChangesAsync();

  return Results.NoContent();
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

public class TodoItemDTO
{
  public int Id { get; set; }
  public string? Name { get; set; }
  public bool IsComplete { get; set; }

  public TodoItemDTO() { }
  public TodoItemDTO(Todo todoItem) =>
    (Id, Name, IsComplete) = (todoItem.Id, todoItem.Name, todoItem.IsComplete);
}
