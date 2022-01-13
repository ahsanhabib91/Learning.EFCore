using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;

/*
 * https://github.com/rstropek/htl-leo-csharp-4/blob/master/exercises/0040-ef-intro/Program.cs
 * https://github.com/rstropek/htl-leo-csharp-4/blob/master/exercises/0050-ef-advanced/Program.cs
 * https://github.com/rstropek/htl-leo-csharp-4/blob/master/slides/ef-aspnet-cheat-sheet.md
 */

var factory = new CookbookContextFactory();
using var dbContext = factory.CreateDbContext(args);

//var soup = new Dish
//{
//    Title = "Thai Soup",
//    Notes = "Thai soup is really good",
//    Stars = 5,
//    Ingredients = new() 
//    {
//        new() { Amount = 3, Description = "Red Chilli", UnitOfMeasure = "Pices" },
//        new() { Amount = 7, Description = "Salt", UnitOfMeasure = "Table spoon" },
//    }
//};
//dbContext.Dishes.Add(soup);
//await dbContext.SaveChangesAsync();

//Console.WriteLine("Adding Porridge for breakfast");
//var porridge = new Dish {
//    Id = 2,
//    //Title = "Breakfast Porridge", Notes = "This is soooo good", Stars = 4 
//};
//dbContext.Dishes.Add(porridge);
//await dbContext.SaveChangesAsync();
//Console.WriteLine($"Added Porridge (id = {porridge.Id}) successfully");

//Console.WriteLine("Checking Stars for Porridge");
//var dishes = await dbContext.Dishes
//    .Where(d => d.Title.Contains("Porridge"))
//    .ToListAsync();
//Console.WriteLine($"Porridge has {dishes[0].Stars} stars");

//Console.WriteLine("Changing Porridge stars to 5");
//porridge.Stars = 5;
//await dbContext.SaveChangesAsync();
//Console.WriteLine($"Porridge: {porridge.Id}, {porridge.Stars} Update Done !!!");

//Console.WriteLine("Removing data");
//dbContext.Dishes.Remove(porridge);
//await dbContext.SaveChangesAsync();
//Console.WriteLine("Porridge removed");


// An experiment
//var newDish = new Dish { Title = "Foo", Notes = "Bar" };
//dbContext.Dishes.Add(newDish);
//await dbContext.SaveChangesAsync();
//newDish.Notes = "Baz";
//await dbContext.SaveChangesAsync();


//await EntityStates(factory);
//await ChangeTracking(factory);
//await AttachEntities(factory);
//await NoTracking(factory);
//await RawSql(factory);
//await Transactions(factory);
//await ExpressionTrees(factory);


static async Task EntityStates(CookbookContextFactory factory)
{
    using var dbContext = factory.CreateDbContext();

    var newDish = new Dish { Title = "Foo", Notes = "Bar" };
    var state = dbContext.Entry(newDish).State; // Detached

    dbContext.Dishes.Add(newDish);
    state = dbContext.Entry(newDish).State; // Added

    await dbContext.SaveChangesAsync();
    state = dbContext.Entry(newDish).State; // Unchanged

    newDish.Notes = "Baz";
    state = dbContext.Entry(newDish).State; // Modified

    await dbContext.SaveChangesAsync();

    dbContext.Dishes.Remove(newDish);
    state = dbContext.Entry(newDish).State; // Deleted

    await dbContext.SaveChangesAsync();
    state = dbContext.Entry(newDish).State; // Detached
}

static async Task ChangeTracking(CookbookContextFactory factory)
{
    using var dbContext = factory.CreateDbContext();
    var newDish = new Dish { Title = "Foo", Notes = "Bar" };
    dbContext.Dishes.Add(newDish);
    await dbContext.SaveChangesAsync();
    newDish.Notes = "Baz";

    var entry = dbContext.Entry(newDish);
    var originalValue = entry.OriginalValues[nameof(Dish.Notes)].ToString();
    var dishFromDatabase = await dbContext.Dishes.SingleAsync(d => d.Id == newDish.Id);
    var dishes = await dbContext.Dishes
        //.Where(d => d.Title.Contains("Porridge"))
        .Where(d => d.Id == newDish.Id)
        .ToListAsync();

    // ---
    using var dbContext2 = factory.CreateDbContext();
    var dishFromDatabase2 = await dbContext2.Dishes.SingleAsync(d => d.Id == newDish.Id);
}

static async Task AttachEntities(CookbookContextFactory factory)
{
    using var dbContext = factory.CreateDbContext();

    var newDish = new Dish { Title = "Foo", Notes = "Bar" };
    dbContext.Dishes.Add(newDish);
    await dbContext.SaveChangesAsync();

    dbContext.Entry(newDish).State = EntityState.Detached;
    var state = dbContext.Entry(newDish).State;

    dbContext.Dishes.Update(newDish);
    await dbContext.SaveChangesAsync();
    state = dbContext.Entry(newDish).State;
}

static async Task NoTracking(CookbookContextFactory factory)
{
    using var dbContext = factory.CreateDbContext();

    // SELECT * FROM Dishes
    var dishes = await dbContext.Dishes.AsNoTracking().ToArrayAsync();
    var state = dbContext.Entry(dishes[0]).State;
}

static async Task RawSql(CookbookContextFactory factory)
{
    using var dbContext = factory.CreateDbContext();

    // Read data with raw SQL. Note that change tracking works just like before
    var dishes = await dbContext.Dishes
        .FromSqlRaw("SELECT * FROM Dishes")
        .ToArrayAsync();

    // Read data with parameters. Make sure to check the generated SQL query.
    var filter = "%z";
    dishes = await dbContext.Dishes
        .FromSqlInterpolated($"SELECT * FROM Dishes WHERE Notes LIKE {filter}")
        .AsNoTracking()
        .ToArrayAsync();

    // This is BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD,
    // BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD, BAD
    dishes = await dbContext.Dishes
        .FromSqlRaw("SELECT * FROM Dishes WHERE Notes LIKE '" + filter + "'")
        .AsNoTracking()
        .ToArrayAsync();

    // Write data
    await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Dishes WHERE Id NOT IN (SELECT DishId FROM Ingredients)");
}

static async Task Transactions(CookbookContextFactory factory)
{
    using var dbContext = factory.CreateDbContext();

    // Let's begin a transaction. All DB change from here until Commit
    // are either completely written or not written at all (rollback).
    using var transaction = await dbContext.Database.BeginTransactionAsync();
    try
    {
        dbContext.Dishes.Add(new Dish { Title = "Foo", Notes = "Bar" });
        await dbContext.SaveChangesAsync();

        // Let's generate an error in the DB (division by 0)
        await dbContext.Database.ExecuteSqlRawAsync("SELECT 1/0 AS Meaningless");
        await transaction.CommitAsync();
    }
    catch (SqlException ex)
    {
        Console.Error.WriteLine($"Something bad happened: {ex.Message}");
    }
}

static async Task ExpressionTrees(CookbookContextFactory factory)
{
    var dbContext = factory.CreateDbContext();

    // Let's add some demo data
    dbContext.Dishes.Add(new Dish { Title = "Foo", Notes = "Barbarbarbarbar" });
    await dbContext.SaveChangesAsync();

    // How can EFCore translate the following query into SQL???
    var dishes = await dbContext.Dishes
        .Where(d => d.Title.StartsWith("F"))
        .Select(d => new { Description = $"{d.Title} ({d.Notes!.Substring(0, 10)}...)" })
        .ToArrayAsync();

    // The secret: Expression trees
    Func<Dish, bool> f = d => d.Title.StartsWith("F");
    Expression<Func<Dish, bool>> ex = d => d.Title.StartsWith("F");

    // Dynamic query with Expression trees
    var d = Expression.Parameter(typeof(Dish), "d");
    var sw = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) });
    ex = Expression.Lambda<Func<Dish, bool>>(
        Expression.Call(
            Expression.Property(d, nameof(Dish.Title)),
            sw!,
            Expression.Constant("F")),
        d);

    dishes = await dbContext.Dishes
        .Where(ex)
        .Select(d => new { Description = $"{d.Title} ({d.Notes!.Substring(0, 10)}...)" })
        .ToArrayAsync();
}

// Create a Model
class Dish
{
    public int Id { get; set; }
    
    [MaxLength(100)]
    public string Title { get; set; } = String.Empty;
    
    [MaxLength(1000)]
    public string? Notes { get; set; }
    
    public int? Stars { get; set; }

    public List<DishIngredient> Ingredients { get; set; } = new();
}

class DishIngredient
{
    public int Id { get; set; }
    
    [MaxLength(100)]
    public string Description { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string UnitOfMeasure { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(5, 2)")]
    public decimal Amount { get; set; }

    public Dish? Dish { get; set; }

    public int DishId { get; set; }

}

class CookbookContext : DbContext
{
    public DbSet<Dish> Dishes { get; set; }
    public DbSet<DishIngredient> Ingredients { get; set; }

    public CookbookContext(DbContextOptions<CookbookContext> options) : base(options)
    { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Dish>()
            .HasMany(d => d.Ingredients)
            .WithOne(d => d.Dish)
            .HasForeignKey(d => d.DishId)
            //.OnDelete(DeleteBehavior.NoAction);
            .OnDelete(DeleteBehavior.Cascade); // default
    }
}

// This factory is responsible for creating our DB context. Note that
// this will NOT BE NECESSARY anymore once we move to ASP.NET.
class CookbookContextFactory : IDesignTimeDbContextFactory<CookbookContext>
{
    public CookbookContext CreateDbContext(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<CookbookContext>();
        optionsBuilder
             // Uncomment the following line if you want to print generated
             // SQL statements on the console.
             .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new CookbookContext(optionsBuilder.Options);
    }
}