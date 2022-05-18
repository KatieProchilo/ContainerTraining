using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class Todo
{
  public int Id { get; set; }
  [Required]
  public string? Name { get; set; }
  public bool IsComplete { get; set; }
  public string? Secret { get; set; }
}

public class TodoItemDTO
{
  public int Id { get; set; }
  public string? Name { get; set; }
  public bool IsComplete { get; set; }

  public TodoItemDTO() { }
  public TodoItemDTO(Todo todoItem) =>
    (Id, Name, IsComplete) = (todoItem.Id, todoItem.Name, todoItem.IsComplete);
}

class TodoDbContext : DbContext
{
  public TodoDbContext(DbContextOptions<TodoDbContext> options)
    : base(options) { }

  public DbSet<Todo> Todos => Set<Todo>();
}