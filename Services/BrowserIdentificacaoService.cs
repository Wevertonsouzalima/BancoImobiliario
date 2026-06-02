using Microsoft.JSInterop;

namespace BancoImobiliario.Services;

public class BrowserIdentificacaoService
{
    private readonly IJSRuntime _jsRuntime;

    private const string ChaveIdentificador = "BancoImobiliario.IdentificadorJogador";
    private const string ChaveNome = "BancoImobiliario.NomeJogador";

    public BrowserIdentificacaoService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string> ObterOuCriarIdentificadorAsync()
    {
        var identificador = await _jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            ChaveIdentificador);

        if (!string.IsNullOrWhiteSpace(identificador))
            return identificador;

        identificador = Guid.NewGuid().ToString("N");

        await _jsRuntime.InvokeVoidAsync(
            "localStorage.setItem",
            ChaveIdentificador,
            identificador);

        return identificador;
    }

    public async Task<string?> ObterNomeAsync()
    {
        var nome = await _jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            ChaveNome);

        return string.IsNullOrWhiteSpace(nome)
            ? null
            : nome.Trim();
    }

    public async Task SalvarNomeAsync(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new InvalidOperationException("Informe o nome do jogador.");

        await _jsRuntime.InvokeVoidAsync(
            "localStorage.setItem",
            ChaveNome,
            nome.Trim());
    }
}