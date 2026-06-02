using BancoImobiliario.Data;
using BancoImobiliario.Models.Casas;
using DllTeste.Banco.Services;
using Microsoft.EntityFrameworkCore;


namespace BancoImobiliario.Services.Casas
{
    public class PrisaoService : ManipulacaoService<AppDbContext, Prisao>
    {
        public PrisaoService(IDbContextFactory<AppDbContext> contextFactory, ILogger<PrisaoService>? logger = null)
            : base(contextFactory, logger)
        {
        }

        protected override Task AntesDeInserirAsync(AppDbContext context, Prisao entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = DateTime.Now;

            // CORREÇÃO AQUI: Estava chamando o base.AntesDeAtualizarAsync!
            return base.AntesDeInserirAsync(context, entidade, cancellationToken);
        }

        protected override Task AntesDeAtualizarAsync(AppDbContext context, Prisao entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = entidade.DataCadastro == default ? DateTime.Now : entidade.DataCadastro;

            return base.AntesDeAtualizarAsync(context, entidade, cancellationToken);
        }
    }


// Serviço para Imposto
public class ImpostoService : ManipulacaoService<AppDbContext, Imposto>
    {
        public ImpostoService(IDbContextFactory<AppDbContext> contextFactory, ILogger<ImpostoService>? logger = null)
            : base(contextFactory, logger)
        {
        }
        protected override Task AntesDeInserirAsync(AppDbContext context, Imposto entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = DateTime.Now;

            // CORREÇÃO AQUI: Estava chamando o base.AntesDeAtualizarAsync!
            return base.AntesDeInserirAsync(context, entidade, cancellationToken);
        }

        protected override Task AntesDeAtualizarAsync(AppDbContext context, Imposto entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = entidade.DataCadastro == default ? DateTime.Now : entidade.DataCadastro;

            return base.AntesDeAtualizarAsync(context, entidade, cancellationToken);
        }
    }

    // Serviço para Cidade
    public class CidadeService : ManipulacaoService<AppDbContext, Cidade>
    {
        public CidadeService(IDbContextFactory<AppDbContext> contextFactory, ILogger<CidadeService>? logger = null)
            : base(contextFactory, logger)
        {
        }
        protected override Task AntesDeInserirAsync(AppDbContext context, Cidade entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = DateTime.Now;

            // CORREÇÃO AQUI: Estava chamando o base.AntesDeAtualizarAsync!
            return base.AntesDeInserirAsync(context, entidade, cancellationToken);
        }

        protected override Task AntesDeAtualizarAsync(AppDbContext context, Cidade entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = entidade.DataCadastro == default ? DateTime.Now : entidade.DataCadastro;

            return base.AntesDeAtualizarAsync(context, entidade, cancellationToken);
        }
    }

    // Serviço para Companhia
    public class CompanhiaService : ManipulacaoService<AppDbContext, Companhia>
    {
        public CompanhiaService(IDbContextFactory<AppDbContext> contextFactory, ILogger<CompanhiaService>? logger = null)
            : base(contextFactory, logger)
        {
        }
        protected override Task AntesDeInserirAsync(AppDbContext context, Companhia entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = DateTime.Now;

            // CORREÇÃO AQUI: Estava chamando o base.AntesDeAtualizarAsync!
            return base.AntesDeInserirAsync(context, entidade, cancellationToken);
        }

        protected override Task AntesDeAtualizarAsync(AppDbContext context, Companhia entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = entidade.DataCadastro == default ? DateTime.Now : entidade.DataCadastro;

            return base.AntesDeAtualizarAsync(context, entidade, cancellationToken);
        }
    }

    // Serviço para Efeitos
    public class EfeitoService : ManipulacaoService<AppDbContext, Efeito>
    {
        public EfeitoService(IDbContextFactory<AppDbContext> contextFactory, ILogger<EfeitoService>? logger = null)
            : base(contextFactory, logger)
        {
        }
        protected override Task AntesDeInserirAsync(AppDbContext context, Efeito entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = DateTime.Now;

            // CORREÇÃO AQUI: Estava chamando o base.AntesDeAtualizarAsync!
            return base.AntesDeInserirAsync(context, entidade, cancellationToken);
        }

        protected override Task AntesDeAtualizarAsync(AppDbContext context, Efeito entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = entidade.DataCadastro == default ? DateTime.Now : entidade.DataCadastro;

            return base.AntesDeAtualizarAsync(context, entidade, cancellationToken);
        }
    }

    // Serviço para Casas Especiais
    public class CasaEspecialService : ManipulacaoService<AppDbContext, CasaEspecial>
    {
        public CasaEspecialService(IDbContextFactory<AppDbContext> contextFactory, ILogger<CasaEspecialService>? logger = null)
            : base(contextFactory, logger)
        {
        }
        protected override Task AntesDeInserirAsync(AppDbContext context, CasaEspecial entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = DateTime.Now;

            // CORREÇÃO AQUI: Estava chamando o base.AntesDeAtualizarAsync!
            return base.AntesDeInserirAsync(context, entidade, cancellationToken);
        }

        protected override Task AntesDeAtualizarAsync(AppDbContext context, CasaEspecial entidade, CancellationToken cancellationToken = default)
        {
            entidade.DataAtualizacao = DateTime.Now;
            entidade.DataCadastro = entidade.DataCadastro == default ? DateTime.Now : entidade.DataCadastro;

            return base.AntesDeAtualizarAsync(context, entidade, cancellationToken);
        }
    }
}