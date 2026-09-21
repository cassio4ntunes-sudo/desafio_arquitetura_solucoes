namespace FluxoDeCaixa.Web.Services;

/// <summary>
/// Erro de API já traduzido para uma mensagem que faz sentido ao comerciante.
/// O dialog exibe Message direto no Snackbar.
/// </summary>
public class ApiException : Exception
{
    public ApiException(string mensagem) : base(mensagem) { }
}
