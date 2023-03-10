﻿using System.CommandLine;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityModel.Client;
using IdentityModel.OidcClient;
using Microsoft.Extensions.Logging;

namespace OidcCli;

public class AuthenticateCommand : RootCommand
{
    private readonly ILoggerFactory _loggerFactory;

    public AuthenticateCommand(ILogger<AuthenticateCommand> logger, ILoggerFactory loggerFactory)
        : base("Interactively authenticate using OpenID Connect")
    {
        _loggerFactory = loggerFactory;

        var authorityOption = new Option<string>(
            name: "--authority",
            description: "The OAuth2 authority to authenticate with")
        {
            IsRequired = true
        };

        authorityOption.AddAlias("-a");

        AddOption(authorityOption);

        var clientIdOption = new Option<string>(
            name: "--clientId",
            description: "The OAuth2 client ID")
        {
            IsRequired = true
        };

        clientIdOption.AddAlias("-c");

        AddOption(clientIdOption);

        var scopeOption = new Option<string>(
            name: "--scope",
            description: "The scope(s) to use, separated by spaces",
            getDefaultValue: () => "openid");

        scopeOption.AddAlias("-s");

        AddOption(scopeOption);

        var portOption = new Option<int?>(
            name: "--port",
            description: "The callback port to use in redirection. Defaults to a random port.");

        portOption.AddAlias("-p");

        AddOption(portOption);

        var audienceOption = new Option<string?>(
            name: "--audience",
            description: "The audience (optional)");

        AddOption(audienceOption);

        var diagnosticsOption = new Option<bool>(
            name: "--diagnostics",
            description: "Enable diagnostic output (optional)",
            getDefaultValue: () => false);

        AddOption(diagnosticsOption);

        this.SetHandler(async (authority, clientId, scope, port, audience, diagnostics) =>
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            await AuthenticateAsync(authority, clientId, scope, port, audience, diagnostics,
                cancellationTokenSource.Token).ConfigureAwait(false);

        }, authorityOption, clientIdOption, scopeOption, portOption, audienceOption, diagnosticsOption);
    }

    private async Task AuthenticateAsync(string authority, string clientId, string scope, int? port = null,
        string? audience = null, bool diagnostics = false, CancellationToken cancellationToken = default)
    {
        port ??= GetRandomUnusedPort();

        var options = new OidcClientOptions
        {
            Policy = new Policy { RequireAccessTokenHash = false },
            Authority = authority,
            ClientId = clientId,
            FilterClaims = false,
            LoadProfile = true,
            RedirectUri = $"http://127.0.0.1:{port}",
            PostLogoutRedirectUri = $"http://127.0.0.1:{port}",
            Scope = scope,
            Browser = new SystemBrowser(port.Value)
        };

        if (diagnostics)
            options.LoggerFactory = _loggerFactory;

        var oidcClient = new OidcClient(options);

        var loginRequest = new LoginRequest();

        if (!string.IsNullOrEmpty(audience))
        {
            loginRequest.FrontChannelExtraParameters = new Parameters(new Dictionary<string, string>
            {
                { "audience", audience }
            });
        }

        var result = await oidcClient.LoginAsync(loginRequest, cancellationToken);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (result.IsError)
            return;

        var output = new Output
        {
            IdToken = result.IdentityToken,
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresAt = result.AccessTokenExpiration,
            Claims = result.User.Claims.Select(c => new Output.Claim { Type = c.Type, Value = c.Value })
        };

        // We specifically use Console.WriteLine instead of a logger here to make the command output easier to parse.
        Console.WriteLine(JsonSerializer.Serialize(output, jsonOptions));
    }

    private static int GetRandomUnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }
}
