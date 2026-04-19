namespace API.RequestHelper;

public class StripContentLanguageHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            request.Content.Headers.ContentLanguage.Clear();
            request.Content.Headers.ContentLanguage.Add("en-GB");
        }
        return base.SendAsync(request, cancellationToken);
    }
}