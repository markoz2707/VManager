using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HyperV.CentralManagement.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var connectionString = "Host=localhost;Port=6485;Database=hyperv_central;Username=hyperv;Password=hyperv";
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
