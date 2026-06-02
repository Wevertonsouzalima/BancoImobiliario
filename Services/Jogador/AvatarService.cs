using Microsoft.AspNetCore.Components.Forms;
using System.Text;

namespace BancoImobiliario.Services.Jogador;

/// <summary>
/// Responsável por salvar imagens de avatar enviadas pelos jogadores.
/// Os arquivos são gravados em wwwroot/avatares e o caminho relativo
/// retornado é gravado em Tb_PartidaJogador.UrlAvatar.
/// </summary>
public class AvatarService
{
    private const long TamanhoMaximoBytes = 5 * 1024 * 1024; // 5 MB
    private const string PastaRelativa = "avatares";

    private static readonly string[] ExtensoesPermitidas =
        { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    private static readonly string[] ContentTypesPermitidos =
        { "image/jpeg", "image/png", "image/webp", "image/gif" };

    private readonly IWebHostEnvironment _ambiente;
    private readonly ILogger<AvatarService> _logger;

    public AvatarService(IWebHostEnvironment ambiente, ILogger<AvatarService> logger)
    {
        _ambiente = ambiente;
        _logger = logger;
    }

    /// <summary>
    /// Salva o arquivo enviado e retorna o caminho relativo (ex: /avatares/nome-abc123.png).
    /// </summary>
    /// <param name="arquivo">Arquivo recebido do componente InputFile.</param>
    /// <param name="nomeDesejado">Nome informado pelo jogador (será sanitizado).</param>
    public async Task<string> SalvarUploadAsync(
        IBrowserFile arquivo,
        string? nomeDesejado,
        CancellationToken cancellationToken = default)
    {
        if (arquivo == null)
            throw new InvalidOperationException("Nenhum arquivo foi enviado.");

        if (arquivo.Size <= 0)
            throw new InvalidOperationException("O arquivo enviado está vazio.");

        if (arquivo.Size > TamanhoMaximoBytes)
            throw new InvalidOperationException(
                $"A imagem excede o tamanho máximo de {TamanhoMaximoBytes / (1024 * 1024)} MB.");

        var extensao = Path.GetExtension(arquivo.Name).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(extensao) || !ExtensoesPermitidas.Contains(extensao))
            throw new InvalidOperationException(
                "Formato inválido. Use uma imagem JPG, PNG, WEBP ou GIF.");

        if (!string.IsNullOrWhiteSpace(arquivo.ContentType) &&
            !ContentTypesPermitidos.Contains(arquivo.ContentType.ToLowerInvariant()))
        {
            throw new InvalidOperationException("O conteúdo enviado não é uma imagem válida.");
        }

        // Garante que a pasta de destino existe (wwwroot/avatares)
        var raizWeb = string.IsNullOrWhiteSpace(_ambiente.WebRootPath)
            ? Path.Combine(_ambiente.ContentRootPath, "wwwroot")
            : _ambiente.WebRootPath;

        var pastaDestino = Path.Combine(raizWeb, PastaRelativa);
        Directory.CreateDirectory(pastaDestino);

        // Nome final: nome do jogador sanitizado + sufixo único + extensão real
        var nomeBase = SanitizarNome(nomeDesejado);
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var nomeArquivo = $"{nomeBase}-{sufixo}{extensao}";

        var caminhoFisico = Path.Combine(pastaDestino, nomeArquivo);

        try
        {
            await using var origem = arquivo.OpenReadStream(TamanhoMaximoBytes, cancellationToken);
            await using var destino = File.Create(caminhoFisico);
            await origem.CopyToAsync(destino, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar avatar. Nome={NomeArquivo}", nomeArquivo);
            throw new InvalidOperationException("Não foi possível salvar a imagem.", ex);
        }

        // Caminho relativo servido pela aplicação
        return $"/{PastaRelativa}/{nomeArquivo}";
    }

    /// <summary>
    /// Remove caracteres inválidos do nome informado, evitando path traversal
    /// e nomes de arquivo inválidos. Mantém apenas letras, números e hífen.
    /// </summary>
    private static string SanitizarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return "avatar";

        nome = nome.Trim().ToLowerInvariant();

        var sb = new StringBuilder(nome.Length);

        foreach (var c in nome)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (c is ' ' or '-' or '_')
                sb.Append('-');
            // qualquer outro caractere é descartado
        }

        var limpo = sb.ToString().Trim('-');

        // Colapsa hífens repetidos
        while (limpo.Contains("--"))
            limpo = limpo.Replace("--", "-");

        if (string.IsNullOrWhiteSpace(limpo))
            return "avatar";

        // Limita o tamanho para não estourar o limite de caminho do SO
        if (limpo.Length > 40)
            limpo = limpo[..40].Trim('-');

        return limpo;
    }
}
