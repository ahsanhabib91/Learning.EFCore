using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

/*
 * https://github.com/rstropek/htl-leo-csharp-4/blob/master/exercises/0060-ef-relations/Program.cs
 * https://github.com/rstropek/htl-leo-csharp-4/blob/master/slides/ef-aspnet-cheat-sheet.md
 */

var factory = new BrickContextFactory();
using var context = factory.CreateDbContext(args);

//await AddData();
await QueryData();

#region Adding data with relations
async Task AddData()
{
    Vendor brickKing, bunteSteine, heldDerSteine, brickHeaven;
    await context.AddRangeAsync(new[]
    {
        brickKing = new Vendor { VendorName = "Brick King" },
        bunteSteine = new Vendor { VendorName = "Bunte Steine" },
        heldDerSteine = new Vendor { VendorName = "Held der Steine" },
        brickHeaven = new Vendor { VendorName = "Brick Heaven" },
    });
    await context.SaveChangesAsync();

    Tag rare, ninjago, minecraft;
    await context.AddRangeAsync(new[]
    {
        rare = new Tag { Title = "Rare" },
        ninjago = new Tag { Title = "Ninjago" },
        minecraft = new Tag { Title = "Mincraft" },
    });
    await context.SaveChangesAsync();

    await context.AddAsync(new BasePlate()
    {
        Title = "Baseplate 16 x 16 with Island on Blue Water Pattern",
        Color = Color.Green,
        Length = 16,
        Width = 16,
        Tags = new() { rare, minecraft },
        Availability = new() 
        {
            new() { Vendor = bunteSteine, AvailableAmount = 5, PriceEur = 6.6m },
            new() { Vendor = heldDerSteine, AvailableAmount = 10, PriceEur = 5.9m },
        },
    });
    await context.AddAsync(new Brick
    {
        Title = "Brick 1 x 2 x 1",
        Color = Color.Orange
    });
    await context.AddAsync(new MinifigHead
    {
        Title = "Minifigure, Head Dual Sided Black Eyebrows, Wide Open Mouth / Lopsided Grin",
        Color = Color.Yellow
    });
    await context.SaveChangesAsync();
}
#endregion

#region Loading data with relations
async Task QueryData()
{
    var availabilities = await context.BrickAvailabilities
        .Include(ba => ba.Brick)
        .Include(ba => ba.Vendor)
        .ToArrayAsync();

    //var brickWithVendors = await context.Bricks
    //    //.Include("Availability.Vendor")
    //    .Include($"{nameof(Brick.Availability)}.{nameof(BrickAvailability.Vendor)}")
    //    .Include(b => b.Tags)
    //    .ToArrayAsync();

    var bricks = await context.Bricks.ToArrayAsync();
    foreach (var item in bricks)
    {
        // Another way to load data without .InClude()
        await context.Entry(item).Collection(b => b.Tags).LoadAsync();
        if (!item.Tags.Any()) continue;
        Console.WriteLine($"Brick {item.Title} ({string.Join(',', item.Tags.Select(t => t.Title))}) ");
    }

}
#endregion

#region Model
// Note that we can use enums with EF without any issues
enum Color
{
    White,
    Black,
    DarkRed,
    Red,
    Coral,
    Tan,
    Nougat,
    DarkOrange,
    Orange,
    Yellow,
    Green
}

class Brick
{
    public int Id { get; set; }

    [MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    public Color? Color { get; set; }

    public List<Tag> Tags { get; set; } = new();

    public List<BrickAvailability> Availability { get; set; } = new();
}

class BasePlate : Brick
{
    public int Length { get; set; }

    public int Width { get; set; }
}

class MinifigHead : Brick
{
    public bool IsDualSided { get; set; }
}

class Tag
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    public List<Brick> Bricks { get; set; } = new();
}

class Vendor
{
    public int Id { get; set; }

    [MaxLength(250)]
    public string VendorName { get; set; } = string.Empty;

    public List<BrickAvailability> Availability { get; set; } = new();
}

class BrickAvailability
{
    public int Id { get; set; }

    [Required]
    public Brick? Brick { get; set; }

    public int? BrickId { get; set; }

    [Required]
    public Vendor? Vendor { get; set; }

    public int? VendorId { get; set; }

    public int AvailableAmount { get; set; }

    [Column(TypeName = "decimal(8, 2)")]
    public decimal PriceEur { get; set; }
}
#endregion

#region DBContext
class BrickContext : DbContext
{
    public DbSet<Brick> Bricks { get; set; }
    public DbSet<Vendor> Vendors { get; set; }
    public DbSet<BrickAvailability> BrickAvailabilities { get; set; }
    public DbSet<Tag> Tags { get; set; }

    public BrickContext(DbContextOptions<BrickContext> options) : base(options) { }
  
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BasePlate>().HasBaseType<Brick>();
        modelBuilder.Entity<MinifigHead>().HasBaseType<Brick>();
    }
}

class BrickContextFactory : IDesignTimeDbContextFactory<BrickContext>
{
    public BrickContext CreateDbContext(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<BrickContext>();
        optionsBuilder
            // Uncomment the following line if you want to print generated
            // SQL statements on the console.
            .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new BrickContext(optionsBuilder.Options);
    }
}
#endregion