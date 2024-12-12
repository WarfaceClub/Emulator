using Emulator.Model;
using Microsoft.EntityFrameworkCore;

namespace Emulator;

public class GameDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
}

public class GameDbContextOptions : DbContextOptions<GameDbContext>
{

}