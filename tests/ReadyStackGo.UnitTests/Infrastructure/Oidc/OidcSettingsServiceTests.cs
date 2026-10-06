using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Services;
using ReadyStackGo.Infrastructure.Services.Oidc;

namespace ReadyStackGo.UnitTests.Infrastructure.Oidc;

/// <summary>
/// Tests the settings service against the real file store and the real encryption, so that
/// the stored rsgo.oidc.json is checked as it lands on disk.
/// </summary>
public class OidcSettingsServiceTests : IDisposable
{
    private const string Secret = "super-secret-value-123";

    private readonly string _root;
    private readonly ConfigStore _store;
    private readonly CredentialEncryptionService _encryption;
    private readonly OidcSettingsService _sut;

    public OidcSettingsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "rsgo-oidc-settings-tests", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConfigPath"] = Path.Combine(_root, "config"),
                ["DataPath"] = Path.Combine(_root, "data"),
                ["RSGO_ENCRYPTION_KEY"] = "test-master-key"
            })
            .Build();
        _store = new ConfigStore(configuration);
        _encryption = new CredentialEncryptionService(configuration, NullLogger<CredentialEncryptionService>.Instance);
        _sut = new OidcSettingsService(_store, _encryption);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Ignore cleanup errors
        }
    }

    private string OidcFile => Path.Combine(_root, "config", "rsgo.oidc.json");

    private static OidcProviderSettings NewProvider(string name = "wysch", string? secret = Secret) => new()
    {
        Name = name,
        DisplayName = "WYSCH",
        Authority = "https://id.example.com",
        ClientId = "client-1",
        ClientSecret = secret,
        Scopes = "openid profile email",
        Enabled = true,
        Template = "wysch",
        Registration = RegistrationKinds.Pairing,
        RequirePar = true,
        Claims = new OidcClaimNames { DisplayName = "nickname" },
        PairedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
        PairedBy = "marcus",
        TrustUnverifiedEmail = false
    };

    #region Compatibility with old entries

    [Fact]
    public async Task GetAll_OldJsonWithoutNewFields_ReadsAsGenericOidcManualTrustingEmail()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OidcFile)!);
        var encrypted = _encryption.Encrypt(Secret);
        await File.WriteAllTextAsync(OidcFile, $$"""
            {
              "providers": [
                {
                  "name": "IdentityAccess",
                  "displayName": "Identity Access",
                  "authority": "https://login.example.com/realms/main",
                  "clientId": "rsgo",
                  "encryptedClientSecret": "{{encrypted}}",
                  "scopes": "openid email profile",
                  "enabled": true
                }
              ]
            }
            """);

        var provider = (await _sut.GetAllAsync()).Should().ContainSingle().Subject;

        provider.Name.Should().Be("IdentityAccess");
        provider.ClientSecret.Should().Be(Secret);
        provider.Enabled.Should().BeTrue();
        provider.Template.Should().Be(OidcProviderSettings.GenericTemplateId);
        provider.Registration.Should().Be(RegistrationKinds.Manual);
        provider.IsPaired.Should().BeFalse();
        provider.TrustUnverifiedEmail.Should().BeTrue();
        provider.RequirePar.Should().BeFalse();
        provider.Claims.Should().Be(new OidcClaimNames());
        provider.TestedSignIn.Should().BeNull();
        provider.LastResult.Should().BeNull();
        provider.ReconnectNeeded.Should().BeFalse();
        provider.PairedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetByName_OldEntryWithUpperCaseName_IsFoundCaseInsensitive()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OidcFile)!);
        await File.WriteAllTextAsync(OidcFile,
            """{"providers":[{"name":"IdentityAccess","displayName":"x","authority":"https://a","clientId":"c","enabled":true}]}""");

        var provider = await _sut.GetByNameAsync("identityaccess");

        provider.Should().NotBeNull();
        provider!.ClientSecret.Should().BeNull();
    }

    [Fact]
    public async Task Update_OldEntry_KeepsOriginalNameSpelling()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OidcFile)!);
        await File.WriteAllTextAsync(OidcFile,
            """{"providers":[{"name":"IdentityAccess","displayName":"x","authority":"https://a","clientId":"c","enabled":true}]}""");
        var provider = (await _sut.GetByNameAsync("identityaccess"))!;
        provider.Name = "identityaccess";
        provider.DisplayName = "Renamed";

        await _sut.UpdateAsync(provider);

        var stored = (await _store.GetOidcConfigAsync()).Providers.Should().ContainSingle().Subject;
        stored.Name.Should().Be("IdentityAccess");
        stored.DisplayName.Should().Be("Renamed");
    }

    [Fact]
    public async Task GetAll_NoFile_ReturnsEmpty()
    {
        (await _sut.GetAllAsync()).Should().BeEmpty();
        (await _sut.GetByNameAsync("wysch")).Should().BeNull();
    }

    #endregion

    #region Add

    [Fact]
    public async Task Add_NewProvider_RoundTripsAllFields()
    {
        var provider = NewProvider();
        provider.TestedSignIn = new OidcTestedSignIn(DateTime.UtcNow, OidcConnectionFingerprint.Compute(provider));
        provider.LastResult = new OidcLastResult(DateTime.UtcNow, OidcResultKinds.TestSignIn, true, "ok");

        await _sut.AddAsync(provider);

        var read = (await _sut.GetByNameAsync("wysch"))!;
        read.TrustUnverifiedEmail.Should().BeFalse();
        read.Template.Should().Be("wysch");
        read.Registration.Should().Be(RegistrationKinds.Pairing);
        read.RequirePar.Should().BeTrue();
        read.Claims.DisplayName.Should().Be("nickname");
        read.PairedAt.Should().Be(provider.PairedAt);
        read.PairedBy.Should().Be("marcus");
        read.ClientSecret.Should().Be(Secret);
        read.TestedSignIn.Should().Be(provider.TestedSignIn);
        read.LastResult.Should().Be(provider.LastResult);
        read.HasPassedTestSignInForCurrentConnection.Should().BeTrue();
    }

    [Fact]
    public async Task Add_NewProvider_WritesTrustUnverifiedEmailFalseExplicitly()
    {
        await _sut.AddAsync(NewProvider());

        var stored = (await _store.GetOidcConfigAsync()).Providers.Single();
        stored.TrustUnverifiedEmail.Should().BeFalse();
        (await File.ReadAllTextAsync(OidcFile)).Should().Contain("\"trustUnverifiedEmail\": false");
    }

    [Fact]
    public async Task Add_SecretIsEncryptedAndNeverPlainInFile()
    {
        await _sut.AddAsync(NewProvider());

        var json = await File.ReadAllTextAsync(OidcFile);
        json.Should().NotContain(Secret);
        var stored = (await _store.GetOidcConfigAsync()).Providers.Single();
        stored.EncryptedClientSecret.Should().NotBeNullOrEmpty().And.NotBe(Secret);
        _encryption.Decrypt(stored.EncryptedClientSecret!).Should().Be(Secret);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Add_WithoutSecret_StoresNoSecret(string? secret)
    {
        await _sut.AddAsync(NewProvider(secret: secret));

        (await _store.GetOidcConfigAsync()).Providers.Single().EncryptedClientSecret.Should().BeNull();
        (await _sut.GetByNameAsync("wysch"))!.ClientSecret.Should().BeNull();
    }

    [Theory]
    [InlineData("wysch")]
    [InlineData("WYSCH")]
    public async Task Add_DuplicateName_ThrowsAndKeepsFirst(string duplicate)
    {
        await _sut.AddAsync(NewProvider());

        var act = () => _sut.AddAsync(NewProvider(name: duplicate, secret: "other"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        var providers = await _sut.GetAllAsync();
        providers.Should().ContainSingle().Which.ClientSecret.Should().Be(Secret);
    }

    [Fact]
    public async Task Add_EmptyScopes_StoresDefaultScopes()
    {
        var provider = NewProvider();
        provider.Scopes = " ";

        await _sut.AddAsync(provider);

        (await _sut.GetByNameAsync("wysch"))!.Scopes.Should().Be(OidcProviderSettings.DefaultScopes);
    }

    #endregion

    #region Update

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Update_EmptySecret_KeepsStoredSecret(string? secret)
    {
        await _sut.AddAsync(NewProvider());
        var changed = NewProvider(secret: secret);
        changed.DisplayName = "Changed";

        await _sut.UpdateAsync(changed);

        var read = (await _sut.GetByNameAsync("wysch"))!;
        read.ClientSecret.Should().Be(Secret);
        read.DisplayName.Should().Be("Changed");
    }

    [Fact]
    public async Task Update_NewSecret_ReplacesAndEncrypts()
    {
        await _sut.AddAsync(NewProvider());

        await _sut.UpdateAsync(NewProvider(secret: "new-secret-456"));

        (await _sut.GetByNameAsync("wysch"))!.ClientSecret.Should().Be("new-secret-456");
        var json = await File.ReadAllTextAsync(OidcFile);
        json.Should().NotContain("new-secret-456").And.NotContain(Secret);
    }

    [Fact]
    public async Task Update_UnknownProvider_ThrowsKeyNotFound()
    {
        var act = () => _sut.UpdateAsync(NewProvider("unknown"));

        await act.Should().ThrowAsync<KeyNotFoundException>();
        (await _sut.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Update_KeepsOrderOfProviders()
    {
        await _sut.AddAsync(NewProvider("first"));
        await _sut.AddAsync(NewProvider("second"));

        await _sut.UpdateAsync(NewProvider("first"));

        (await _sut.GetAllAsync()).Select(p => p.Name).Should().Equal("first", "second");
    }

    #endregion

    #region Remove

    [Fact]
    public async Task Remove_Existing_ReturnsTrueAndRemovesOnlyThatProvider()
    {
        await _sut.AddAsync(NewProvider("first"));
        await _sut.AddAsync(NewProvider("second"));

        (await _sut.RemoveAsync("FIRST")).Should().BeTrue();

        (await _sut.GetAllAsync()).Select(p => p.Name).Should().Equal("second");
    }

    [Fact]
    public async Task Remove_Unknown_ReturnsFalse()
    {
        await _sut.AddAsync(NewProvider());

        (await _sut.RemoveAsync("unknown")).Should().BeFalse();

        (await _sut.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Remove_ThenAddSameName_Works()
    {
        await _sut.AddAsync(NewProvider());
        await _sut.RemoveAsync("wysch");

        await _sut.AddAsync(NewProvider(secret: "fresh-secret"));

        (await _sut.GetByNameAsync("wysch"))!.ClientSecret.Should().Be("fresh-secret");
    }

    #endregion

    #region RecordResult

    [Fact]
    public async Task RecordResult_StoresResultAndKeepsOtherFields()
    {
        await _sut.AddAsync(NewProvider());
        var result = new OidcLastResult(new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc), OidcResultKinds.SignIn, false, "invalid_client");

        await _sut.RecordResultAsync("wysch", result, reconnectNeeded: true);

        var read = (await _sut.GetByNameAsync("wysch"))!;
        read.LastResult.Should().Be(result);
        read.ReconnectNeeded.Should().BeTrue();
        read.ClientSecret.Should().Be(Secret);
        read.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task RecordResult_ReconnectNeededNull_KeepsFlag()
    {
        await _sut.AddAsync(NewProvider());
        await _sut.RecordResultAsync("wysch", new OidcLastResult(DateTime.UtcNow, OidcResultKinds.SignIn, false, null), true);

        await _sut.RecordResultAsync("wysch", new OidcLastResult(DateTime.UtcNow, OidcResultKinds.Checks, true, null));

        (await _sut.GetByNameAsync("wysch"))!.ReconnectNeeded.Should().BeTrue();
    }

    [Fact]
    public async Task RecordResult_ReconnectNeededFalse_ClearsFlag()
    {
        await _sut.AddAsync(NewProvider());
        await _sut.RecordResultAsync("wysch", new OidcLastResult(DateTime.UtcNow, OidcResultKinds.SignIn, false, null), true);

        await _sut.RecordResultAsync("wysch", new OidcLastResult(DateTime.UtcNow, OidcResultKinds.SignIn, true, null), false);

        (await _sut.GetByNameAsync("wysch"))!.ReconnectNeeded.Should().BeFalse();
    }

    [Fact]
    public async Task RecordResult_UnknownProvider_IsIgnored()
    {
        await _sut.AddAsync(NewProvider());

        var act = () => _sut.RecordResultAsync("unknown", new OidcLastResult(DateTime.UtcNow, OidcResultKinds.SignIn, true, null), true);

        await act.Should().NotThrowAsync();
        (await _sut.GetAllAsync()).Should().ContainSingle(p => p.Name == "wysch" && p.LastResult == null);
    }

    #endregion

    #region Connection fingerprint

    private static string Fingerprint(Action<OidcProviderSettings>? change = null)
    {
        var provider = NewProvider();
        change?.Invoke(provider);
        return OidcConnectionFingerprint.Compute(provider);
    }

    [Fact]
    public void Fingerprint_ChangesWithEachConnectionField()
    {
        var original = Fingerprint();

        Fingerprint(p => p.Authority = "https://other.example.com").Should().NotBe(original);
        Fingerprint(p => p.ClientId = "client-2").Should().NotBe(original);
        Fingerprint(p => p.ClientSecret = "other-secret").Should().NotBe(original);
        Fingerprint(p => p.ClientSecret = null).Should().NotBe(original);
        Fingerprint(p => p.Scopes = "openid profile").Should().NotBe(original);
    }

    [Fact]
    public void Fingerprint_IgnoresNonConnectionFieldsAndFormatting()
    {
        var original = Fingerprint();

        Fingerprint(p => p.DisplayName = "Other").Should().Be(original);
        Fingerprint(p => p.Enabled = false).Should().Be(original);
        Fingerprint(p => p.TrustUnverifiedEmail = true).Should().Be(original);
        Fingerprint(p => p.Authority = " https://id.example.com/ ").Should().Be(original);
        Fingerprint(p => p.Scopes = "email  openid profile openid").Should().Be(original);
    }

    [Fact]
    public async Task TestedSignIn_NoLongerCountsAfterConnectionChange()
    {
        var provider = NewProvider();
        provider.TestedSignIn = new OidcTestedSignIn(DateTime.UtcNow, OidcConnectionFingerprint.Compute(provider));
        await _sut.AddAsync(provider);

        var read = (await _sut.GetByNameAsync("wysch"))!;
        read.HasPassedTestSignInForCurrentConnection.Should().BeTrue();
        read.ClientId = "client-2";

        read.HasPassedTestSignInForCurrentConnection.Should().BeFalse();
    }

    #endregion
}
