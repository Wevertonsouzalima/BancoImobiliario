using Microsoft.EntityFrameworkCore;

namespace BancoImobiliario.Data
{
    public class AppDbContextTeste: DbContext
    {
        public AppDbContextTeste(DbContextOptions<AppDbContext> options)
     : base(options)
        {
        }



    }
}
